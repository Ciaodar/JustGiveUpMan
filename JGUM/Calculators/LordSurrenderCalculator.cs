using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using JGUM.Config;
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

            float traitEffect = 0f;
            if (uniqueEnemyLeaders.Any())
            {
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
            float threshold = JgumSettingsManager.BaseSurrenderThreshold / JgumSettingsManager.SurrenderTendencyMultiplier;

            return totalRatio > threshold;
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
