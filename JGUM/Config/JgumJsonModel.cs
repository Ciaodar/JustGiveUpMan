namespace JGUM.Config
{
    public class JgumJsonModel
    {
        // Common
        public float SurrenderTendencyMultiplier { get; set; } = 1f;
        public float BaseSurrenderThreshold { get; set; } = 2.5f;
        public float PlayerMercyMultiplier { get; set; } = 100f;
        public int RequiredSurrenderCount { get; set; } = 3;

        // Siege
        public bool EnableSiegeSurrender { get; set; } = true;
        public bool EnableSiegeStarvationSallyOut { get; set; } = true;
        public float NearbyEnemyLordStrengthPercentage { get; set; } = 50f;
        public float NearbyEnemyLordDetectionRange { get; set; } = 7f;

        // Lord
        public bool EnableLordSurrender { get; set; } = true;
        public int LordDialogPriority { get; set; } = 10000;

        // Patrol
        public bool EnablePatrolSurrender { get; set; } = true;
        public int PatrolDialogPriority { get; set; } = 10000;

        // Trait tuning
        public float LordCalculatingMultiplier { get; set; } = 100f;
        public float LordValorMultiplier { get; set; } = 100f;
        public float LordMercyMultiplier { get; set; } = 100f;
        public float LordHonorMultiplier { get; set; } = 100f;
    }
}
