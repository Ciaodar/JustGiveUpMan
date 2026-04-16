namespace JGUM.Config
{
    public class JgumJsonModel
    {
        public float SurrenderTendencyMultiplier { get; set; } = 1f;
        public float BaseSurrenderThreshold { get; set; } = 2.5f;
        public float PlayerMercyMultiplier { get; set; } = 100f;
        public float LordCalculatingMultiplier { get; set; } = 100f;
        public float LordValorMultiplier { get; set; } = 100f;
        public float LordMercyMultiplier { get; set; } = 100f;
        public float LordHonorMultiplier { get; set; } = 100f;
        public float NearbyEnemyLordStrengthPercentage { get; set; } = 50f;
        public int RequiredSurrenderCount { get; set; } = 3;
    }
}

