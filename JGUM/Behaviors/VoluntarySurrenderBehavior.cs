using System.Collections.Generic;
using JGUM.Calculators;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using JGUM.Interop;

namespace JGUM.Behaviors
{
    public class VoluntarySurrenderBehavior : CampaignBehaviorBase
    {
        private const string SiegeStrategiesMenuId = "menu_siege_strategies";
        private const string OptionId = "jgum_voluntary_surrender_menu_option";
        
        private Dictionary<string, CampaignTime> _surrenderCooldownBySettlement = new Dictionary<string, CampaignTime>();
        private Dictionary<Hero, int> _besiegerRejectionCounts = new Dictionary<Hero, int>();

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        public void ClearAllData()
        {
            _surrenderCooldownBySettlement?.Clear();
            _besiegerRejectionCounts?.Clear();
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("JGUM_VoluntarySurrenderCooldowns", ref _surrenderCooldownBySettlement);
            dataStore.SyncData("JGUM_BesiegerRejectionCounts", ref _besiegerRejectionCounts);
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            AddSiegeMenuOption(starter);
        }

        private void AddSiegeMenuOption(CampaignGameStarter starter)
        {
            starter.AddGameMenuOption(
                SiegeStrategiesMenuId,
                OptionId,
                StringCalculator.GetString("jgum_voluntary_surrender_menu_option", "Offer to surrender the settlement"),
                OfferVoluntarySurrenderCondition,
                OfferVoluntarySurrenderConsequence,
                false,
                4,
                false);
        }

        private bool OfferVoluntarySurrenderCondition(MenuCallbackArgs args)
        {
            if (!Config.JgumSettingsManager.EnableVoluntarySurrender)
                return false;

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

            // Only show when the player is the defender.
            if (!IsPlayerDefender(settlement))
                return false;

            args.optionLeaveType = GameMenuOption.LeaveType.Surrender;

            string key = settlement.StringId;
            if (_surrenderCooldownBySettlement.TryGetValue(key, out CampaignTime cooldownUntil) &&
                CampaignTime.Now.ToHours < cooldownUntil.ToHours)
            {
                args.IsEnabled = false;
                args.Tooltip = new TextObject(StringCalculator.GetString(
                    "jgum_voluntary_surrender_menu_tooltip_cooldown",
                    "You have recently sent a surrender offer. The enemy will not entertain another messenger so soon."));
                return true;
            }

            args.IsEnabled = true;
            return true;
        }

        private void OfferVoluntarySurrenderConsequence(MenuCallbackArgs args)
        {
            Settlement? settlement = GetCurrentPlayerSiegeSettlement();
            if (settlement?.SiegeEvent == null)
                return;

            Hero? besiegerLeader = settlement.SiegeEvent.BesiegerCamp?.LeaderParty?.LeaderHero;
            if (besiegerLeader == null)
                return;

            // Acceptance probability: clamp(baseChance + besiegerLeader.Mercy * 30, 0, 100)
            int mercyLevel = besiegerLeader.GetTraitLevel(DefaultTraits.Mercy);
            int acceptanceChance = MBMath.ClampInt(Config.JgumSettingsManager.VoluntarySurrenderBaseChance + (mercyLevel * 30), 0, 100);

            bool isAccepted = MBRandom.RandomInt(100) < acceptanceChance;

            if (isAccepted)
            {
                OnSurrenderAccepted(settlement, besiegerLeader);
            }
            else
            {
                OnSurrenderRejected(settlement, besiegerLeader);
            }
        }

        private void OnSurrenderAccepted(Settlement settlement, Hero besiegerLeader)
        {
            MBTextManager.SetTextVariable("SETTLEMENT_NAME", settlement.Name?.ToString() ?? settlement.StringId);
            MBTextManager.SetTextVariable("BESIEGER_NAME", besiegerLeader.Name?.ToString() ?? "the enemy");

            string title = StringCalculator.GetString("jgum_voluntary_surrender_inquiry_title", "Surrender Accepted");
            string body = StringCalculator.GetString(
                "jgum_voluntary_surrender_accepted_body",
                "{BESIEGER_NAME} has accepted your terms of surrender. The garrison will be spared and the settlement changes hands peacefully.");

            InformationManager.ShowInquiry(new InquiryData(
                title,
                body,
                true,
                false,
                StringCalculator.GetString("jgum_voluntary_surrender_ok", "So be it."),
                string.Empty,
                () => ExecuteSurrender(settlement, besiegerLeader),
                null
            ), true);
        }

