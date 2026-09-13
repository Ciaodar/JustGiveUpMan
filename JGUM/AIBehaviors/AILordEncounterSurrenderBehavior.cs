using System;
using System.Collections.Generic;
using System.Linq;
using JGUM.Calculators;
using JGUM.Config;
using JGUM.Data;
using JGUM.Interop;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace JGUM.AIBehaviors
{
    public class AILordEncounterSurrenderBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.MapEventStarted.AddNonSerializedListener(this, OnMapEventStarted);
        }

        public override void SyncData(IDataStore dataStore)
        {
            // No data to sync for this behavior.
        }

        private void OnMapEventStarted(MapEvent mapEvent, PartyBase attackerParty, PartyBase defenderParty)
        {
            if (mapEvent == null) return;

            // Only apply to AI vs AI encounters.
            if (mapEvent.IsPlayerMapEvent) return;

            // Check if feature is enabled.
            if (!JgumSettingsManager.EnableAiVsAiFieldSurrender) return;

            // Only apply to field battles.
            if (!mapEvent.IsFieldBattle) return;

            // Do not apply if either side is a bandit faction.
            if (attackerParty?.MapFaction?.IsBanditFaction == true || defenderParty?.MapFaction?.IsBanditFaction == true) return;

            float attackerStrength = mapEvent.StrengthOfSide[(int)BattleSideEnum.Attacker];
            float defenderStrength = mapEvent.StrengthOfSide[(int)BattleSideEnum.Defender];

            if (JgumSettingsManager.EnableAiAreaControlRadius)
            {
                float radius = JgumSettingsManager.AiAreaControlRadius;
                attackerStrength += CalculateNearbyPartyStrength(mapEvent, attackerParty?.MapFaction, radius);
                defenderStrength += CalculateNearbyPartyStrength(mapEvent, defenderParty?.MapFaction, radius);
            }

            if (attackerStrength <= 0f && defenderStrength <= 0f) return;

            BattleSideEnum weakerSide;
            BattleSideEnum strongerSide;
            float weakerStrength;
            float strongerStrength;

            if (attackerStrength > defenderStrength)
            {
                strongerSide = BattleSideEnum.Attacker;
                weakerSide = BattleSideEnum.Defender;
                strongerStrength = attackerStrength;
                weakerStrength = defenderStrength;
            }
            else
            {
                strongerSide = BattleSideEnum.Defender;
                weakerSide = BattleSideEnum.Attacker;
                strongerStrength = defenderStrength;
                weakerStrength = attackerStrength;
            }

            if (weakerStrength <= 0f) return; // Cannot calculate ratio or already dead.

            float powerRatio = strongerStrength / weakerStrength;

            // Get leaders for trait calculation.
            var weakerLeaders = mapEvent.GetMapEventSide(weakerSide).Parties
                .Select(p => p.Party.LeaderHero)
                .Where(h => h != null)
                .ToList();

            var strongerLeaders = mapEvent.GetMapEventSide(strongerSide).Parties
                .Select(p => p.Party.LeaderHero)
                .Where(h => h != null)
                .ToList();

            // AI vs AI doesn't have a main hero, so we average the stronger side's traits for "PlayerMercy" equivalent,
            // or just use 0 since the AI might not be as merciful as the player.
            // Let's adapt LordSurrenderCalculator's logic directly.
            float traitEffect = 0f;
            if (weakerLeaders.Any())
            {
                float avgValor = (float)weakerLeaders.Average(h => h.GetTraitLevel(DefaultTraits.Valor));
                float avgHonor = (float)weakerLeaders.Average(h => h.GetTraitLevel(DefaultTraits.Honor));
                float avgCalculating = (float)weakerLeaders.Average(h => h.GetTraitLevel(DefaultTraits.Calculating));
                float avgMercy = (float)weakerLeaders.Average(h => h.GetTraitLevel(DefaultTraits.Mercy));

                traitEffect -= (avgValor / 10f) * (JgumSettingsManager.LordValorMultiplier / 100f);
                traitEffect -= (avgHonor / 20f) * (JgumSettingsManager.LordHonorMultiplier / 100f);
                traitEffect += (avgCalculating / 20f) * (JgumSettingsManager.LordCalculatingMultiplier / 100f);
                traitEffect += (avgMercy / 20f) * (JgumSettingsManager.LordMercyMultiplier / 100f);
            }

            if (strongerLeaders.Any())
            {
                float avgStrongerMercy = (float)strongerLeaders.Average(h => h.GetTraitLevel(DefaultTraits.Mercy));
                traitEffect += (avgStrongerMercy / 10f) * (JgumSettingsManager.PlayerMercyMultiplier / 100f);
            }

            float totalRatio = powerRatio + traitEffect;
            float baseThreshold = JgumSettingsManager.AiVsAiFieldBaseSurrenderThreshold;
            float guaranteedThreshold = JgumSettingsManager.AiVsAiFieldGuaranteedSurrenderThreshold;

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
                Hero? winnerHero = mapEvent.GetMapEventSide(strongerSide).LeaderParty?.LeaderHero;
                Hero? loserHero = mapEvent.GetMapEventSide(weakerSide).LeaderParty?.LeaderHero;

                if (winnerHero != null && loserHero != null)
                {
                    LogEntry.AddLogEntry(new AiSurrenderLogEntry(winnerHero, loserHero));
                    
                    JgumInteropEvents.RaiseSurrenderResolved(new JgumSurrenderRecord
                    {
                        Kind = JgumSurrenderKind.LordEncounter,
                        SurrenderingHeroId = loserHero.StringId,
                        SurrenderingPartyName = mapEvent.GetMapEventSide(weakerSide).LeaderParty?.Name?.ToString(),
                        WinnerHeroId = winnerHero.StringId,
                        WinnerClanId = winnerHero.Clan?.StringId,
                        LoserFactionId = loserHero.MapFaction?.StringId,
                        CampaignTimeDays = (float)CampaignTime.Now.ToDays,
                        AcceptedByPlayer = false,
                        Outcome = JgumSurrenderOutcome.Accepted
                    });
                }

                // Force the map event to end with the weaker side surrendering
                mapEvent.DoSurrender(weakerSide);
            }
        }

        private float CalculateNearbyPartyStrength(MapEvent mapEvent, IFaction? faction, float radius)
        {
            if (faction == null) return 0f;
            float extraStrength = 0f;

            var involvedParties = mapEvent.InvolvedParties.ToList();

            foreach (MobileParty party in MobileParty.All)
            {
                if (party.MapFaction != faction) continue;
                if (!party.IsLordParty) continue;
                if (party.CurrentSettlement != null) continue;
                if (involvedParties.Contains(party.Party)) continue;

                float dist = party.Position.Distance(mapEvent.Position);
                if (dist <= radius)
                {
                    float ratio = 1f - (dist / radius);
                    if (ratio < 0f) ratio = 0f;
                    
                    extraStrength += party.Party.CalculateCurrentStrength() * ratio;
                }
            }

            return extraStrength;
        }
    }
}

