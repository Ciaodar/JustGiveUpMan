using System.ComponentModel;
using JGUM.Config;
using JGUM.MCMBridge.Config;

namespace JGUM.MCMBridge
{
    public static class BridgeBootstrap
    {
        public static void TryRegister()
        {
            var mcmSettings = JgumMcmSettings.Instance;
            if (mcmSettings != null)
            {
                mcmSettings.PropertyChanged += OnMcmSettingsChanged;
            }

            JgumSettingsManager.RegisterExternalSettingsProvider(BuildModelFromMcm);
        }

        private static void OnMcmSettingsChanged(object sender, PropertyChangedEventArgs e)
        {
            JgumSettingsManager.Reload();
        }

        private static JgumJsonModel BuildModelFromMcm()
        {
            JgumMcmSettings mcm = JgumMcmSettings.Instance ?? new JgumMcmSettings();
            JgumJsonModel json = new JgumJsonModel
            {
                SurrenderTendencyMultiplier = mcm.SurrenderTendencyMultiplier,
                BaseSurrenderThreshold = mcm.BaseSurrenderThreshold,
                SurrenderRandomnessMode = mcm.SurrenderRandomnessMode.SelectedIndex,
                GuaranteedSurrenderThreshold = mcm.GuaranteedSurrenderThreshold,
                PlayerMercyMultiplier = mcm.PlayerMercyMultiplier,
                RequiredSurrenderCount = mcm.RequiredSurrenderCount,
                EnableSiegeSurrender = mcm.EnableSiegeSurrender,
                EnableSiegeStarvationSallyOut = mcm.EnableSiegeStarvationSallyOut,
                NearbyEnemyLordStrengthPercentage = mcm.NearbyEnemyLordStrengthPercentage,
                NearbyEnemyLordDetectionRange = mcm.NearbyEnemyLordDetectionRange,
                SiegeTowerEnginePressureMultiplier = mcm.SiegeTowerEnginePressureMultiplier,
                SiegeRamEnginePressureMultiplier = mcm.SiegeRamEnginePressureMultiplier,
                SiegeCatapultEnginePressureMultiplier = mcm.SiegeCatapultEnginePressureMultiplier,
                SiegeTrebuchetEnginePressureMultiplier = mcm.SiegeTrebuchetEnginePressureMultiplier,
                SiegeEnginePowerRatioMultiplier = mcm.SiegeEnginePowerRatioMultiplier,
                SiegeWallDamagePressureMultiplier = mcm.SiegeWallDamagePressureMultiplier,
                SiegeWallDestroyedBonus = mcm.SiegeWallDestroyedBonus,
                SiegeWallDamageCurveExponent = mcm.SiegeWallDamageCurveExponent,
                SiegeNegotiationEasyThreshold = mcm.SiegeNegotiationEasyThreshold,
                SiegeNegotiationNormalThreshold = mcm.SiegeNegotiationNormalThreshold,
                SiegeNegotiationHardThreshold = mcm.SiegeNegotiationHardThreshold,
                EnableLordSurrender = mcm.EnableLordSurrender,
                LordDialogPriority = mcm.LordDialogPriority,
                EnablePatrolSurrender = mcm.EnablePatrolSurrender,
                PatrolDialogPriority = mcm.PatrolDialogPriority,
                LordCalculatingMultiplier = mcm.LordCalculatingMultiplier,
                LordValorMultiplier = mcm.LordValorMultiplier,
                LordMercyMultiplier = mcm.LordMercyMultiplier,
                LordHonorMultiplier = mcm.LordHonorMultiplier,
                EnableFiefPurchaseOffer = mcm.EnableFiefPurchaseOffer,
                FiefPriceOfferMultiplier = mcm.FiefPriceOfferMultiplier,
                MinTradeSkillForFiefOffer = mcm.MinTradeSkillForFiefOffer,
                FiefPurchaseOfferDailyChance = mcm.FiefPurchaseOfferDailyChance,
                FiefPurchaseOfferCooldownDays = mcm.FiefPurchaseOfferCooldownDays,
                EnableAiVsAiFieldSurrender = mcm.EnableAiVsAiFieldSurrender,
                EnableAiVsAiSiegeSurrender = mcm.EnableAiVsAiSiegeSurrender,
                AiVsAiRandomnessMode = mcm.AiVsAiRandomnessMode.SelectedIndex,
                AiVsAiFieldBaseSurrenderThreshold = mcm.AiVsAiFieldBaseSurrenderThreshold,
                AiVsAiFieldGuaranteedSurrenderThreshold = mcm.AiVsAiFieldGuaranteedSurrenderThreshold,
                AiVsAiSiegeBaseSurrenderThreshold = mcm.AiVsAiSiegeBaseSurrenderThreshold,
                AiVsAiSiegeGuaranteedSurrenderThreshold = mcm.AiVsAiSiegeGuaranteedSurrenderThreshold,
                AiVsAiSiegeDailySurrenderLimit = mcm.AiVsAiSiegeDailySurrenderLimit,
                EnableAiAreaControlRadius = mcm.EnableAiAreaControlRadius,
                AiAreaControlRadius = mcm.AiAreaControlRadius,
                EnableVoluntarySurrender = mcm.EnableVoluntarySurrender,
                VoluntarySurrenderBaseChance = mcm.VoluntarySurrenderBaseChance,
                VoluntarySurrenderHonorPenalty = mcm.VoluntarySurrenderHonorPenalty,
                VoluntarySurrenderValorPenalty = mcm.VoluntarySurrenderValorPenalty,
                VoluntarySurrenderMercyReward = mcm.VoluntarySurrenderMercyReward,
                VoluntarySurrenderCalculatingReward = mcm.VoluntarySurrenderCalculatingReward
            };
#if !DEBUG
            json.SiegeNegotiationDifficultyPreset = mcm.SiegeNegotiationDifficultyPreset.SelectedIndex;
#endif
            return json;
        }
    }
}
