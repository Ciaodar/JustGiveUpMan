using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Settlements;
using JGUM.Config; // JGUMSettings'e erişim için eklendi

namespace JGUM.Calculators
{
    public class SurrenderCalculator
    {
        public bool ShouldSettlementSurrender(Settlement settlement, float configTendency)
        {
            if (settlement?.Town == null || !settlement.IsUnderSiege || settlement.SiegeEvent == null)
                return false;

            // 1. Yemek durumu kontrolü: Yemek bitmemişse teslim olunmaz.
            if (settlement.Town.FoodStocks > 0f)
                return false;

            var siegeEvent = settlement.SiegeEvent;
            
            // Saldıranlar: BesiegerCamp içerisindeki ilgili partiler.
            var attackers = siegeEvent.BesiegerCamp.GetInvolvedPartiesForEventType().ToList();
            
            // Savunanlar: Settlement'ta garnizon ve milisler Party olarak tutulur.
            var defenders = settlement.Parties.Select(p => p.Party).ToList();
            
            // Eğer kalede (Garnizon/Milis/Lord) savunacak kimse kalmamışsa anında teslim olurlar.
            if (!defenders.Any()) 
                return true;

            // Güç (Power) hesaplaması: CalculateCurrentStrength() ile partilerin anlık güçlerini float olarak alırız.
            float attackerPower = attackers.Sum(p => p.CalculateCurrentStrength());
            float defenderPower = defenders.Sum(p => p.CalculateCurrentStrength());

            // Savunmacıların toplam gücü kalmamışsa (öle öle bitmişlerse) teslim olurlar.
            if (defenderPower <= 0) 
                return true;

            float powerRatio = attackerPower / defenderPower;

            // Moral (Morale) hesaplaması
            var mobileAttackers = attackers.Where(p => p.MobileParty != null).ToList();
            var mobileDefenders = defenders.Where(p => p.MobileParty != null).ToList();

            float attackerMorale = mobileAttackers.Any() 
                ? mobileAttackers.Average(p => p.MobileParty.Morale) 
                : 0f;
                
            float defenderMorale = mobileDefenders.Any() 
                ? mobileDefenders.Average(p => p.MobileParty.Morale) 
                : 1f; // 0'a bölmeyi önlemek için savunan morali en az 1 kabul ediliyor.
            
            float moraleRatio = defenderMorale > 0 ? (attackerMorale / defenderMorale) : attackerMorale;

            // Lord sayısı kontrolü: 
            // 1. MobileParty leri olan lordları say.
            // 2. Kalede partisi dağılmış ama kalede bulunan (HeroesWithoutParty) lordları say.
            var defendingLords = defenders.Where(p => p.MobileParty != null && p.MobileParty.LeaderHero != null)
                .Select(p => p.MobileParty.LeaderHero)
                .Concat(settlement.HeroesWithoutParty.Where(h => h.IsLord))
                .Distinct()
                .ToList();
            int lordCount = defendingLords.Count;

            // Trait etkilerini hesapla
            float traitEffect = 0f;

            // Oyuncunun Mercy trait'i
            var player = Hero.MainHero;
            traitEffect += (player.GetTraitLevel(DefaultTraits.Mercy) / 10f) * (JGUMSettings.Instance.PlayerMercyMultiplier / 100f); // Mercy zalimse negatif etki yapar.

            // Kaledeki lordların trait'leri
            foreach (var lord in defendingLords)
            {
                traitEffect += (lord.GetTraitLevel(DefaultTraits.Calculating) / 20f) * (JGUMSettings.Instance.LordCalculatingMultiplier / 100f); // Hesapçıysa +
                traitEffect -= (lord.GetTraitLevel(DefaultTraits.Valor) / 10f) * (JGUMSettings.Instance.LordValorMultiplier / 100f); // Cesursa -
                traitEffect += (lord.GetTraitLevel(DefaultTraits.Mercy) / 20f) * (JGUMSettings.Instance.LordMercyMultiplier / 100f); // Merhametliyse +
                traitEffect -= (lord.GetTraitLevel(DefaultTraits.Honor) / 20f) * (JGUMSettings.Instance.LordHonorMultiplier / 100f); // Onurluysa -
            }

            // Formül: (Power Ratio + Morale Ratio) - (Lord Sayısı * 0.1) + Trait Etkisi > Base Surrender Threshold * Config Tendency
            // Not: Lordlar savunmayı daha inatçı yapar, bu yüzden eksiltiyoruz.
            float totalRatio = (powerRatio + moraleRatio) - (lordCount * 0.1f) + traitEffect;
            float threshold = JGUMSettings.Instance.BaseSurrenderThreshold / configTendency;

            return totalRatio > threshold;
        }
    }
}