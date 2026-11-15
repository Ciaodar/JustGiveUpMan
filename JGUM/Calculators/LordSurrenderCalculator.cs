using System.Collections.Generic;
using System.Linq;
using JGUM.Config;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace JGUM.Calculators
{
    public class LordSurrenderCalculator
    {
        // Multi-party encounter calculation used for PlayerEncounter scenarios.
        public bool ShouldEnemySurrenderInEncounter(
            IEnumerable<Hero>? enemyLeaders,
            float playerStrength,
            float enemyStrength)
        {
            if (playerStrength <= 0f)
                return false;


            if (enemyStrength <= 0)
                return true;

            // Power ratio: how much stronger is the player side in this encounter.
            float powerRatio = playerStrength / enemyStrength;

            var uniqueEnemyLeaders = enemyLeaders?
                .Where(h => h != null)
                .Distinct()
                .ToList() ?? new List<Hero>();

            if (uniqueEnemyLeaders.Any() && uniqueEnemyLeaders.All(h => h.Clan?.HasBloodFeudWithPlayer == true))
                return false;

            float traitEffect = 0f;
            if (uniqueEnemyLeaders.Any())
            {
                int bloodFeudCount = uniqueEnemyLeaders.Count(h => h.Clan?.HasBloodFeudWithPlayer == true);
                traitEffect -= bloodFeudCount * 1.5f;

                // Use average enemy leader traits to keep scaling stable in multi-lord encounters.
                float avgValor = (float)uniqueEnemyLeaders.Average(h => h.GetTraitLevel(DefaultTraits.Valor));
                float avgHonor = (float)uniqueEnemyLeaders.Average(h => h.GetTraitLevel(DefaultTraits.Honor));
                float avgCalculating = (float)uniqueEnemyLeaders.Average(h => h.GetTraitLevel(DefaultTraits.Calculating));
                float avgMercy = (float)uniqueEnemyLeaders.Average(h => h.GetTraitLevel(DefaultTraits.Mercy));

                traitEffect -= (avgValor / 10f) * (JgumSettingsManager.LordValorMultiplier / 100f);
                traitEffect -= (avgHonor / 20f) * (JgumSettingsManager.LordHonorMultiplier / 100f);
                traitEffect += (avgCalculating / 20f) * (JgumSettingsManager.LordCalculatingMultiplier / 100f);
                traitEffect += (avgMercy / 20f) * (JgumSettingsManager.LordMercyMultiplier / 100f);
            }

            var player = Hero.MainHero;
            traitEffect += (player.GetTraitLevel(DefaultTraits.Mercy) / 10f) * (JgumSettingsManager.PlayerMercyMultiplier / 100f);

            float totalRatio = powerRatio + traitEffect;
            float baseThreshold = JgumSettingsManager.BaseSurrenderThreshold / JgumSettingsManager.SurrenderTendencyMultiplier;
            float guaranteedThreshold = JgumSettingsManager.GuaranteedSurrenderThreshold / JgumSettingsManager.SurrenderTendencyMultiplier;
            
            if (guaranteedThreshold < baseThreshold)
                guaranteedThreshold = baseThreshold;

            int mode = JgumSettingsManager.SurrenderRandomnessMode;

            if (mode == 0) // Off
            {
                return totalRatio >= baseThreshold;
            }
            else if (mode == 1) // Thresholded
            {
                if (totalRatio >= guaranteedThreshold)
                    return true;
                if (totalRatio < baseThreshold)
                    return false;

                float range = guaranteedThreshold - baseThreshold;
                if (range <= 0.001f)
                    return true;

                float chance = (totalRatio - baseThreshold) / range;
                return TaleWorlds.Core.MBRandom.RandomFloat <= chance;
            }
            else // Unbound
            {
                if (totalRatio >= guaranteedThreshold)
                {
                    float extra = totalRatio - guaranteedThreshold;
                    float chance = 0.95f + (extra * 0.01f);
                    if (chance > 1f) chance = 1f;
                    return TaleWorlds.Core.MBRandom.RandomFloat <= chance;
                }
                else if (totalRatio < baseThreshold)
                {
                    float deficit = baseThreshold - totalRatio;
                    float chance = 0.05f - (deficit * 0.01f);
                    if (chance < 0f) chance = 0f;
                    return TaleWorlds.Core.MBRandom.RandomFloat <= chance;
                }
                else
                {
                    float range = guaranteedThreshold - baseThreshold;
                    if (range <= 0.001f)
                        return TaleWorlds.Core.MBRandom.RandomFloat <= 0.5f;

                    float normalized = (totalRatio - baseThreshold) / range;
                    float chance = 0.05f + (normalized * 0.90f);
                    return TaleWorlds.Core.MBRandom.RandomFloat <= chance;
                }
            }
        }

        // Get the best defender character for dialog
        public CharacterObject? GetEncounterDefenderCharacter(PartyBase enemyParty)
        {
            if (enemyParty.LeaderHero != null)
                return enemyParty.LeaderHero.CharacterObject;

            return null;
        }
    }
}
