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
            starter.AddDialogLine("jgum_defender_surrender_start", "start", "jgum_player_surrender_response",
                GetSurrenderOfferText().ToString(),
                SurrenderCondition,
                null,
                9999
            );

            starter.AddPlayerLine("jgum_player_accepts_surrender", "jgum_player_surrender_response", "close_window",
                GetAcceptText().ToString(),
                PlayerResponseCondition,
                AcceptSurrender
            );

            starter.AddPlayerLine("jgum_player_rejects_surrender", "jgum_player_surrender_response", "close_window",
                GetRejectText().ToString(),
                PlayerResponseCondition,
                RejectSurrender
            );
        }

        private TextObject GetSurrenderOfferText() => new TextObject(StringBringCalculator.GetRandomStringId("jgum_surrender_offer"));
        private TextObject GetAcceptText() => new TextObject(StringBringCalculator.GetRandomStringId("jgum_surrender_accept"));
        private TextObject GetRejectText() => new TextObject(StringBringCalculator.GetRandomStringId("jgum_surrender_reject"));

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

            if (_calculator.ShouldSettlementSurrender(settlement, JGUMSettings.SurrenderTendencyMultiplier))
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
            var notificationText = new TextObject("{=jgum_surrender_notification}{SETTLEMENT_NAME}");
            notificationText.SetTextVariable("SETTLEMENT_NAME", settlement.Name);

            InformationManager.ShowInquiry(new InquiryData(
                new TextObject("{=jgum_inquiry_title}").ToString(),
                notificationText.ToString(),
                true, true,
                new TextObject("{=jgum_inquiry_accept}").ToString(),
                new TextObject("{=jgum_inquiry_reject}").ToString(),
                () => OnInquiryAccepted(settlement),
                OnInquiryRejected
            ), true);
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