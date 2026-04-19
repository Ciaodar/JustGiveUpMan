using System;
using JGUM.Calculators;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace JGUM.Behaviors.SiegeNegotiationBehavior
{
    public partial class SiegeNegotiationBehavior
    {
        private void AddSiegeMenuOption(CampaignGameStarter starter)
        {
            starter.AddGameMenuOption(
                SiegeStrategiesMenuId,
                OptionId,
                StringCalculator.GetString("jgum_siege_negotiation_menu_option", "Offer a Negotiation Meeting"),
                OfferNegotiationOptionCondition,
                OfferNegotiationOptionConsequence,
                false,
                3,
                false);
        }

        private bool OfferNegotiationOptionCondition(MenuCallbackArgs args)
        {
            CleanupInvalidNegotiationState();

            Settlement? settlement = GetCurrentPlayerSiegeSettlement();
            if (settlement?.SiegeEvent == null)
            {
                args.IsEnabled = false;
                args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
                args.Tooltip = new TextObject(StringCalculator.GetString(
                    "jgum_siege_negotiation_menu_tooltip_no_context",
                    "Negotiation is not available in the current siege context."));
                return true;
            }

            args.optionLeaveType = GameMenuOption.LeaveType.Submenu;

            string key = settlement.StringId;
            if (TryGetPendingRequest(settlement, out _))
            {
                args.IsEnabled = false;
                args.Tooltip = new TextObject(StringCalculator.GetString(
                    "jgum_siege_negotiation_menu_tooltip_pending",
                    "Your previous request is still awaiting a response."));
                return true;
            }

            if (FailedRetryCooldownBySettlement.TryGetValue(key, out CampaignTime retryAt) && CampaignTime.Now.ToHours < retryAt.ToHours)
            {
                args.IsEnabled = false;
                args.Tooltip = new TextObject(StringCalculator.GetString(
                    "jgum_siege_negotiation_menu_tooltip_retry",
                    "You need to wait before trying negotiation again after a failed persuasion."));
                return true;
            }

            if (RequestCooldownBySettlement.TryGetValue(key, out CampaignTime cooldownAt) && CampaignTime.Now.ToHours < cooldownAt.ToHours)
            {
                args.IsEnabled = false;
                args.Tooltip = new TextObject(StringCalculator.GetString(
                    "jgum_siege_negotiation_menu_tooltip_cooldown",
                    "You recently sent a negotiation request. Wait before sending another."));
                return true;
            }

            args.IsEnabled = true;
            return true;
        }

        private void OfferNegotiationOptionConsequence(MenuCallbackArgs args)
        {
            CleanupInvalidNegotiationState();

            Settlement? settlement = GetCurrentPlayerSiegeSettlement();
            if (settlement == null)
                return;

            RequestCooldownBySettlement[settlement.StringId] = CampaignTime.HoursFromNow(RequestButtonCooldownHours);

            _pendingRequests.Add(new PendingNegotiationRequest
            {
                Settlement = settlement,
                ResolveAt = CampaignTime.HoursFromNow(RequestResponseDelayHours)
            });

            InformationManager.DisplayMessage(new InformationMessage(
                StringCalculator.GetString("jgum_siege_negotiation_request_sent", "Your messenger has been sent. You will receive a response soon."),
                Colors.White));

            GameMenu.SwitchToMenu(SiegeStrategiesMenuId);
        }

        private void OnHourlyTick()
        {
            CleanupInvalidNegotiationState();

            if (_pendingRequests.Count == 0)
                return;

            for (int i = _pendingRequests.Count - 1; i >= 0; i--)
            {
                PendingNegotiationRequest request = _pendingRequests[i];
                if (CampaignTime.Now.ToHours < request.ResolveAt.ToHours)
                    continue;

                _pendingRequests.RemoveAt(i);
                ResolvePendingRequest(request);
            }
        }

        private void ResolvePendingRequest(PendingNegotiationRequest request)
        {
            Settlement settlement = request.Settlement;
            if (settlement.SiegeEvent == null || !IsPlayerBesieger(settlement))
                return;

            float powerRatio = NegotiationCalculator.GetPowerRatio(settlement);
            bool approved = powerRatio >= 1f;

            string title = StringCalculator.GetString("jgum_siege_negotiation_inquiry_title", "Negotiation Response");

            if (!approved)
            {
                string rejectedBody = StringCalculator.GetString(
                    "jgum_siege_negotiation_inquiry_rejected",
                    "Your proposal was rejected. The defenders do not believe your position is strong enough.");

                InformationManager.ShowInquiry(new InquiryData(
                    title,
                    rejectedBody,
                    true,
                    false,
                    StringCalculator.GetString("jgum_siege_negotiation_inquiry_ok", "OK"),
                    string.Empty,
                    () => { },
                    null),
                    true);

                return;
            }

            string acceptedBody = StringCalculator.GetString(
                "jgum_siege_negotiation_inquiry_accepted",
                "Your proposal was accepted. A representative is ready to meet you.");

            InformationManager.ShowInquiry(new InquiryData(
                title,
                acceptedBody,
                true,
                false,
                StringCalculator.GetString("jgum_siege_negotiation_inquiry_go", "Go to meeting"),
                string.Empty,
                () => StartNegotiationConversation(settlement, powerRatio),
                null),
                true);
        }

        private void StartNegotiationConversation(Settlement settlement, float powerRatio)
        {
            if (settlement.SiegeEvent == null || !IsPlayerBesieger(settlement))
                return;

            CharacterObject? defenderCharacter = FindSettlementRepresentative(settlement);
            if (defenderCharacter == null)
                return;

            _activeSettlement = settlement;
            _activeSettlementKey = settlement.StringId;
            BuildPersuasionTasks(powerRatio);

            var playerData = new ConversationCharacterData(CharacterObject.PlayerCharacter);
            var defenderData = new ConversationCharacterData(
                defenderCharacter,
                spawnAfterFight: true,
                noWeapon: true,
                noBodyguards: true,
                isCivilianEquipmentRequiredForLeader: true,
                isCivilianEquipmentRequiredForBodyGuardCharacters: true);

            CampaignMapConversation.OpenConversation(playerData, defenderData);
        }

        private static Settlement? GetCurrentPlayerSiegeSettlement()
        {
            Settlement? current = PlayerEncounter.EncounterSettlement
                                  ?? MobileParty.MainParty?.BesiegedSettlement
                                  ?? Settlement.CurrentSettlement;
            if (current?.SiegeEvent == null)
                return null;

            return current;
        }

        private static bool IsPlayerBesieger(Settlement settlement)
        {
            PartyBase? playerParty = MobileParty.MainParty?.Party;
            return playerParty != null && settlement.SiegeEvent?.BesiegerCamp.HasInvolvedPartyForEventType(playerParty) == true;
        }

        private bool TryGetPendingRequest(Settlement settlement, out PendingNegotiationRequest? request)
        {
            request = _pendingRequests.Find(x => x.Settlement == settlement);
            return request != null;
        }
    }
}

