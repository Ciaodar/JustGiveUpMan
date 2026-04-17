#if USE_MCM
using JGUM.Calculators;
using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;

namespace JGUM.Config
{
    public class JgumMcmSettings : AttributeGlobalSettings<JgumMcmSettings>
    {
        public override string Id => "JGUMSettings";
        public override string DisplayName => StringCalculator.GetString("JGUM.Settings.DisplayName", "Just Give Up Man!");

        public override string FolderName => "JGUM";
        public override string FormatType => "json";

        [SettingPropertyFloatingInteger("{=JGUM.Settings.SurrenderTendencyMultiplier.Name}General Surrender Tendency", 0f, 2f, "0.00", Order = 0, RequireRestart = false, HintText = "{=JGUM.Settings.SurrenderTendencyMultiplier.Hint}Overall multiplier for surrender checks.")]
        [SettingPropertyGroup("{=JGUM.Settings.Group.Common}Common")]
        public float SurrenderTendencyMultiplier { get; set; } = 1f;

        [SettingPropertyFloatingInteger("{=JGUM.Settings.BaseSurrenderThreshold.Name}Base Surrender Threshold", 0f, 10f, "0.00", Order = 1, RequireRestart = false, HintText = "{=JGUM.Settings.BaseSurrenderThreshold.Hint}Higher values make surrender harder.")]
        [SettingPropertyGroup("{=JGUM.Settings.Group.Common}Common")]
        public float BaseSurrenderThreshold { get; set; } = 2.5f;

        [SettingPropertyFloatingInteger("{=JGUM.Settings.PlayerMercyMultiplier.Name}Player Mercy Multiplier", 0f, 200f, "0", Order = 2, RequireRestart = false, HintText = "{=JGUM.Settings.PlayerMercyMultiplier.Hint}Scales player Mercy/Cruelty effect.")]
        [SettingPropertyGroup("{=JGUM.Settings.Group.Common}Common")]
        public float PlayerMercyMultiplier { get; set; } = 100f;

        [SettingPropertyInteger("{=JGUM.Settings.RequiredSurrenderCount.Name}Required Surrender Count", 1, 10, Order = 3, RequireRestart = false, HintText = "{=JGUM.Settings.RequiredSurrenderCount.Hint}Accepted surrender count before mercy trait gain.")]
        [SettingPropertyGroup("{=JGUM.Settings.Group.Common}Common")]
        public int RequiredSurrenderCount { get; set; } = 3;

        [SettingPropertyBool("{=JGUM.Settings.EnableSiegeSurrender.Name}Enable Siege Surrender", Order = 0, RequireRestart = false, HintText = "{=JGUM.Settings.EnableSiegeSurrender.Hint}Enable settlement surrender checks during sieges.")]
        [SettingPropertyGroup("{=JGUM.Settings.Group.Siege}Siege")]
        public bool EnableSiegeSurrender { get; set; } = true;

        [SettingPropertyBool("{=JGUM.Settings.EnableSiegeStarvationSallyOut.Name}Enable Starvation Sally Out", Order = 1, RequireRestart = false, HintText = "{=JGUM.Settings.EnableSiegeStarvationSallyOut.Hint}If no surrender, starving defenders can sally out.")]
        [SettingPropertyGroup("{=JGUM.Settings.Group.Siege}Siege")]
        public bool EnableSiegeStarvationSallyOut { get; set; } = true;

        [SettingPropertyFloatingInteger("{=JGUM.Settings.NearbyEnemyLordStrengthPercentage.Name}Nearby Enemy Lord Strength (%)", 0f, 100f, "0", Order = 2, RequireRestart = false, HintText = "{=JGUM.Settings.NearbyEnemyLordStrengthPercentage.Hint}Added percentage from nearby hostile lords.")]
        [SettingPropertyGroup("{=JGUM.Settings.Group.Siege}Siege")]
        public float NearbyEnemyLordStrengthPercentage { get; set; } = 50f;

