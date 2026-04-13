using System.Linq;
using TaleWorlds.CampaignSystem.Settlements;

namespace JGUM.Calculators
{
    public class SurrenderCalculator
    {
        public bool ShouldSettlementSurrender(Settlement settlement, float configMultiplier)
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
            int lordCount = defenders.Count(p => p.MobileParty != null && p.MobileParty.LeaderHero != null) +
                            settlement.HeroesWithoutParty.Count(h => h.IsLord);

            // Formül: (Power Ratio + Morale Ratio) - (Lord Sayısı * 0.1) > 2.5 * Config
            // Not: Lordlar savunmayı daha inatçı yapar, bu yüzden eksiltiyoruz.
            float totalRatio = (powerRatio + moraleRatio) - (lordCount * 0.1f);
            float threshold = 2.5f * configMultiplier;

            return totalRatio > threshold;
        }
    }
}