using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace JGUM.Actions
{
    public static class WarScoreHelper
    {
        public static void RecordSiegeSurrenderWarScore(Settlement settlement, Hero besiegerLeader)
        {
            if (settlement == null || besiegerLeader == null)
                return;

            IFaction winnerFaction = besiegerLeader.MapFaction;
            IFaction loserFaction = settlement.MapFaction; // Record before ownership changes!

            if (winnerFaction != null && loserFaction != null && winnerFaction != loserFaction)
            {
                StanceLink stance = winnerFaction.GetStanceWith(loserFaction);
                if (stance != null && stance.IsAtWar)
                {
                    if (stance.Faction1 == winnerFaction)
                    {
                        stance.SuccessfulSieges1++;
                        if (settlement.IsTown)
                        {
                            stance.SuccessfulTownSieges1++;
                        }
                    }
                    else if (stance.Faction2 == winnerFaction)
                    {
                        stance.SuccessfulSieges2++;
                        if (settlement.IsTown)
                        {
                            stance.SuccessfulTownSieges2++;
                        }
                    }
                }
            }
        }
        public static void RecordFieldSurrenderCasualties(TaleWorlds.CampaignSystem.Party.PartyBase loserParty, IFaction winnerFaction, int casualtiesAmount)
        {
            if (loserParty == null || winnerFaction == null) return;
            
            IFaction loserFaction = loserParty.MapFaction;
            if (loserFaction != null && winnerFaction != loserFaction)
            {
                StanceLink stance = winnerFaction.GetStanceWith(loserFaction);
                if (stance != null && stance.IsAtWar)
                {
                    if (stance.Faction1 == loserFaction)
                    {
                        stance.TroopCasualties1 += casualtiesAmount;
                    }
                    else if (stance.Faction2 == loserFaction)
                    {
                        stance.TroopCasualties2 += casualtiesAmount;
                    }
                }
            }
        }
    }
}
