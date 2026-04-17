using System;
using System.Collections.Generic;
using System.IO;
using JGUM.Calculators;
using Newtonsoft.Json;
using TaleWorlds.Library;

namespace JGUM.Config
{
    public static class JgumSettingsManager
    {
#if USE_MCM
        private static JgumMcmSettings Settings => JgumMcmSettings.Instance!;
#else
        private static readonly object SyncRoot = new object();
        private static JgumJsonModel _settings = new JgumJsonModel();
        private const string ConfigFileName = "config.json";
#endif

        
        public static float SurrenderTendencyMultiplier => SettingsOrDefault().SurrenderTendencyMultiplier;
        public static float BaseSurrenderThreshold => SettingsOrDefault().BaseSurrenderThreshold;
        public static float PlayerMercyMultiplier => SettingsOrDefault().PlayerMercyMultiplier;
        public static float LordCalculatingMultiplier => SettingsOrDefault().LordCalculatingMultiplier;
        public static float LordValorMultiplier => SettingsOrDefault().LordValorMultiplier;
        public static float LordMercyMultiplier => SettingsOrDefault().LordMercyMultiplier;
        public static float LordHonorMultiplier => SettingsOrDefault().LordHonorMultiplier;
        public static float NearbyEnemyLordStrengthPercentage => SettingsOrDefault().NearbyEnemyLordStrengthPercentage;
        public static int RequiredSurrenderCount => SettingsOrDefault().RequiredSurrenderCount;
        public static bool EnableSiegeSurrender => SettingsOrDefault().EnableSiegeSurrender;
        public static bool EnableSiegeStarvationSallyOut => SettingsOrDefault().EnableSiegeStarvationSallyOut;
        public static bool EnableLordSurrender => SettingsOrDefault().EnableLordSurrender;
        public static bool EnablePatrolSurrender => SettingsOrDefault().EnablePatrolSurrender;
        public static float NearbyEnemyLordDetectionRange => SettingsOrDefault().NearbyEnemyLordDetectionRange;
        public static int LordDialogPriority => SettingsOrDefault().LordDialogPriority;
        public static int PatrolDialogPriority => SettingsOrDefault().PatrolDialogPriority;

        public static void Initialize()
        {
#if USE_MCM
            _ = JgumMcmSettings.Instance;
#else
            Reload();
#endif
        }

        public static bool Reload()
        {
#if USE_MCM
            _ = JgumMcmSettings.Instance;
            return true;
#else
            lock (SyncRoot)
            {
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
                        // Ignored intentionally: manager falls back to in-memory defaults.
                    }

                    return false;
                }
            }
#endif
        }

#if USE_MCM
        private static JgumJsonModel SettingsOrDefault()
        {
            var mcm = Settings;
            return new JgumJsonModel
            {
                SurrenderTendencyMultiplier = mcm.SurrenderTendencyMultiplier,
                BaseSurrenderThreshold = mcm.BaseSurrenderThreshold,
                PlayerMercyMultiplier = mcm.PlayerMercyMultiplier,
                RequiredSurrenderCount = mcm.RequiredSurrenderCount,
                EnableSiegeSurrender = mcm.EnableSiegeSurrender,
                EnableSiegeStarvationSallyOut = mcm.EnableSiegeStarvationSallyOut,
                NearbyEnemyLordStrengthPercentage = mcm.NearbyEnemyLordStrengthPercentage,
                NearbyEnemyLordDetectionRange = mcm.NearbyEnemyLordDetectionRange,
                EnableLordSurrender = mcm.EnableLordSurrender,
                LordDialogPriority = mcm.LordDialogPriority,
                EnablePatrolSurrender = mcm.EnablePatrolSurrender,
                PatrolDialogPriority = mcm.PatrolDialogPriority,
                LordCalculatingMultiplier = mcm.LordCalculatingMultiplier,
                LordValorMultiplier = mcm.LordValorMultiplier,
                LordMercyMultiplier = mcm.LordMercyMultiplier,
                LordHonorMultiplier = mcm.LordHonorMultiplier
            };
        }
#else
        private static JgumJsonModel SettingsOrDefault()
        {
            return _settings;
        }

        private static string GetConfigPath()
        {
            // Prefer module-local config for portability, then fallback to user documents.
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
                    // Fallback below.
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
            var ok = Reload();
            return ok ? "JGUM config reloaded." : "JGUM config reload failed, defaults applied.";
        }
#endif
    }
}