        private void ExecuteSurrender(Settlement settlement, Hero besiegerLeader)
        {
            // Player's traits decrease via config values
            int honorPenalty = Config.JgumSettingsManager.VoluntarySurrenderHonorPenalty;
            if (honorPenalty < 0)
                TraitLevelingHelper.OnIncidentResolved(DefaultTraits.Honor, honorPenalty);
                
            int valorPenalty = Config.JgumSettingsManager.VoluntarySurrenderValorPenalty;
            if (valorPenalty < 0)
                TraitLevelingHelper.OnIncidentResolved(DefaultTraits.Valor, valorPenalty);

            int mercyReward = Config.JgumSettingsManager.VoluntarySurrenderMercyReward;
            if (mercyReward > 0)
                TraitLevelingHelper.OnIncidentResolved(DefaultTraits.Mercy, mercyReward);

            int calculatingReward = Config.JgumSettingsManager.VoluntarySurrenderCalculatingReward;
            if (calculatingReward > 0)
                TraitLevelingHelper.OnIncidentResolved(DefaultTraits.Calculating, calculatingReward);

            // Relationship bonus for peaceful resolution
            Hero.MainHero.SetPersonalRelation(besiegerLeader, (int)besiegerLeader.GetRelationWithPlayer() + 5);

            // Dismiss the garrison so they aren't taken prisoner or absorbed
            if (settlement.Town?.GarrisonParty != null)
            {
                settlement.Town.GarrisonParty.MemberRoster?.Clear();
                settlement.Town.GarrisonParty.PrisonRoster?.Clear();
            }

            // Transfer settlement ownership without pillaging
            JGUM.Actions.WarScoreHelper.RecordSiegeSurrenderWarScore(settlement, besiegerLeader);
            ChangeOwnerOfSettlementAction.ApplyByDefault(besiegerLeader, settlement);

            // Finalize siege
            if (PlayerEncounter.Current != null)
                PlayerEncounter.Finish();

            settlement.SiegeEvent?.FinalizeSiegeEvent();
            
            JgumInteropEvents.RaiseSurrenderResolved(new JgumSurrenderRecord
            {
                Kind = JgumSurrenderKind.SiegeNegotiatedSurrender,
                SettlementId = settlement.StringId,
                SurrenderingHeroId = settlement.OwnerClan?.Leader?.StringId,
                SurrenderingPartyName = settlement.Name?.ToString(),
                WinnerHeroId = besiegerLeader.StringId,
                WinnerClanId = besiegerLeader.Clan?.StringId,
                LoserFactionId = settlement.MapFaction?.StringId,
                CampaignTimeDays = (float)CampaignTime.Now.ToDays,
                AcceptedByPlayer = true,
                Outcome = JgumSurrenderOutcome.Accepted
            });
        }

        private void OnSurrenderRejected(Settlement settlement, Hero besiegerLeader)
        {
            // Apply cooldown
            _surrenderCooldownBySettlement[settlement.StringId] = CampaignTime.HoursFromNow(24f);

            // Track rejection count for besieger
            if (!_besiegerRejectionCounts.ContainsKey(besiegerLeader))
            {
                _besiegerRejectionCounts[besiegerLeader] = 0;
            }

            _besiegerRejectionCounts[besiegerLeader]++;

            // Every 3 rejections, drop Honor and Mercy by 1
            if (_besiegerRejectionCounts[besiegerLeader] >= 3)
            {
                int currentHonor = besiegerLeader.GetTraitLevel(DefaultTraits.Honor);
                if (currentHonor > -2)
                    besiegerLeader.SetTraitLevel(DefaultTraits.Honor, currentHonor - 1);

                int currentMercy = besiegerLeader.GetTraitLevel(DefaultTraits.Mercy);
                if (currentMercy > -2)
                    besiegerLeader.SetTraitLevel(DefaultTraits.Mercy, currentMercy - 1);

                _besiegerRejectionCounts[besiegerLeader] = 0;
            }

            MBTextManager.SetTextVariable("SETTLEMENT_NAME", settlement.Name?.ToString() ?? settlement.StringId);
            MBTextManager.SetTextVariable("BESIEGER_NAME", besiegerLeader.Name?.ToString() ?? "the enemy");

            string title = StringCalculator.GetString("jgum_voluntary_surrender_inquiry_title", "Surrender Rejected");
            string body = StringCalculator.GetString(
                "jgum_voluntary_surrender_rejected_body",
                "{BESIEGER_NAME} refuses your offer. They intend to take {SETTLEMENT_NAME} by force and show no mercy to its defenders.");

            InformationManager.ShowInquiry(new InquiryData(
                title,
                body,
                true,
                false,
                StringCalculator.GetString("jgum_voluntary_surrender_ok", "So be it."),
                string.Empty,
                () => { },
                null
            ), true);
            
            JgumInteropEvents.RaiseSurrenderResolved(new JgumSurrenderRecord
            {
                Kind = JgumSurrenderKind.SiegeNegotiatedSurrender,
                SettlementId = settlement.StringId,
                SurrenderingHeroId = settlement.OwnerClan?.Leader?.StringId,
                SurrenderingPartyName = settlement.Name?.ToString(),
                WinnerHeroId = besiegerLeader.StringId,
                WinnerClanId = besiegerLeader.Clan?.StringId,
                LoserFactionId = settlement.MapFaction?.StringId,
                CampaignTimeDays = (float)CampaignTime.Now.ToDays,
                AcceptedByPlayer = false,
                Outcome = JgumSurrenderOutcome.Rejected
            });
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

        private static bool IsPlayerDefender(Settlement settlement)
        {
            // Only allow abandoning the settlement if it actually belongs to the player's clan
            if (settlement.OwnerClan == Hero.MainHero?.Clan)
            {
                var playerParty = MobileParty.MainParty?.Party;
                if (playerParty != null)
                {
                    var defenderSide = settlement.SiegeEvent?.GetSiegeEventSide(BattleSideEnum.Defender);
                    return defenderSide?.HasInvolvedPartyForEventType(playerParty) == true;
                }
            }

            return false;
        }
    }
}