        [SettingPropertyFloatingInteger("{=JGUM.Settings.NearbyEnemyLordDetectionRange.Name}Nearby Lord Detection Range", 0f, 20f, "0.0", Order = 3, RequireRestart = false, HintText = "{=JGUM.Settings.NearbyEnemyLordDetectionRange.Hint}Map distance used for nearby lord checks around settlement.")]
        [SettingPropertyGroup("{=JGUM.Settings.Group.Siege}Siege")]
        public float NearbyEnemyLordDetectionRange { get; set; } = 7f;

        [SettingPropertyBool("{=JGUM.Settings.EnableLordSurrender.Name}Enable Lord Surrender", Order = 0, RequireRestart = false, HintText = "{=JGUM.Settings.EnableLordSurrender.Hint}Enable surrender dialogs in hostile lord encounters.")]
        [SettingPropertyGroup("{=JGUM.Settings.Group.Lord}Lord")]
        public bool EnableLordSurrender { get; set; } = true;

        [SettingPropertyInteger("{=JGUM.Settings.LordDialogPriority.Name}Lord Dialog Priority", 0, 20000, Order = 1, RequireRestart = false, HintText = "{=JGUM.Settings.LordDialogPriority.Hint}Conversation override priority for lord surrender lines.")]
        [SettingPropertyGroup("{=JGUM.Settings.Group.Lord}Lord")]
        public int LordDialogPriority { get; set; } = 10000;

        [SettingPropertyFloatingInteger("{=JGUM.Settings.LordCalculatingMultiplier.Name}Calculating Trait Multiplier", 0f, 200f, "0", Order = 2, RequireRestart = false, HintText = "{=JGUM.Settings.LordCalculatingMultiplier.Hint}Multiplier for Calculating trait impact in surrender formula.")]
        [SettingPropertyGroup("{=JGUM.Settings.Group.Lord}Lord")]
        public float LordCalculatingMultiplier { get; set; } = 100f;

        [SettingPropertyFloatingInteger("{=JGUM.Settings.LordValorMultiplier.Name}Valor Trait Multiplier", 0f, 200f, "0", Order = 3, RequireRestart = false, HintText = "{=JGUM.Settings.LordValorMultiplier.Hint}Multiplier for Valor trait impact in surrender formula.")]
        [SettingPropertyGroup("{=JGUM.Settings.Group.Lord}Lord")]
        public float LordValorMultiplier { get; set; } = 100f;

        [SettingPropertyFloatingInteger("{=JGUM.Settings.LordMercyMultiplier.Name}Mercy Trait Multiplier", 0f, 200f, "0", Order = 4, RequireRestart = false, HintText = "{=JGUM.Settings.LordMercyMultiplier.Hint}Multiplier for Mercy trait impact in surrender formula.")]
        [SettingPropertyGroup("{=JGUM.Settings.Group.Lord}Lord")]
        public float LordMercyMultiplier { get; set; } = 100f;

        [SettingPropertyFloatingInteger("{=JGUM.Settings.LordHonorMultiplier.Name}Honor Trait Multiplier", 0f, 200f, "0", Order = 5, RequireRestart = false, HintText = "{=JGUM.Settings.LordHonorMultiplier.Hint}Multiplier for Honor trait impact in surrender formula.")]
        [SettingPropertyGroup("{=JGUM.Settings.Group.Lord}Lord")]
        public float LordHonorMultiplier { get; set; } = 100f;

        [SettingPropertyBool("{=JGUM.Settings.EnablePatrolSurrender.Name}Enable Patrol Surrender", Order = 0, RequireRestart = false, HintText = "{=JGUM.Settings.EnablePatrolSurrender.Hint}Enable surrender dialogs for patrol/non-lord hostile encounters.")]
        [SettingPropertyGroup("{=JGUM.Settings.Group.Patrol}Patrol")]
        public bool EnablePatrolSurrender { get; set; } = true;

        [SettingPropertyInteger("{=JGUM.Settings.PatrolDialogPriority.Name}Patrol Dialog Priority", 0, 20000, Order = 1, RequireRestart = false, HintText = "{=JGUM.Settings.PatrolDialogPriority.Hint}Conversation override priority for patrol surrender lines.")]
        [SettingPropertyGroup("{=JGUM.Settings.Group.Patrol}Patrol")]
        public int PatrolDialogPriority { get; set; } = 10000;
    }
}
#endif
