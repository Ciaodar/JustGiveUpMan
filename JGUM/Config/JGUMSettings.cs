namespace JGUM.Config
{
    public static class JGUMSettings
    {
        // İleride MCM (Mod Configuration Menu) gibi kütüphanelere bağlayabileceğimiz,
        // MVP için statik tuttuğumuz konfigürasyon sınıfı.
        
        // 0 ile 2 arasında değişen çarpan. V1 için şimdilik sabit 1.0f veriyorum.
        public static float SurrenderTendencyMultiplier { get; set; } = 1.0f;
    }
}