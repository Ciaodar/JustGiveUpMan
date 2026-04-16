using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using JGUM.Config;

namespace JGUM.Calculators
{
    public class SiegeSurrenderCalculator
    {
        public bool ShouldSettlementSurrender(Settlement? settlement, float configTendency)
        {
            if (settlement?.Town == null || !settlement.IsUnderSiege || settlement.SiegeEvent == null)
                return false;

            // Food status check: Settlement will not surrender if not starving.
            if (!settlement.IsStarving)
                return false;

            var siegeEvent = settlement.SiegeEvent;
            
            // Attackers: Relevant parties within BesiegerCamp.
            var attackers = siegeEvent.BesiegerCamp.GetInvolvedPartiesForEventType().ToList();
            
            // Nearby enemy lords: Get hostile lords within detection range to boost defender defense perception.
            var nearbyEnemyLordStrength = GetNearbyEnemiesStrength(settlement);
            
            // Defenders: Settlement garrison and militia are held as Party objects.
            var defenders = settlement.Parties.Select(p => p.Party).ToList();
            
            // If there is no one left to defend in the fortress (Garrison/Militia/Lord), they surrender immediately.
            if (!defenders.Any()) 
                return true;

            // Power calculation: Get current strength of parties as float using CalculateCurrentStrength().
            float attackerPower = attackers.Sum(p => p.CalculateCurrentStrength());
            float defenderPower = defenders.Sum(p => p.CalculateCurrentStrength()) + nearbyEnemyLordStrength;

            // Defenders surrender if their total power is depleted (exhausted).
            if (defenderPower <= 0) 
                return true;

            float powerRatio = attackerPower / defenderPower;

            // Lord count check:
            // 1. Count lords with MobileParty.
            // 2. Count lords without parties but present in the settlement (HeroesWithoutParty).
            var defendingLords = defenders.Where(p => p.MobileParty != null && p.MobileParty.LeaderHero != null)
                .Select(p => p.MobileParty.LeaderHero)
                .Concat(settlement.HeroesWithoutParty.Where(h => h.IsLord))
                .Distinct()
                .ToList();
            int lordCount = defendingLords.Count;

            // Calculate trait effects
            float traitEffect = 0f;

            // Player's Mercy trait
            var player = Hero.MainHero;
            traitEffect += (player.GetTraitLevel(DefaultTraits.Mercy) / 10f) * (JgumSettingsManager.PlayerMercyMultiplier / 100f); // If Mercy/Cruelty is negative, it has negative effect.

            // Traits of lords in the fortress
            foreach (var lord in defendingLords)
            {
                traitEffect += (lord.GetTraitLevel(DefaultTraits.Calculating) / 20f) * (JgumSettingsManager.LordCalculatingMultiplier / 100f); // Calculating +
                traitEffect -= (lord.GetTraitLevel(DefaultTraits.Valor) / 10f) * (JgumSettingsManager.LordValorMultiplier / 100f); // Valor -
                traitEffect += (lord.GetTraitLevel(DefaultTraits.Mercy) / 20f) * (JgumSettingsManager.LordMercyMultiplier / 100f); // Mercy +
                traitEffect -= (lord.GetTraitLevel(DefaultTraits.Honor) / 20f) * (JgumSettingsManager.LordHonorMultiplier / 100f); // Honor -
            }

            // Formula: (Power Ratio + Morale Ratio) - (Lord Count * 0.1) + Trait Effect > Base Surrender Threshold * Config Tendency
            // Note: Lords make defense more stubborn, so we subtract them.
            float totalRatio = (powerRatio) - (lordCount * 0.1f) + traitEffect;
            float threshold = JgumSettingsManager.BaseSurrenderThreshold / configTendency;

            return totalRatio > threshold;
        }

        private float GetNearbyEnemiesStrength(Settlement? settlement)
        {
            float totalEnemyStrength = 0f;
            var playerParty = MobileParty.MainParty;
            var playerFaction = Hero.MainHero?.MapFaction;
            var settlementFaction = settlement?.MapFaction;

            if (playerParty == null || playerFaction == null || settlementFaction == null)
                return 0f;

            var playerPosition = playerParty.Position;
            var detectionRange = 7f; // Was configurable but found out the exact need.
            var strengthPercentage = JgumSettingsManager.NearbyEnemyLordStrengthPercentage / 100f;

            if (detectionRange <= 0f || strengthPercentage <= 0f)
                return 0f;

            // Only include lords with valid parties to avoid transient state nulls during campaign ticks.
            var hostileLords = Hero.AllAliveHeroes.Where(h =>
                    h.IsLord &&
                    h != Hero.MainHero &&
                    h.MapFaction != null &&
                    h.MapFaction.IsAtWarWith(playerFaction) &&
                    !h.MapFaction.IsAtWarWith(settlementFaction) // Exclude factions already at war with the besieged settlement.
                    && h.PartyBelongedTo != null &&
                    h.PartyBelongedTo.Party != null)
                .ToList();

            foreach (var hero in hostileLords)
            {
                var lordParty = hero.PartyBelongedTo;
                if (lordParty?.Party == null)
                    continue;

                float distance = (lordParty.Position - playerPosition).Length;
                if (distance > detectionRange)
                    continue;

                totalEnemyStrength += lordParty.Party.CalculateCurrentStrength() * strengthPercentage;
            }

            return totalEnemyStrength;
        }
    }
}