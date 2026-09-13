using System;
using System.Collections.Generic;
using System.Linq;
using JGUM.Calculators;
using JGUM.Config;
using JGUM.Interop;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace JGUM.Behaviors
{
    public class FiefPurchaseOfferBehavior : CampaignBehaviorBase
    {
        private Dictionary<string, CampaignTime> _offerCooldownBySettlement = new Dictionary<string, CampaignTime>();
        private Dictionary<string, CampaignTime> _siegeStartBySettlement = new Dictionary<string, CampaignTime>();

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickSettlementEvent.AddNonSerializedListener(this, OnDailyTickSettlement);
        }

        public void ClearAllData()
        {
            _offerCooldownBySettlement?.Clear();
            _siegeStartBySettlement?.Clear();
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("JGUM_FiefPurchaseOfferCooldowns", ref _offerCooldownBySettlement);
            dataStore.SyncData("JGUM_FiefPurchaseSiegeStarts", ref _siegeStartBySettlement);
        }

        private void OnDailyTickSettlement(Settlement settlement)
        {
            string key = settlement.StringId;

            // Only process settlements under siege
            if (!settlement.IsUnderSiege)
            {
                _siegeStartBySettlement.Remove(key);
                return;
            }

            if (settlement.Party?.MapEvent != null)
                return;

            if (!_siegeStartBySettlement.ContainsKey(key))
            {
                _siegeStartBySettlement[key] = CampaignTime.Now;
            }

            // Feature toggle check
            if (!JgumSettingsManager.EnableFiefPurchaseOffer)
                return;

            // Only trigger when player is the DEFENDER, not the besieger
            if (!IsPlayerDefender(settlement))
                return;

            // Check besieger leader exists and has sufficient Trade skill
            Hero? besiegerLeader = settlement.SiegeEvent?.BesiegerCamp?.LeaderParty?.LeaderHero;
            if (besiegerLeader == null)
                return;

            int tradeSkill = besiegerLeader.GetSkillValue(DefaultSkills.Trade);
            if (tradeSkill < JgumSettingsManager.MinTradeSkillForFiefOffer)
                return;

            // Check cooldown
            if (_offerCooldownBySettlement.TryGetValue(key, out CampaignTime cooldownUntil) &&
                CampaignTime.Now.ToDays < cooldownUntil.ToDays)
                return;

            // Daily chance roll
            if (MBRandom.RandomFloat > JgumSettingsManager.FiefPurchaseOfferDailyChance)
                return;

            // Calculate offer price
            int offerPrice = CalculateOfferPrice(settlement, besiegerLeader);
            if (offerPrice <= 0)
                return;

            // Show the offer to the player
            ShowPurchaseOffer(settlement, besiegerLeader, offerPrice);
        }

        private int CalculateOfferPrice(Settlement settlement, Hero besiegerLeader)
        {
            // Base price from settlement prosperity
            float prosperity = settlement.Town?.Prosperity ?? 1000f;
            float basePrice = prosperity * 10f;

            // Power discount: stronger attacker = lower price
            // (defenderStrength / attackerStrength) — when attacker is 2x stronger, ratio is 0.5
            float attackerStrength = 0f;
            float defenderStrength = 0f;

            var siegeEvent = settlement.SiegeEvent;
            if (siegeEvent != null)
            {
                var attackerParties = siegeEvent.BesiegerCamp.GetInvolvedPartiesForEventType();
                var defenderParties = siegeEvent.GetSiegeEventSide(BattleSideEnum.Defender)?.GetInvolvedPartiesForEventType();

                if (attackerParties != null)
                    attackerStrength = attackerParties.Sum(p => p.CalculateCurrentStrength());
                if (defenderParties != null)
                    defenderStrength = defenderParties.Sum(p => p.CalculateCurrentStrength());
            }

            // Prevent division by zero
            float powerDiscount = attackerStrength > 0f
                ? Math.Max(defenderStrength / attackerStrength, 0.1f)
                : 1f;

            // Time bonus: longer siege = higher offer (5% per day)
            string key = settlement.StringId;
            int siegeDays = 0;
            if (_siegeStartBySettlement.TryGetValue(key, out CampaignTime startTime))
            {
                siegeDays = (int)(CampaignTime.Now.ToDays - startTime.ToDays);
            }
            float timeBonus = 1f + (siegeDays * 0.05f);

            // Apply configurable multiplier
            float finalPrice = basePrice * powerDiscount * timeBonus * JgumSettingsManager.FiefPriceOfferMultiplier;

            return Math.Max((int)finalPrice, 1);
        }

        private void ShowPurchaseOffer(Settlement settlement, Hero besiegerLeader, int offerPrice)
        {
            MBTextManager.SetTextVariable("SETTLEMENT_NAME", settlement.Name?.ToString() ?? settlement.StringId);
            MBTextManager.SetTextVariable("BESIEGER_NAME", besiegerLeader.Name?.ToString() ?? "the enemy");
            MBTextManager.SetTextVariable("OFFER_GOLD", offerPrice.ToString());
            MBTextManager.SetTextVariable("GOLD_ICON", "{=!}<img src=\"General\\Icons\\Coin@2x\" extend=\"8\">");

            string title = StringCalculator.GetString("jgum_fief_purchase_inquiry_title", "A Merchant's Proposition");
            string body = StringCalculator.GetString(
                "jgum_fief_purchase_inquiry_body",
                "{BESIEGER_NAME} offers {OFFER_GOLD}{GOLD_ICON} gold for the peaceful surrender of {SETTLEMENT_NAME}. The offer comes with guarantees: no looting, no reprisals against the garrison.");

            InformationManager.ShowInquiry(new InquiryData(
                title,
                body,
                true,
                true,
                StringCalculator.GetString("jgum_fief_purchase_accept", "Accept the offer"),
                StringCalculator.GetString("jgum_fief_purchase_reject", "Reject the offer"),
                () => OnOfferAccepted(settlement, besiegerLeader, offerPrice),
                () => OnOfferRejected(settlement)
            ), true);
        }

        private void OnOfferAccepted(Settlement settlement, Hero besiegerLeader, int offerPrice)
        {
            // Transfer gold to the player
            GiveGoldAction.ApplyBetweenCharacters(besiegerLeader, Hero.MainHero, offerPrice, true);

            // Transfer settlement ownership
            JGUM.Actions.WarScoreHelper.RecordSiegeSurrenderWarScore(settlement, besiegerLeader);
            ChangeOwnerOfSettlementAction.ApplyBySiege(besiegerLeader, besiegerLeader, settlement);

            // Relationship bonus for peaceful resolution
            Hero.MainHero.SetPersonalRelation(besiegerLeader, (int)besiegerLeader.GetRelationWithPlayer() + 5);

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

            // Notify player
            InformationManager.DisplayMessage(new InformationMessage(
                StringCalculator.GetString("jgum_fief_purchase_accepted_msg",
                    "The settlement has been handed over. Gold changes hands, and the siege ends without further bloodshed."),
                Colors.Green));
        }

        private void OnOfferRejected(Settlement settlement)
        {
            string key = settlement.StringId;
            _offerCooldownBySettlement[key] = CampaignTime.DaysFromNow(JgumSettingsManager.FiefPurchaseOfferCooldownDays);

            InformationManager.DisplayMessage(new InformationMessage(
                StringCalculator.GetString("jgum_fief_purchase_rejected_msg",
                    "You have rejected the offer. The siege continues."),
                Colors.Yellow));
                
            var sEvent = settlement.SiegeEvent;
            var bLeader = sEvent?.BesiegerCamp?.LeaderParty?.LeaderHero;
            if (bLeader != null)
            {
                JgumInteropEvents.RaiseSurrenderResolved(new JgumSurrenderRecord
                {
                    Kind = JgumSurrenderKind.SiegeNegotiatedSurrender,
                    SettlementId = settlement.StringId,
                    SurrenderingHeroId = settlement.OwnerClan?.Leader?.StringId,
                    SurrenderingPartyName = settlement.Name?.ToString(),
                    WinnerHeroId = bLeader.StringId,
                    WinnerClanId = bLeader.Clan?.StringId,
                    LoserFactionId = settlement.MapFaction?.StringId,
                    CampaignTimeDays = (float)CampaignTime.Now.ToDays,
                    AcceptedByPlayer = false,
                    Outcome = JgumSurrenderOutcome.Rejected
                });
            }
        }

        private static bool IsPlayerDefender(Settlement settlement)
        {
            // The player is a defender if:
            // 1. The settlement belongs to the player's clan, OR
            // 2. The player's party is inside the settlement as a defender
            if (settlement.OwnerClan == Hero.MainHero?.Clan)
                return true;

            var playerParty = MobileParty.MainParty?.Party;
            if (playerParty == null)
                return false;

            var defenderSide = settlement.SiegeEvent?.GetSiegeEventSide(BattleSideEnum.Defender);
            return defenderSide?.HasInvolvedPartyForEventType(playerParty) == true;
        }
    }
}

