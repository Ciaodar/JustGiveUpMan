using System.Collections.Generic;
using System.Linq;
using JGUM.Calculators;
using JGUM.Config;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace JGUM.Behaviors
{
    internal static class SurrenderDialogContext
    {
        public static bool IsInSurrenderConversation { get; set; }
        public static Settlement? SurrenderingSettlement { get; set; }
    }

    public class SiegeSurrenderBehavior : CampaignBehaviorBase
    {
        private readonly SiegeSurrenderCalculator _calculator;

        public SiegeSurrenderBehavior()
        {
            _calculator = new SiegeSurrenderCalculator();
        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.DailyTickSettlementEvent.AddNonSerializedListener(this, OnDailyTickSettlement);
            CampaignEvents.ConversationEnded.AddNonSerializedListener(this, OnConversationEnded);
        }

        private void OnSessionLaunched(CampaignGameStarter campaignGameStarter)
        {
#if DEBUG
            InformationManager.DisplayMessage(new InformationMessage(
                StringCalculator.GetString("jgum_test_msg", "Just Give Up Man loaded."),
                Colors.Gray));
#endif
            AddDialogs(campaignGameStarter);
        }

        private void AddDialogs(CampaignGameStarter starter)
        {
            starter.AddDialogLine("jgum_siege_defender_start", "start", "jgum_player_start",
                StringCalculator.GetString("jgum_siege_defender_greeting","Thank you for coming, my {?CONVERSATION_NPC.GENDER}Lady{?}Lord{\\?}."),
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

            bool shouldSurrender = _calculator.ShouldSettlementSurrender(settlement, JgumSettingsManager.SurrenderTendencyMultiplier);

            if (shouldSurrender)
            {
                if (IsPlayerBesieger(settlement))
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
            else if (settlement.IsStarving && IsPlayerBesieger(settlement))
            {
                StartStarvationSallyOut(settlement);
            }
        }

        private void StartSurrenderInquiry(Settlement settlement)
        {
            var notificationText = new TextObject(StringCalculator.GetString("jgum_siege_surrender_notification", "{SETTLEMENT_NAME} wants to negotiate surrender with you."));
            notificationText.SetTextVariable("SETTLEMENT_NAME", settlement.Name);

            InformationManager.ShowInquiry(new InquiryData(
                StringCalculator.GetString("jgum_siege_inquiry_title", "Surrender Negotiation"),
                notificationText.ToString(),
                true, true,
                StringCalculator.GetString("jgum_siege_inquiry_accept", "Accept Meeting"),
                StringCalculator.GetString("jgum_siege_inquiry_reject", "Reject Offer"),
                () => OnInquiryAccepted(settlement),
                () => OnInquiryRejected(settlement)
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
                var playerData = new ConversationCharacterData(CharacterObject.PlayerCharacter);
                var defenderData = BuildCivilianConversationData(defenderCharacter);
                CampaignMapConversation.OpenConversation(playerData, defenderData);
            }
            else
            {
                AcceptSurrender();
            }
        }

        private void OnInquiryRejected(Settlement settlement)
        {
            RejectSurrenderInternal(settlement);
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
            siegeEvent.FinalizeSiegeEvent();
            
            EncounterManager.StartSettlementEncounter(MobileParty.MainParty, settlement);
        }

        private void RejectSurrender()
        {
            RejectSurrenderInternal(null);
        }

        private void RejectSurrenderInternal(Settlement? settlement)
        {
            OnConversationRelationshipChanges(-2);
            var currentMercy = Hero.MainHero.GetTraitLevel(DefaultTraits.Mercy);
            if (currentMercy > -2)
                Hero.MainHero.SetTraitLevel(DefaultTraits.Mercy, currentMercy - 1);

            var targetSettlement = settlement ?? SurrenderDialogContext.SurrenderingSettlement;
            if (targetSettlement != null && targetSettlement.IsStarving)
            {
                StartStarvationSallyOut(targetSettlement);
            }
        }

        private static bool IsPlayerBesieger(Settlement settlement)
        {
            var playerParty = MobileParty.MainParty?.Party;
            return playerParty != null && settlement.SiegeEvent?.BesiegerCamp.HasInvolvedPartyForEventType(playerParty) == true;
        }

        private static ConversationCharacterData BuildCivilianConversationData(CharacterObject character)
        {
            var conversationDataType = typeof(ConversationCharacterData);
            foreach (var ctor in conversationDataType.GetConstructors())
            {
                var parameters = ctor.GetParameters();
                if (parameters.Length == 0 || parameters[0].ParameterType != typeof(CharacterObject))
                    continue;

                object?[] args = new object?[parameters.Length];
                args[0] = character;
                bool assignedCivilianFlag = false;

                for (int i = 1; i < parameters.Length; i++)
                {
                    var parameter = parameters[i];
                    if (!assignedCivilianFlag && parameter.ParameterType == typeof(bool))
                    {
                        // Prefer civilian outfit when opening surrender talks.
                        args[i] = true;
                        assignedCivilianFlag = true;
                        continue;
                    }

                    if (parameter.HasDefaultValue)
                    {
                        args[i] = parameter.DefaultValue;
                        continue;
                    }

                    args[i] = parameter.ParameterType.IsValueType
                        ? System.Activator.CreateInstance(parameter.ParameterType)
                        : null;
                }

                if (!assignedCivilianFlag)
                    continue;

                if (ctor.Invoke(args) is ConversationCharacterData data)
                    return data;
            }

            return new ConversationCharacterData(character);
        }

        private static void StartStarvationSallyOut(Settlement settlement)
        {
            if (!settlement.IsUnderSiege || !IsPlayerBesieger(settlement))
                return;

            if (PlayerEncounter.Current == null)
            {
                if (settlement.IsFortification)
                    EncounterManager.StartPartyEncounter(settlement.Town?.GarrisonParty.Party, settlement.SiegeEvent.BesiegerCamp.LeaderParty.Party);
            }
            

            var encounter = PlayerEncounter.Current;
            if (encounter == null)
                return;

            encounter.ForceSallyOut = true;

            if (PlayerEncounter.Battle == null)
            {
                PlayerEncounter.StartBattle();
            }

            var battle = PlayerEncounter.Battle;
            if (battle == null)
            {
                return;
            }

            InformationManager.DisplayMessage(new InformationMessage(
                StringCalculator.GetString("jgum_siege_starvation_sally_out", "The starving defenders have launched a desperate sally out!"),
                Colors.Yellow));
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

