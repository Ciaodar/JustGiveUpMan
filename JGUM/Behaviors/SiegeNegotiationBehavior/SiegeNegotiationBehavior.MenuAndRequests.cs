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
                StringCalculator.GetString("jgum_siege_negotiation_menu_option", "Propose a parley to avoid further bloodshed."),
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
                    "The chaos of the siege prevents any meaningful talk."));
                return true;
            }

            // Only show negotiation option when the player is the besieger, not the defender.
            if (!IsPlayerBesieger(settlement))
                return false;

            args.optionLeaveType = GameMenuOption.LeaveType.ShowMercy;

            string key = settlement.StringId;
            if (TryGetPendingRequest(settlement, out _))
            {
                args.IsEnabled = false;
                args.Tooltip = new TextObject(StringCalculator.GetString(
                    "jgum_siege_negotiation_menu_tooltip_pending",
                    "Your envoy has yet to return. Patience, {?PLAYER.GENDER}madam{?}sir{\\?}"));
                return true;
            }

            if (FailedRetryCooldownBySettlement.TryGetValue(key, out CampaignTime retryAt) &&
                CampaignTime.Now.ToHours < retryAt.ToHours)
            {
                args.IsEnabled = false;
                args.Tooltip = new TextObject(StringCalculator.GetString(
                    "jgum_siege_negotiation_menu_tooltip_retry",
                    "They will not entertain another of your messengers so soon after the last failure."));
                return true;
            }

            if (RequestCooldownBySettlement.TryGetValue(key, out CampaignTime cooldownAt) &&
                CampaignTime.Now.ToHours < cooldownAt.ToHours)
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
                StringCalculator.GetString("jgum_siege_negotiation_request_sent",
                    "Your rider carries the terms under a flag of truce. We await their reply."),
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

            string title = StringCalculator.GetString("jgum_siege_negotiation_inquiry_title", "A Raven Arrives");

            if (!approved)
            {
                string rejectedBody = StringCalculator.GetString(
                    "jgum_siege_negotiation_inquiry_rejected",
                    "A message returns, spattered with mud. The defenders scoff at your terms, believing their walls stronger than your will.");

                InformationManager.ShowInquiry(new InquiryData(
                        title,
                        rejectedBody,
                        true,
                        false,
                        StringCalculator.GetString("jgum_siege_negotiation_inquiry_ok", "So be it."),
                        string.Empty,
                        () => { },
                        null),
                    true);

                return;
            }

            string acceptedBody = StringCalculator.GetString(
                "jgum_siege_negotiation_inquiry_accepted",
                "A clean scroll arrives, bearing the defender's seal. They will meet with you.");

            InformationManager.ShowInquiry(new InquiryData(
                    title,
                    acceptedBody,
                    true,
                    false,
                    StringCalculator.GetString("jgum_siege_negotiation_inquiry_go", "Attend the parley."),
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
                character: defenderCharacter,
                party: null,
                noHorse: true,
                noWeapon: true,
                spawnAfterFight: false,
                isCivilianEquipmentRequiredForLeader: defenderCharacter.IsHero,
                isCivilianEquipmentRequiredForBodyGuardCharacters: false,
                noBodyguards: true);

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
            return playerParty != null &&
                   settlement.SiegeEvent?.BesiegerCamp.HasInvolvedPartyForEventType(playerParty) == true;
        }

        private bool TryGetPendingRequest(Settlement settlement, out PendingNegotiationRequest? request)
        {
            request = _pendingRequests.Find(x => x.Settlement == settlement);
            return request != null;
        }
    }
}
