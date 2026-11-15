using System;
using JGUM.Config;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace JGUM.Calculators
{
    public static class SiegePressureCalculator
    {
        private const float BaseAttackerTowerEngineWeight = 0.12f;
        private const float BaseAttackerRamEngineWeight = 0.14f;
        private const float BaseAttackerCatapultEngineWeight = 0.1f;
        private const float BaseAttackerTrebuchetEngineWeight = 0.12f;

        private const float BaseDefenderTowerEngineWeight = 0.16f;
        private const float BaseDefenderRamEngineWeight = 0.18f;
        private const float BaseDefenderCatapultEngineWeight = 0.14f;
        private const float BaseDefenderTrebuchetEngineWeight = 0.16f;

        private const float EngineRatioBaseline = 1f;
        private const float BaseWallDamageWeight = 1.0f;

        private const float ActiveNavalBlockadePressureBonus = 0.25f;
        private const float UnblockadedPortPressureRelief = -0.15f;

        public static float GetSiegePressureModifier(Settlement? settlement)
        {
            var siegeEvent = settlement?.SiegeEvent;
            if (siegeEvent?.BesiegerCamp == null)
                return 0f;

            float attackerEnginePressure = GetSideSiegeEnginePressure(siegeEvent.BesiegerCamp.SiegeEngines, false);
            float defenderEnginePressure = GetSideSiegeEnginePressure(settlement?.SiegeEngines, true);
            float enginePowerRatio = (attackerEnginePressure + EngineRatioBaseline) / (defenderEnginePressure + EngineRatioBaseline);
            float enginePowerRatioEffect = (enginePowerRatio - 1f) * (JgumSettingsManager.SiegeEnginePowerRatioMultiplier / 100f);

            return enginePowerRatioEffect + GetWallDamagePressure(settlement) + GetNavalBlockadePressure(settlement);
        }

        private static float GetNavalBlockadePressure(Settlement? settlement)
        {
            if (settlement == null || !settlement.HasPort || settlement.SiegeEvent == null)
                return 0f;

            return settlement.SiegeEvent.IsBlockadeActive ? ActiveNavalBlockadePressureBonus : UnblockadedPortPressureRelief;
        }

        private static float GetSideSiegeEnginePressure(TaleWorlds.CampaignSystem.Siege.SiegeEvent.SiegeEnginesContainer? siegeEngines, bool isDefenderSide)
        {
            if (siegeEngines == null)
                return 0f;

            float towerWeightMultiplier = JgumSettingsManager.SiegeTowerEnginePressureMultiplier / 100f;
            float ramWeightMultiplier = JgumSettingsManager.SiegeRamEnginePressureMultiplier / 100f;
            float catapultWeightMultiplier = JgumSettingsManager.SiegeCatapultEnginePressureMultiplier / 100f;
            float trebuchetWeightMultiplier = JgumSettingsManager.SiegeTrebuchetEnginePressureMultiplier / 100f;
            float pressure = 0f;

            foreach (var engine in siegeEngines.AllSiegeEngines())
            {
                if (engine == null)
                    continue;

                float readiness = engine.IsConstructed ? 1f : Clamp01(engine.Progress);
                if (readiness <= 0f)
                    continue;

                float baseWeight = GetEngineWeight(engine.SiegeEngine,
                    towerWeightMultiplier, ramWeightMultiplier, catapultWeightMultiplier, trebuchetWeightMultiplier, isDefenderSide);

                pressure += baseWeight * readiness;
            }

            return pressure;
        }

        private static float GetEngineWeight(
            SiegeEngineType? siegeEngineType,
            float towerWeightMultiplier,
            float ramWeightMultiplier,
            float catapultWeightMultiplier,
            float trebuchetWeightMultiplier,
            bool isDefenderSide)
        {
            if (siegeEngineType == null)
                return 0f;

            if (siegeEngineType == DefaultSiegeEngineTypes.Preparations || siegeEngineType == DefaultSiegeEngineTypes.Ladder)
                return 0f;

            if (siegeEngineType == DefaultSiegeEngineTypes.SiegeTower || siegeEngineType == DefaultSiegeEngineTypes.HeavySiegeTower)
                return (isDefenderSide ? BaseDefenderTowerEngineWeight : BaseAttackerTowerEngineWeight) * towerWeightMultiplier;

            if (siegeEngineType == DefaultSiegeEngineTypes.Ram || siegeEngineType == DefaultSiegeEngineTypes.ImprovedRam)
                return (isDefenderSide ? BaseDefenderRamEngineWeight : BaseAttackerRamEngineWeight) * ramWeightMultiplier;

            if (siegeEngineType == DefaultSiegeEngineTypes.Catapult ||
                siegeEngineType == DefaultSiegeEngineTypes.FireCatapult ||
                siegeEngineType == DefaultSiegeEngineTypes.Onager ||
                siegeEngineType == DefaultSiegeEngineTypes.FireOnager ||
                siegeEngineType == DefaultSiegeEngineTypes.Ballista ||
                siegeEngineType == DefaultSiegeEngineTypes.FireBallista ||
                siegeEngineType == DefaultSiegeEngineTypes.Bricole)
            {
                return (isDefenderSide ? BaseDefenderCatapultEngineWeight : BaseAttackerCatapultEngineWeight) * catapultWeightMultiplier;
            }

            if (siegeEngineType == DefaultSiegeEngineTypes.Trebuchet || siegeEngineType == DefaultSiegeEngineTypes.FireTrebuchet)
                return (isDefenderSide ? BaseDefenderTrebuchetEngineWeight : BaseAttackerTrebuchetEngineWeight) * trebuchetWeightMultiplier;

            return siegeEngineType.IsRanged
                ? (isDefenderSide ? BaseDefenderCatapultEngineWeight : BaseAttackerCatapultEngineWeight) * catapultWeightMultiplier
                : (isDefenderSide ? BaseDefenderRamEngineWeight : BaseAttackerRamEngineWeight) * ramWeightMultiplier;
        }

        private static float GetWallDamagePressure(Settlement? settlement)
        {
            var wallRatios = settlement?.SettlementWallSectionHitPointsRatioList;
            if (wallRatios == null)
                return 0f;

            float wallDamageMultiplier = JgumSettingsManager.SiegeWallDamagePressureMultiplier / 100f;
            float destroyedWallBonus = JgumSettingsManager.SiegeWallDestroyedBonus;
            float damageCurveExponent = Math.Max(1f, JgumSettingsManager.SiegeWallDamageCurveExponent);

            float totalDamage = 0f;
            int wallSectionCount = 0;
            int destroyedWallCount = 0;

            foreach (float ratio in wallRatios)
            {
                wallSectionCount++;
                float clampedRatio = Clamp01(ratio);
                float damage = 1f - clampedRatio;
                totalDamage += (float)Math.Pow(damage, damageCurveExponent);

                if (clampedRatio <= 0f)
                    destroyedWallCount++;
            }

            if (wallSectionCount <= 0)
                return 0f;

            float softenedDamagePressure = (totalDamage / wallSectionCount) * (BaseWallDamageWeight * wallDamageMultiplier);
            float destroyedWallPressure = destroyedWallCount * destroyedWallBonus;

            return softenedDamagePressure + destroyedWallPressure;
        }

        private static float Clamp01(float value)
        {
            if (value < 0f)
                return 0f;

            if (value > 1f)
                return 1f;

            return value;
        }
    }
}




