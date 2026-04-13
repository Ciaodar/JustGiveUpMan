using System.Collections.Generic;
using System.Linq;
using JGUM.Calculators;
using JGUM.Config;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace JGUM.Behaviors
{
    internal static class SurrenderDialogContext
    {
        public static bool IsInSurrenderConversation { get; set; }
        public static Settlement? SurrenderingSettlement { get; set; }
    }

    public class SiegeSurrenderBehavior : CampaignBehaviorBase
    {
        private readonly SurrenderCalculator _calculator;

        public SiegeSurrenderBehavior()
        {
            _calculator = new SurrenderCalculator();
        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.DailyTickSettlementEvent.AddNonSerializedListener(this, OnDailyTickSettlement);
            CampaignEvents.ConversationEnded.AddNonSerializedListener(this, OnConversationEnded);
        }

        private void OnSessionLaunched(CampaignGameStarter campaignGameStarter)
        {
            AddDialogs(campaignGameStarter);
        }

        private void AddDialogs(CampaignGameStarter starter)
        {
            starter.AddDialogLine("jgum_siege_defender_start", "start", "jgum_player_start",
                StringCalculator.GetString("jgum_siege_defender_greeting","Thank you for coming, My lord."),
                SurrenderCondition,
                OnConversationRelationshipChanges(2),
                9999
            );
            // Player initiates surrender negotiation
            starter.AddPlayerLine("jgum_siege_player_surrender_start", "jgum_player_start", "jgum_siege_defender_surrenders",
                StringCalculator.GetString("jgum_siege_player_siege_surrender_offer","I see your situation is dire. Do you want to surrender and spare your people from further suffering?"),
                SurrenderCondition,
                null
            );
            
            // Defender responds with surrender plea
            starter.AddDialogLine("jgum_siege_defender_surrenders", "jgum_siege_defender_surrenders", "jgum_siege_player_surrender_response",
                StringCalculator.GetString("jgum_siege_surrender_offer","We are starving to death. The city is yours."),
                SurrenderCondition,
                null
            );

            // Player accepts surrender
            starter.AddPlayerLine("jgum_siege_player_accepts_surrender", "jgum_siege_player_surrender_response", "jgum_siege_merciful",
                StringCalculator.GetString("jgum_siege_surrender_accept","You made a wise choice. Lay down your arms, I spare your lives."), 
                PlayerResponseCondition,
                AcceptSurrender
            );

            // Player rejects surrender
            starter.AddPlayerLine("jgum_siege_player_rejects_surrender", "jgum_siege_player_surrender_response", "jgum_siege_cruel",
                StringCalculator.GetString("jgum_siege_surrender_reject", "It is too late to beg for mercy. I am coming to crush you."),
                PlayerResponseCondition,
                RejectSurrender
            );
            
            // Merciful ending response
            starter.AddDialogLine("jgum_siege_merciful", "jgum_siege_merciful", "close_window",
                StringCalculator.GetString("jgum_siege_defender_accepted", "We are grateful for your mercy!"),
                PlayerResponseCondition,
                null
            );
            
            // Cruel ending response
            starter.AddDialogLine("jgum_siege_cruel", "jgum_siege_cruel", "close_window",
                StringCalculator.GetString("jgum_siege_defender_rejected", "Kalradia will remember this cruelty!"),
                PlayerResponseCondition,
                null
            );
        }
        
        private bool SurrenderCondition()
        {
            if (!SurrenderDialogContext.IsInSurrenderConversation)
                return false;
            var conversationCharacter = Campaign.Current.ConversationManager.OneToOneConversationCharacter;
            if (conversationCharacter == null || SurrenderDialogContext.SurrenderingSettlement == null)
                return false;

            var conversationHero = Campaign.Current.ConversationManager.OneToOneConversationHero;
            if (conversationHero != null && !conversationHero.HasMet)
            {
                conversationHero.SetHasMet();
            }
            return true;
        }

        private bool PlayerResponseCondition()
        {
            return SurrenderDialogContext.IsInSurrenderConversation;
        }

        private void OnDailyTickSettlement(Settlement settlement)
        {
            if (!settlement.IsUnderSiege || SurrenderDialogContext.IsInSurrenderConversation)
                return;

            if (_calculator.ShouldSettlementSurrender(settlement, JGUMSettings.Instance.SurrenderTendencyMultiplier))
            {
                var playerParty = MobileParty.MainParty;
                if (settlement.SiegeEvent?.BesiegerCamp.HasInvolvedPartyForEventType(playerParty.Party) == true)
                {
                    StartSurrenderInquiry(settlement);
                }
                else
                {
                    var besiegerLeader = settlement.SiegeEvent?.BesiegerCamp.LeaderParty?.LeaderHero;
                    if (besiegerLeader != null)
                    {
                        ChangeOwnerOfSettlementAction.ApplyBySiege(besiegerLeader, besiegerLeader, settlement);
                    }
                }
            }
        }

        private void StartSurrenderInquiry(Settlement settlement)
        {
            var notificationText = new TextObject("{=jgum_siege_surrender_notification}{SETTLEMENT_NAME} wants to negotiate surrender with you.");
            notificationText.SetTextVariable("SETTLEMENT_NAME", settlement.Name);

            InformationManager.ShowInquiry(new InquiryData(
                new TextObject("{=jgum_siege_inquiry_title}Surrender Negotiation").ToString(),
                notificationText.ToString(),
                true, true,
                new TextObject("{=jgum_siege_inquiry_accept}Accept Meeting").ToString(),
                new TextObject("{=jgum_siege_inquiry_reject}Reject Offer").ToString(),
                () => OnInquiryAccepted(settlement),
                OnInquiryRejected
            ), true);
        }

        private ConversationSentence.OnConsequenceDelegate? OnConversationRelationshipChanges(int change)
        {
            var currentConvHero = Campaign.Current.ConversationManager.OneToOneConversationHero;
            if (currentConvHero != null)
                Hero.MainHero.SetPersonalRelation(currentConvHero, (int)currentConvHero.GetRelationWithPlayer()+change);
            return null;
        }
        
        private void OnInquiryAccepted(Settlement settlement)
        {
            CharacterObject? defenderCharacter =
                settlement.Parties.FirstOrDefault(p => p.LeaderHero != null && p.LeaderHero.IsLord)?.LeaderHero?.CharacterObject
                ?? settlement.Town?.Governor?.CharacterObject
                ?? settlement.Notables.FirstOrDefault()?.CharacterObject
                ?? settlement.Town?.GarrisonParty?.LeaderHero.CharacterObject;

            if (defenderCharacter != null)
            {
                SurrenderDialogContext.IsInSurrenderConversation = true;
                SurrenderDialogContext.SurrenderingSettlement = settlement;
                CampaignMapConversation.OpenConversation(new ConversationCharacterData(CharacterObject.PlayerCharacter), new ConversationCharacterData(defenderCharacter));
            }
            else
            {
                AcceptSurrender();
            }
        }

        private void OnInquiryRejected()
        {
            RejectSurrender();
        }

        private void AcceptSurrender()
        {
            var settlement = SurrenderDialogContext.SurrenderingSettlement;
            if (settlement == null) return;

            var siegeEvent = settlement.SiegeEvent;
            if (siegeEvent == null) return;

            var besiegerLeader = siegeEvent.BesiegerCamp.LeaderParty?.LeaderHero;
            if (besiegerLeader == null) return;

            OnConversationRelationshipChanges(2);
            ChangeOwnerOfSettlementAction.ApplyBySiege(besiegerLeader, besiegerLeader, settlement);
            
            var currentMercy = Hero.MainHero.GetTraitLevel(DefaultTraits.Mercy);
            if (currentMercy < 2)
                Hero.MainHero.SetTraitLevel(DefaultTraits.Mercy, currentMercy + 1);
            if (PlayerEncounter.Current !=null)
                PlayerEncounter.Finish();
            if (siegeEvent !=null)
                siegeEvent.FinalizeSiegeEvent();
            
            EncounterManager.StartSettlementEncounter(MobileParty.MainParty, settlement);
        }

        private void RejectSurrender()
        {
            OnConversationRelationshipChanges(-2);
            var currentMercy = Hero.MainHero.GetTraitLevel(DefaultTraits.Mercy);
            if (currentMercy > -2)
                Hero.MainHero.SetTraitLevel(DefaultTraits.Mercy, currentMercy - 1);
        }

        private void OnConversationEnded(IEnumerable<CharacterObject> involvedCharacters)
        {
            if (SurrenderDialogContext.IsInSurrenderConversation)
            {
                SurrenderDialogContext.IsInSurrenderConversation = false;
                SurrenderDialogContext.SurrenderingSettlement = null;
            }
        }

        public override void SyncData(IDataStore dataStore)
        {
        }
    }
}