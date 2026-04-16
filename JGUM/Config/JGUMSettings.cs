#if USE_MCM
using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;

namespace JGUM.Config
{
    public class JgumMcmSettings : AttributeGlobalSettings<JgumMcmSettings>
    {
        public override string Id => "JGUMSettings";
        public override string DisplayName => "Just Give Up Man";
        public override string FolderName => "JGUM";
        public override string FormatType => "json";

        [SettingPropertyFloatingInteger("{=JGUM.Settings.SurrenderTendencyMultiplier.Name}General Surrender Tendency", 0f, 2f, "0", Order = 3, RequireRestart = false, HintText = "{=JGUM.Settings.SurrenderTendencyMultiplier.Hint}Overall multiplier for the surrender chance. Higher values make surrenders more likely.")]
        [SettingPropertyGroup("{=JGUM.Settings.SiegeSettings.Name}Siege Settings")]
        public float SurrenderTendencyMultiplier { get; set; } = 1f;

        [SettingPropertyFloatingInteger("{=JGUM.Settings.BaseSurrenderThreshold.Name}Base Surrender Threshold", 0f, 10f, "0.0", Order = 0, RequireRestart = false, HintText = "{=JGUM.Settings.BaseSurrenderThreshold.Hint}Base value for the surrender calculation. Higher values make surrenders less likely. Default is 2.5.")]
        [SettingPropertyGroup("{=JGUM.Settings.SiegeSettings.Name}Siege Settings")]
        public float BaseSurrenderThreshold { get; set; } = 2.5f;

        [SettingPropertyFloatingInteger("{=JGUM.Settings.PlayerMercyMultiplier.Name}Player Mercy Multiplier", 0f, 200f, "0", Order = 1, RequireRestart = false, HintText = "{=JGUM.Settings.PlayerMercyMultiplier.Hint}Multiplier for the player's Mercy/Cruelty trait effect. 100 is default. 0 disables it. 200 doubles it.")]
        [SettingPropertyGroup("{=JGUM.Settings.SiegeSettings.Name}Siege Settings")]
        public float PlayerMercyMultiplier { get; set; } = 100f;

        [SettingPropertyFloatingInteger("{=JGUM.Settings.LordCalculatingMultiplier.Name}Calculating Trait Multiplier", 0f, 200f, "0", Order = 2, RequireRestart = false, HintText = "{=JGUM.Settings.LordCalculatingMultiplier.Hint}Multiplier for the defending lords' 'Calculating' trait. 100 is default.")]
        [SettingPropertyGroup("{=JGUM.Settings.SiegeSettings.Name}Siege Settings")]
        public float LordCalculatingMultiplier { get; set; } = 100f;

        [SettingPropertyFloatingInteger("{=JGUM.Settings.LordValorMultiplier.Name}Valor Trait Multiplier", 0f, 200f, "0", Order = 6, RequireRestart = false, HintText = "{=JGUM.Settings.LordValorMultiplier.Hint}Multiplier for the defending lords' 'Valor' trait. 100 is default.")]
        [SettingPropertyGroup("{=JGUM.Settings.SiegeSettings.Name}Siege Settings")]
        public float LordValorMultiplier { get; set; } = 100f;

        [SettingPropertyFloatingInteger("{=JGUM.Settings.LordMercyMultiplier.Name}Mercy Trait Multiplier", 0f, 200f, "0", Order = 4, RequireRestart = false, HintText = "{=JGUM.Settings.LordMercyMultiplier.Hint}Multiplier for the defending lords' 'Mercy' trait. 100 is default.")]
        [SettingPropertyGroup("{=JGUM.Settings.SiegeSettings.Name}Siege Settings")]
        public float LordMercyMultiplier { get; set; } = 100f;

        [SettingPropertyFloatingInteger("{=JGUM.Settings.LordHonorMultiplier.Name}Honor Trait Multiplier", 0f, 200f, "0", Order = 5, RequireRestart = false, HintText = "{=JGUM.Settings.LordHonorMultiplier.Hint}Multiplier for the defending lords' 'Honor' trait. 100 is default.")]
        [SettingPropertyGroup("{=JGUM.Settings.SiegeSettings.Name}Siege Settings")]
        public float LordHonorMultiplier { get; set; } = 100f;

        [SettingPropertyFloatingInteger("{=JGUM.Settings.NearbyEnemyLordStrengthPercentage.Name}Nearby Enemy Lord Strength (%)", 0f, 100f, "0", Order = 7, RequireRestart = false, HintText = "{=JGUM.Settings.NearbyEnemyLordStrengthPercentage.Hint}Percentage of nearby enemy lord strength to add to defender power. Higher values make defenders see enemy lords as threatening. Default 50%.")]
        [SettingPropertyGroup("{=JGUM.Settings.SiegeSettings.Name}Siege Settings")]
        public float NearbyEnemyLordStrengthPercentage { get; set; } = 50f;

        [SettingPropertyInteger("Required Surrender Count", 1, 10, Order = 8, RequireRestart = false, HintText = "How many accepted surrenders are needed before mercy trait gain is applied.")]
        [SettingPropertyGroup("{=JGUM.Settings.SiegeSettings.Name}Siege Settings")]
        public int RequiredSurrenderCount { get; set; } = 3;
    }
}
#endif
