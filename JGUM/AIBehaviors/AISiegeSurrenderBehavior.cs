using System;
using System.Collections.Generic;
using System.Linq;
using JGUM.Calculators;
using JGUM.Config;
using JGUM.Data;
using JGUM.Interop;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace JGUM.AIBehaviors
{
    public class AISiegeSurrenderBehavior : CampaignBehaviorBase
    {
        private int _dailySurrenderCount = 0;

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.DailyTickSettlementEvent.AddNonSerializedListener(this, OnDailyTickSettlement);
        }

        public void ClearAllData()
        {
            _dailySurrenderCount = 0;
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("JGUM_AiSiegeDailySurrenderCount", ref _dailySurrenderCount);
        }

        private void OnDailyTick()
        {
            _dailySurrenderCount = 0;
        }

        private void OnDailyTickSettlement(Settlement settlement)
        {
            if (!JgumSettingsManager.EnableAiVsAiSiegeSurrender) return;

            if (settlement?.SiegeEvent == null || !settlement.IsUnderSiege) return;

            // Do not trigger surrender if an assault (MapEvent) is actively ongoing to prevent simulation crashes
            if (settlement.Party?.MapEvent != null) return;

            // Check if player is involved (we only want AI vs AI)
            if (IsPlayerInvolvedInSiege(settlement)) return;

            // Check daily limit
            if (_dailySurrenderCount >= JgumSettingsManager.AiVsAiSiegeDailySurrenderLimit) return;

            var siegeEvent = settlement.SiegeEvent;
            var attackers = siegeEvent.BesiegerCamp.GetInvolvedPartiesForEventType().ToList();
            var defenders = settlement.Parties.Select(p => p.Party).ToList();
            
            // Defending lords
            var defendingLords = defenders.Where(p => p.MobileParty?.LeaderHero != null)
                .Select(p => p.MobileParty.LeaderHero)
                .Concat(settlement.HeroesWithoutParty.Where(h => h.IsLord))
                .Distinct()
                .ToList();

            if (!attackers.Any()) return;

            // Check bandits
            var attackerFaction = siegeEvent.BesiegerCamp.LeaderParty?.MapFaction;
            var defenderFaction = settlement.MapFaction;
            if (attackerFaction?.IsBanditFaction == true || defenderFaction?.IsBanditFaction == true) return;

            float attackerStrength = attackers.Sum(p => p.CalculateCurrentStrength());
            float defenderStrength = defenders.Sum(p => p.CalculateCurrentStrength());

            // If no defenders, they definitely surrender
            if (defenderStrength <= 0f) return;
            if (attackerStrength <= 0f) return;

            float powerRatio = attackerStrength / defenderStrength;

            var strongerLeaders = attackers
                .Select(p => p.MobileParty?.LeaderHero)
                .Where(h => h != null)
                .ToList();

            float traitEffect = 0f;
            if (defendingLords.Any())
            {
                float avgValor = (float)defendingLords.Average(h => h.GetTraitLevel(DefaultTraits.Valor));
                float avgHonor = (float)defendingLords.Average(h => h.GetTraitLevel(DefaultTraits.Honor));
                float avgCalculating = (float)defendingLords.Average(h => h.GetTraitLevel(DefaultTraits.Calculating));
                float avgMercy = (float)defendingLords.Average(h => h.GetTraitLevel(DefaultTraits.Mercy));

                traitEffect -= (avgValor / 10f) * (JgumSettingsManager.LordValorMultiplier / 100f);
                traitEffect -= (avgHonor / 20f) * (JgumSettingsManager.LordHonorMultiplier / 100f);
                traitEffect += (avgCalculating / 20f) * (JgumSettingsManager.LordCalculatingMultiplier / 100f);
                traitEffect += (avgMercy / 20f) * (JgumSettingsManager.LordMercyMultiplier / 100f);
            }

            if (strongerLeaders.Any())
            {
                float avgStrongerMercy = (float)strongerLeaders.Average(h => h!.GetTraitLevel(DefaultTraits.Mercy));
                traitEffect += (avgStrongerMercy / 10f) * (JgumSettingsManager.PlayerMercyMultiplier / 100f);
            }

            // Also consider siege pressure if we want. For now let's just stick to the original AiVsAi logic which was raw ratio + traits.
            float totalRatio = powerRatio + traitEffect;
            float baseThreshold = JgumSettingsManager.AiVsAiSiegeBaseSurrenderThreshold;
            float guaranteedThreshold = JgumSettingsManager.AiVsAiSiegeGuaranteedSurrenderThreshold;

            if (guaranteedThreshold < baseThreshold)
                guaranteedThreshold = baseThreshold;

            int mode = JgumSettingsManager.AiVsAiRandomnessMode;
            bool shouldSurrender = false;

            if (mode == 0) // Off
            {
                shouldSurrender = totalRatio >= baseThreshold;
            }
            else if (mode == 1) // Thresholded
            {
                if (totalRatio >= guaranteedThreshold)
                    shouldSurrender = true;
                else if (totalRatio >= baseThreshold)
                {
                    float range = guaranteedThreshold - baseThreshold;
                    float chance = range <= 0.001f ? 1f : (totalRatio - baseThreshold) / range;
                    shouldSurrender = MBRandom.RandomFloat <= chance;
                }
            }
            else // Unbound
            {
                if (totalRatio >= guaranteedThreshold)
                {
                    float extra = totalRatio - guaranteedThreshold;
                    float chance = Math.Min(1f, 0.95f + (extra * 0.01f));
                    shouldSurrender = MBRandom.RandomFloat <= chance;
                }
                else if (totalRatio < baseThreshold)
                {
                    float deficit = baseThreshold - totalRatio;
                    float chance = Math.Max(0f, 0.05f - (deficit * 0.01f));
                    shouldSurrender = MBRandom.RandomFloat <= chance;
                }
                else
                {
                    float range = guaranteedThreshold - baseThreshold;
                    if (range <= 0.001f)
                    {
                        shouldSurrender = MBRandom.RandomFloat <= 0.5f;
                    }
                    else
                    {
                        float normalized = (totalRatio - baseThreshold) / range;
                        float chance = 0.05f + (normalized * 0.90f);
                        shouldSurrender = MBRandom.RandomFloat <= chance;
                    }
                }
            }

            if (shouldSurrender)
            {
                Hero? winnerHero = siegeEvent.BesiegerCamp.LeaderParty?.LeaderHero;
                Hero? loserHero = settlement.OwnerClan?.Leader;

                if (winnerHero != null && loserHero != null)
                {
                    LogEntry.AddLogEntry(new AiSurrenderLogEntry(winnerHero, loserHero));

                    JgumInteropEvents.RaiseSurrenderResolved(new JgumSurrenderRecord
                    {
                        Kind = JgumSurrenderKind.SiegeAutoSurrender,
                        SettlementId = settlement.StringId,
                        SurrenderingHeroId = loserHero.StringId,
                        SurrenderingPartyName = settlement.Name?.ToString(),
                        WinnerHeroId = winnerHero.StringId,
                        WinnerClanId = winnerHero.Clan?.StringId,
                        LoserFactionId = loserHero.MapFaction?.StringId,
                        CampaignTimeDays = (float)CampaignTime.Now.ToDays,
                        AcceptedByPlayer = false,
                        Outcome = JgumSurrenderOutcome.Accepted
                    });
                }

                _dailySurrenderCount++;
                
                // Finalize siege
                if (winnerHero != null)
                {
                    JGUM.Actions.WarScoreHelper.RecordSiegeSurrenderWarScore(settlement, winnerHero);
                    ChangeOwnerOfSettlementAction.ApplyBySiege(winnerHero, winnerHero, settlement);
                }
                
                siegeEvent.FinalizeSiegeEvent();
            }
        }

        private static bool IsPlayerInvolvedInSiege(Settlement settlement)
        {
            var playerParty = MobileParty.MainParty?.Party;
            if (playerParty == null) return false;

            if (settlement.OwnerClan == Hero.MainHero?.Clan) return true;

            var siegeEvent = settlement.SiegeEvent;
            if (siegeEvent == null) return false;

            if (siegeEvent.BesiegerCamp.HasInvolvedPartyForEventType(playerParty)) return true;
            
            var defenderSide = siegeEvent.GetSiegeEventSide(BattleSideEnum.Defender);
            if (defenderSide != null && defenderSide.HasInvolvedPartyForEventType(playerParty)) return true;

            return false;
        }
    }
}
