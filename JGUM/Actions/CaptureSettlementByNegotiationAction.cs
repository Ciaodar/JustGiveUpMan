using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace JGUM.Actions
{
    public static class CaptureSettlementByNegotiationAction
    {
        public static void Apply(Settlement settlement)
        {
            var siegeEvent = settlement.SiegeEvent;
            if (siegeEvent == null)
                return;

            Hero? besiegerLeader = siegeEvent.BesiegerCamp.LeaderParty?.LeaderHero;
            if (besiegerLeader == null)
                return;

            Hero? conversationHero = Campaign.Current.ConversationManager.OneToOneConversationHero;
            if (conversationHero != null)
                Hero.MainHero.SetPersonalRelation(conversationHero, (int)conversationHero.GetRelationWithPlayer() + 2);

            int currentMercy = Hero.MainHero.GetTraitLevel(DefaultTraits.Mercy);
            if (currentMercy < 2)
                Hero.MainHero.SetTraitLevel(DefaultTraits.Mercy, currentMercy + 1);

            ChangeOwnerOfSettlementAction.ApplyBySiege(besiegerLeader, besiegerLeader, settlement);

            if (PlayerEncounter.Current != null)
                PlayerEncounter.Finish();

            siegeEvent.FinalizeSiegeEvent();
            EncounterManager.StartSettlementEncounter(MobileParty.MainParty, settlement);
        }
    }
}

