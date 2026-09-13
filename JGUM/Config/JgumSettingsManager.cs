using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using TaleWorlds.Library;

namespace JGUM.Config
{
    public static class JgumSettingsManager
    {
        private static readonly object SyncRoot = new object();
        private static JgumJsonModel _settings = new JgumJsonModel();
        private static Func<JgumJsonModel>? _externalSettingsProvider;
        private const string ConfigFileName = "config.json";

        public static float SurrenderTendencyMultiplier => SettingsOrDefault().SurrenderTendencyMultiplier;
        public static float BaseSurrenderThreshold => SettingsOrDefault().BaseSurrenderThreshold;
        public static int SurrenderRandomnessMode => SettingsOrDefault().SurrenderRandomnessMode;
        public static float GuaranteedSurrenderThreshold => SettingsOrDefault().GuaranteedSurrenderThreshold;
        public static float PlayerMercyMultiplier => SettingsOrDefault().PlayerMercyMultiplier;
        public static float LordCalculatingMultiplier => SettingsOrDefault().LordCalculatingMultiplier;
        public static float LordValorMultiplier => SettingsOrDefault().LordValorMultiplier;
        public static float LordMercyMultiplier => SettingsOrDefault().LordMercyMultiplier;
        public static float LordHonorMultiplier => SettingsOrDefault().LordHonorMultiplier;
        public static float NearbyEnemyLordStrengthPercentage => SettingsOrDefault().NearbyEnemyLordStrengthPercentage;
        public static float SiegeNegotiationEasyThreshold => SettingsOrDefault().SiegeNegotiationEasyThreshold;
        public static float SiegeNegotiationNormalThreshold => SettingsOrDefault().SiegeNegotiationNormalThreshold;
        public static float SiegeNegotiationHardThreshold => SettingsOrDefault().SiegeNegotiationHardThreshold;
        public static int SiegeNegotiationDifficultyPreset => SettingsOrDefault().SiegeNegotiationDifficultyPreset;
        public static int RequiredSurrenderCount => SettingsOrDefault().RequiredSurrenderCount;
        public static bool EnableSiegeSurrender => SettingsOrDefault().EnableSiegeSurrender;
        public static bool EnableSiegeStarvationSallyOut => SettingsOrDefault().EnableSiegeStarvationSallyOut;
        public static bool EnableLordSurrender => SettingsOrDefault().EnableLordSurrender;
        public static bool EnablePatrolSurrender => SettingsOrDefault().EnablePatrolSurrender;
        public static float NearbyEnemyLordDetectionRange => SettingsOrDefault().NearbyEnemyLordDetectionRange;
        public static float SiegeTowerEnginePressureMultiplier => SettingsOrDefault().SiegeTowerEnginePressureMultiplier;
        public static float SiegeRamEnginePressureMultiplier => SettingsOrDefault().SiegeRamEnginePressureMultiplier;
        public static float SiegeCatapultEnginePressureMultiplier => SettingsOrDefault().SiegeCatapultEnginePressureMultiplier;
        public static float SiegeTrebuchetEnginePressureMultiplier => SettingsOrDefault().SiegeTrebuchetEnginePressureMultiplier;
        public static float SiegeEnginePowerRatioMultiplier => SettingsOrDefault().SiegeEnginePowerRatioMultiplier;
        public static float SiegeWallDamagePressureMultiplier => SettingsOrDefault().SiegeWallDamagePressureMultiplier;
        public static float SiegeWallDestroyedBonus => SettingsOrDefault().SiegeWallDestroyedBonus;
        public static float SiegeWallDamageCurveExponent => SettingsOrDefault().SiegeWallDamageCurveExponent;
        public static int LordDialogPriority => SettingsOrDefault().LordDialogPriority;
        public static int PatrolDialogPriority => SettingsOrDefault().PatrolDialogPriority;

        public static bool EnableFiefPurchaseOffer => SettingsOrDefault().EnableFiefPurchaseOffer;
        public static float FiefPriceOfferMultiplier => SettingsOrDefault().FiefPriceOfferMultiplier;
        public static int MinTradeSkillForFiefOffer => SettingsOrDefault().MinTradeSkillForFiefOffer;
        public static float FiefPurchaseOfferDailyChance => SettingsOrDefault().FiefPurchaseOfferDailyChance;
        public static int FiefPurchaseOfferCooldownDays => SettingsOrDefault().FiefPurchaseOfferCooldownDays;

        public static bool EnableAiVsAiFieldSurrender => SettingsOrDefault().EnableAiVsAiFieldSurrender;
        public static bool EnableAiVsAiSiegeSurrender => SettingsOrDefault().EnableAiVsAiSiegeSurrender;
        public static int AiVsAiRandomnessMode => SettingsOrDefault().AiVsAiRandomnessMode;
        public static float AiVsAiFieldBaseSurrenderThreshold => SettingsOrDefault().AiVsAiFieldBaseSurrenderThreshold;
        public static float AiVsAiFieldGuaranteedSurrenderThreshold => SettingsOrDefault().AiVsAiFieldGuaranteedSurrenderThreshold;
        public static float AiVsAiSiegeBaseSurrenderThreshold => SettingsOrDefault().AiVsAiSiegeBaseSurrenderThreshold;
        public static float AiVsAiSiegeGuaranteedSurrenderThreshold => SettingsOrDefault().AiVsAiSiegeGuaranteedSurrenderThreshold;
        public static int AiVsAiSiegeDailySurrenderLimit => SettingsOrDefault().AiVsAiSiegeDailySurrenderLimit;
        public static bool EnableAiAreaControlRadius => SettingsOrDefault().EnableAiAreaControlRadius;
        public static float AiAreaControlRadius => SettingsOrDefault().AiAreaControlRadius;

