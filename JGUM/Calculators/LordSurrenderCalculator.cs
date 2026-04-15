using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using JGUM.Config;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.Core;

namespace JGUM.Calculators
{
    public class LordSurrenderCalculator
    {
        // Backward-compatible wrapper for old call sites.
        // Check if enemy should surrender during field encounter.


        // Multi-party encounter calculation used for PlayerEncounter scenarios.
        public bool ShouldEnemySurrenderInEncounter(
            IEnumerable<Hero>? enemyLeaders)
        {
            float enemyStrength = PlayerEncounter.Battle.StrengthOfSide[(int)PartyBase.MainParty.OpponentSide];
            float playerStrength = PlayerEncounter.Battle.StrengthOfSide[(int)PartyBase.MainParty.Side];
            // Calculate current strength of both parties.

           

            if (enemyStrength <= 0)
                return true;

            // Power ratio: how much stronger is the player.
            float powerRatio = playerStrength / enemyStrength;
            

            var uniqueEnemyLeaders = enemyLeaders?
                .Distinct()
                .ToList() ?? new List<Hero>();

            if (!uniqueEnemyLeaders.Any())
                return false;

            // Calculate trait effects
            float traitEffect = 0f;

            // Use average enemy leader traits to keep scaling stable in multi-lord encounters.
            float avgValor = (float)uniqueEnemyLeaders.Average(h => h.GetTraitLevel(DefaultTraits.Valor));
            float avgHonor = (float)uniqueEnemyLeaders.Average(h => h.GetTraitLevel(DefaultTraits.Honor));
            float avgCalculating = (float)uniqueEnemyLeaders.Average(h => h.GetTraitLevel(DefaultTraits.Calculating));
            float avgMercy = (float)uniqueEnemyLeaders.Average(h => h.GetTraitLevel(DefaultTraits.Mercy));

            // Enemy traits make them more stubborn.
            traitEffect -= (avgValor / 10f) * (JGUMSettings.Instance!.LordValorMultiplier / 100f);
            traitEffect -= (avgHonor / 20f) * (JGUMSettings.Instance.LordHonorMultiplier / 100f);
            traitEffect += (avgCalculating / 20f) * (JGUMSettings.Instance.LordCalculatingMultiplier / 100f);
            traitEffect += (avgMercy / 20f) * (JGUMSettings.Instance.LordMercyMultiplier / 100f);

            // Player mercy affects surrender chance
            var player = Hero.MainHero;
            traitEffect += (player.GetTraitLevel(DefaultTraits.Mercy) / 10f) * (JGUMSettings.Instance.PlayerMercyMultiplier / 100f);

            // Formula: (Power Ratio + Morale Ratio) + Trait Effect > Base Surrender Threshold * Config Tendency
            float totalRatio = (powerRatio) + traitEffect;
            float threshold = JGUMSettings.Instance.BaseSurrenderThreshold / JGUMSettings.Instance.SurrenderTendencyMultiplier;

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

