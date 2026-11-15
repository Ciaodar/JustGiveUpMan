using System.Linq;
using JGUM.Config;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace JGUM.Calculators
{
    public class SiegeSurrenderCalculator
    {
        public bool ShouldSettlementSurrender(Settlement? settlement)
        {
            if (!JgumSettingsManager.EnableSiegeSurrender)
                return false;

            if (settlement?.Town == null || !settlement.IsUnderSiege || settlement.SiegeEvent == null)
                return false;

            // Food status check: Settlement will not surrender if not starving.
            if (!settlement.IsStarving || settlement.Town.FoodStocks > 0)
                return false;

            var siegeEvent = settlement.SiegeEvent;
            
            // Attackers: Relevant parties within BesiegerCamp.
            var attackers = siegeEvent.BesiegerCamp.GetInvolvedPartiesForEventType().ToList();
            
            // Nearby enemy lords: Get hostile lords within detection range to boost defender defense perception.
            var nearbyEnemyLordStrength = GetNearbyEnemiesStrength(settlement);
            
            // Defenders: Settlement parties + nearby friendly lord parties that can reinforce.
            var defenders = settlement.Parties.Select(p => p.Party).ToList();
            var nearbyDefenderParties = GetNearbyDefendingLordParties(settlement);
            defenders.AddRange(nearbyDefenderParties);
            defenders = defenders.Distinct().ToList();
            
            // If there is no one left to defend in the fortress (Garrison/Militia/Lord), they surrender immediately.
            if (!defenders.Any()) 
                return true;

            // Power calculation: Get current strength of parties as float using CalculateCurrentStrength().
            float attackerPower = attackers.Sum(p => p.CalculateCurrentStrength());
            float defenderPower = defenders.Sum(p => p.CalculateCurrentStrength()) + nearbyEnemyLordStrength;
            float siegePressure = SiegePressureCalculator.GetSiegePressureModifier(settlement);

            // Defenders surrender if their total power is depleted (exhausted).
            if (defenderPower <= 0) 
                return true;

            float powerRatio = attackerPower / defenderPower;
            bool isPlayerBesieger = PartyBase.MainParty != null && siegeEvent.BesiegerCamp.HasInvolvedPartyForEventType(PartyBase.MainParty);

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

            if (isPlayerBesieger && settlement.OwnerClan?.HasBloodFeudWithPlayer == true)
            {
                traitEffect -= 1.5f;
            }

            // Player's Mercy trait
            var player = Hero.MainHero;
            traitEffect += (player.GetTraitLevel(DefaultTraits.Mercy) / 10f) * (JgumSettingsManager.PlayerMercyMultiplier / 100f); // If Mercy/Cruelty is negative, it has negative effect.

            // Traits of lords in the fortress
            foreach (var lord in defendingLords)
            {
                if (isPlayerBesieger && lord.Clan?.HasBloodFeudWithPlayer == true)
                {
                    traitEffect -= 1.0f;
                }

                traitEffect += (lord.GetTraitLevel(DefaultTraits.Calculating) / 20f) * (JgumSettingsManager.LordCalculatingMultiplier / 100f); // Calculating +
                traitEffect -= (lord.GetTraitLevel(DefaultTraits.Valor) / 10f) * (JgumSettingsManager.LordValorMultiplier / 100f); // Valor -
                traitEffect += (lord.GetTraitLevel(DefaultTraits.Mercy) / 20f) * (JgumSettingsManager.LordMercyMultiplier / 100f); // Mercy +
                traitEffect -= (lord.GetTraitLevel(DefaultTraits.Honor) / 20f) * (JgumSettingsManager.LordHonorMultiplier / 100f); // Honor -
            }

            float totalRatio = powerRatio + siegePressure - (lordCount * 0.1f) + traitEffect;
            float baseThreshold = JgumSettingsManager.BaseSurrenderThreshold / JgumSettingsManager.SurrenderTendencyMultiplier;
            float guaranteedThreshold = JgumSettingsManager.GuaranteedSurrenderThreshold / JgumSettingsManager.SurrenderTendencyMultiplier;
            
            // Ensure guaranteed is always >= base to avoid math errors
            if (guaranteedThreshold < baseThreshold)
                guaranteedThreshold = baseThreshold;

            int mode = JgumSettingsManager.SurrenderRandomnessMode;

            // 0: Off, 1: Thresholded, 2: Unbound
            if (mode == 0) // Off
            {
                return totalRatio >= baseThreshold;
            }
            else if (mode == 1) // Thresholded
            {
                if (totalRatio >= guaranteedThreshold)
                    return true;
                if (totalRatio < baseThreshold)
                    return false;

                // We are between base and guaranteed threshold.
                float range = guaranteedThreshold - baseThreshold;
                if (range <= 0.001f)
                    return true;

                float chance = (totalRatio - baseThreshold) / range;
                return TaleWorlds.Core.MBRandom.RandomFloat <= chance;
            }
            else // Unbound
            {
                // In unbound, there's always a slight chance.
                // We map totalRatio against guaranteedThreshold.
                if (totalRatio >= guaranteedThreshold)
                {
                    // If above guaranteed, still 95% minimum chance, up to 100%
                    float extra = totalRatio - guaranteedThreshold;
                    float chance = 0.95f + (extra * 0.01f);
                    if (chance > 1f) chance = 1f;
                    return TaleWorlds.Core.MBRandom.RandomFloat <= chance;
                }
                else if (totalRatio < baseThreshold)
                {
                    // If below base, there is a tiny chance (max 5%) based on how close they are
                    float deficit = baseThreshold - totalRatio;
                    float chance = 0.05f - (deficit * 0.01f);
                    if (chance < 0f) chance = 0f;
                    return TaleWorlds.Core.MBRandom.RandomFloat <= chance;
                }
                else
                {
                    // Between thresholds
                    float range = guaranteedThreshold - baseThreshold;
                    if (range <= 0.001f)
                        return TaleWorlds.Core.MBRandom.RandomFloat <= 0.5f;

                    // Starts from 5% up to 95%
                    float normalized = (totalRatio - baseThreshold) / range;
                    float chance = 0.05f + (normalized * 0.90f);
                    return TaleWorlds.Core.MBRandom.RandomFloat <= chance;
                }
            }
        }

        private float GetNearbyEnemiesStrength(Settlement? settlement)
        {
            float totalEnemyStrength = 0f;
            var settlementFaction = settlement?.MapFaction;
            var besiegerFaction = settlement?.SiegeEvent?.BesiegerCamp?.LeaderParty?.MapFaction;

            if (settlementFaction == null)
                return 0f;

            var settlementPosition = settlement!.GatePosition;
            var detectionRange = JgumSettingsManager.NearbyEnemyLordDetectionRange;
            var strengthPercentage = JgumSettingsManager.NearbyEnemyLordStrengthPercentage / 100f;

            if (detectionRange <= 0f || strengthPercentage <= 0f)
                return 0f;

            foreach (var party in MobileParty.All)
            {
                if (party?.Party == null || party.LeaderHero == null || !party.LeaderHero.IsLord)
                    continue;
                if (party.MapFaction == null)
                    continue;
                if (!party.MapFaction.IsAtWarWith(settlementFaction))
                    continue;
                if (besiegerFaction != null && !party.MapFaction.IsAtWarWith(besiegerFaction))
                    continue;

                float distance = (party.Position - settlementPosition).Length;
                if (distance > detectionRange)
                    continue;

                totalEnemyStrength += party.Party.CalculateCurrentStrength() * strengthPercentage;
            }

            return totalEnemyStrength;
        }

        private static System.Collections.Generic.List<PartyBase> GetNearbyDefendingLordParties(Settlement settlement)
        {
            var settlementFaction = settlement.MapFaction;
            if (settlementFaction == null)
                return new System.Collections.Generic.List<PartyBase>();

            var detectionRange = JgumSettingsManager.NearbyEnemyLordDetectionRange;
            var settlementPosition = settlement.GatePosition;

            return MobileParty.All
                .Where(p => p?.Party != null && p.LeaderHero != null && p.LeaderHero.IsLord)
                .Where(p => p.MapFaction != null && p.MapFaction == settlementFaction)
                .Where(p => p.CurrentSettlement == null || p.CurrentSettlement == settlement)
                .Where(p => (p.Position - settlementPosition).Length <= detectionRange)
                .Select(p => p.Party)
                .Distinct()
                .ToList();
        }
    }
}