        public static bool EnableVoluntarySurrender => SettingsOrDefault().EnableVoluntarySurrender;
        public static int VoluntarySurrenderBaseChance => SettingsOrDefault().VoluntarySurrenderBaseChance;
        public static int VoluntarySurrenderHonorPenalty => SettingsOrDefault().VoluntarySurrenderHonorPenalty;
        public static int VoluntarySurrenderValorPenalty => SettingsOrDefault().VoluntarySurrenderValorPenalty;
        public static int VoluntarySurrenderMercyReward => SettingsOrDefault().VoluntarySurrenderMercyReward;
        public static int VoluntarySurrenderCalculatingReward => SettingsOrDefault().VoluntarySurrenderCalculatingReward;

        public static void Initialize()
        {
            Reload();
        }

        public static void TriggerClearDataEvent()
        {
            if (TaleWorlds.CampaignSystem.Campaign.Current != null)
            {
                TaleWorlds.CampaignSystem.Campaign.Current.GetCampaignBehavior<JGUM.Behaviors.LordEncounterSurrenderBehavior>()?.ClearAllData();
                TaleWorlds.CampaignSystem.Campaign.Current.GetCampaignBehavior<JGUM.Behaviors.SiegeNegotiationBehavior.SiegeNegotiationBehavior>()?.ClearAllData();
                TaleWorlds.CampaignSystem.Campaign.Current.GetCampaignBehavior<JGUM.Behaviors.VoluntarySurrenderBehavior>()?.ClearAllData();
                TaleWorlds.CampaignSystem.Campaign.Current.GetCampaignBehavior<JGUM.Behaviors.FiefPurchaseOfferBehavior>()?.ClearAllData();
                TaleWorlds.CampaignSystem.Campaign.Current.GetCampaignBehavior<JGUM.AIBehaviors.AISiegeSurrenderBehavior>()?.ClearAllData();

                TaleWorlds.Library.InformationManager.DisplayMessage(
                    new TaleWorlds.Library.InformationMessage("JGUM Data Cleared!", TaleWorlds.Library.Colors.Green));
            }
        }

        public static void RegisterExternalSettingsProvider(Func<JgumJsonModel> provider)
        {
            lock (SyncRoot)
            {
                _externalSettingsProvider = provider;
                Reload();
            }
        }

        public static bool Reload()
        {
            lock (SyncRoot)
            {
                if (_externalSettingsProvider != null)
                {
                    try
                    {
                        JgumJsonModel model = _externalSettingsProvider();
                        _settings = model ?? new JgumJsonModel();
                        return true;
                    }
                    catch
                    {
                        // Bridge provider failed; continue with JSON fallback.
                    }
                }

                var defaults = new JgumJsonModel();
                var configPath = GetConfigPath();

                try
                {
                    if (!File.Exists(configPath))
                    {
                        _settings = defaults;
                        SaveInternal(configPath, _settings);
                        return true;
                    }

                    var json = File.ReadAllText(configPath);
                    var loaded = JsonConvert.DeserializeObject<JgumJsonModel>(json);
                    _settings = loaded ?? defaults;
                    SaveInternal(configPath, _settings);
                    return true;
                }
                catch
                {
                    _settings = defaults;

                    try
                    {
                        SaveInternal(configPath, _settings);
                    }
                    catch
                    {
                        // Keep in-memory defaults when write fails.
                    }

                    return false;
                }
            }
        }

        private static JgumJsonModel SettingsOrDefault() => _settings;

        private static string GetConfigPath()
        {
            var gameRoot = BasePath.Name;
            if (!string.IsNullOrWhiteSpace(gameRoot))
            {
                var moduleConfig = Path.Combine(gameRoot, "Modules", "JGUM", ConfigFileName);
                if (File.Exists(moduleConfig))
                    return moduleConfig;

                try
                {
                    var moduleDir = Path.GetDirectoryName(moduleConfig);
                    if (!string.IsNullOrWhiteSpace(moduleDir))
                    {
                        Directory.CreateDirectory(moduleDir);
                        return moduleConfig;
                    }
                }
                catch
                {
                    // Fall back to documents path.
                }
            }

            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var fallbackDir = Path.Combine(documents, "Mount and Blade II Bannerlord", "Configs", "JGUM");
            Directory.CreateDirectory(fallbackDir);
            return Path.Combine(fallbackDir, ConfigFileName);
        }

        private static void SaveInternal(string path, JgumJsonModel model)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var json = JsonConvert.SerializeObject(model, Formatting.Indented);
            File.WriteAllText(path, json);
        }

        [CommandLineFunctionality.CommandLineArgumentFunction("reload_config", "jgum")]
        public static string ReloadConfigCommand(List<string> args)
        {
            bool ok = Reload();
            return ok ? "JGUM config reloaded." : "JGUM config reload failed, defaults applied.";
        }
    }
}
