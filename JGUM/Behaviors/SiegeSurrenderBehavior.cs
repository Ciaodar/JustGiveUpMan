using System.Collections.Generic;
using System.Linq;
using JGUM.Calculators;
using JGUM.Config;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
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
            if (JgumSettingsManager.EnableSiegeSurrender)
                AddDialogs(campaignGameStarter);
        }
        private void AddDialogs(CampaignGameStarter starter)
        {

            starter.AddDialogLine("jgum_siege_defender_start", "start", "jgum_player_start",
                StringCalculator.GetString("jgum_siege_defender_greeting","Thank you for coming, my {?PLAYER.GENDER}madame{?}sir{\\?}."),
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
            if (!JgumSettingsManager.EnableSiegeSurrender)
                return false;

            if (!SurrenderDialogContext.IsInSurrenderConversation)
                return false;

            if (SurrenderDialogContext.SurrenderingSettlement == null)
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

            if (!JgumSettingsManager.EnableSiegeSurrender && !JgumSettingsManager.EnableSiegeStarvationSallyOut)
                return;

            bool shouldSurrender = JgumSettingsManager.EnableSiegeSurrender &&
                                   _calculator.ShouldSettlementSurrender(settlement);

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
            else if (JgumSettingsManager.EnableSiegeStarvationSallyOut && settlement.IsStarving && IsPlayerBesieger(settlement))
            {
                StartStarvationSallyOut(settlement);
            }
        }

        private void StartSurrenderInquiry(Settlement settlement)
        {
            
            string notificationKey = settlement.IsCastle
                ? "jgum_siege_surrender_notification_castle"
                : "jgum_siege_surrender_notification_town";
            string notificationFallback = settlement.IsCastle
                ? "The castellan of {SETTLEMENT_NAME} wants to negotiate surrender with you."
                : "The commander of {SETTLEMENT_NAME} wants to negotiate surrender with you.";
            
            MBTextManager.SetTextVariable("SETTLEMENT_NAME", settlement.Name?.ToString() ?? settlement.StringId);

            InformationManager.ShowInquiry(new InquiryData(
                StringCalculator.GetString("jgum_siege_inquiry_title", "Surrender Negotiation"),
                StringCalculator.GetString(notificationKey, notificationFallback),                
                true,
                true,
                StringCalculator.GetString("jgum_siege_inquiry_accept", "Accept Meeting"),
                StringCalculator.GetString("jgum_siege_inquiry_reject", "Reject Offer"),
                affirmativeAction: () => OnInquiryAccepted(settlement),
                negativeAction: () => OnInquiryRejected(settlement)
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
            CharacterObject? defenderCharacter = FindSettlementRepresentative(settlement);

            if (defenderCharacter != null)
            {
                SurrenderDialogContext.IsInSurrenderConversation = true;
                SurrenderDialogContext.SurrenderingSettlement = settlement;

                var playerData = new ConversationCharacterData(CharacterObject.PlayerCharacter);
                var defenderData = new ConversationCharacterData(defenderCharacter, spawnAfterFight:true , noWeapon:true, noBodyguards:true, isCivilianEquipmentRequiredForLeader:true , isCivilianEquipmentRequiredForBodyGuardCharacters:true);

                try
                {
                    CampaignMapConversation.OpenConversation(playerData, defenderData);
                    return;
                }
                catch
                {
                    // If conversation setup fails for this settlement, continue with direct surrender.
                    SurrenderDialogContext.IsInSurrenderConversation = false;
                    SurrenderDialogContext.SurrenderingSettlement = null;
                }
            }

            AcceptSurrender();
        }

        private static CharacterObject? FindSettlementRepresentative(Settlement settlement)
        {
            var garrisonParty = settlement.Town?.GarrisonParty;
            CharacterObject? garrisonLeader = garrisonParty?.LeaderHero?.CharacterObject;

            CharacterObject? garrisonHeroRepresentative = null;
            CharacterObject? garrisonTroopRepresentative = null;
            var garrisonRoster = garrisonParty?.MemberRoster;
            if (garrisonRoster != null)
            {
                for (int i = 0; i < garrisonRoster.Count; i++)
                {
                    var element = garrisonRoster.GetElementCopyAtIndex(i);
                    if (element.Number <= 0 || element.Character == null)
                        continue;

                    if (garrisonTroopRepresentative == null)
                        garrisonTroopRepresentative = element.Character;

                    if (element.Character.IsHero)
                    {
                        garrisonHeroRepresentative = element.Character;
                        break;
                    }
                }
            }

            var siegeEvent = settlement.SiegeEvent;
            Hero? defenderLeader = siegeEvent != null
                ? Campaign.Current.Models.EncounterModel.GetLeaderOfSiegeEvent(siegeEvent, BattleSideEnum.Defender)
                : null;

            CharacterObject? heroRepresentative =
                garrisonLeader
                ?? garrisonHeroRepresentative
                ?? garrisonTroopRepresentative
                ?? defenderLeader?.CharacterObject
                ?? settlement.Parties.FirstOrDefault(p => p.LeaderHero != null && p.LeaderHero.IsLord)?.LeaderHero?.CharacterObject
                ?? settlement.Town?.Governor?.CharacterObject
                ?? settlement.Town?.GetDefenderParties(MapEvent.BattleTypes.None).FirstOrDefault(p => p.LeaderHero != null)?.LeaderHero?.CharacterObject
                ?? settlement.Town?.GarrisonPartyComponent?.Leader?.CharacterObject
                ?? settlement.Town?.GarrisonPartyComponent?.PartyOwner?.CharacterObject
                ?? (settlement.Owner?.IsPrisoner == false ? settlement.Owner.CharacterObject : null)
                ?? (settlement.OwnerClan?.Leader?.IsPrisoner == false ? settlement.OwnerClan.Leader.CharacterObject : null)
                ?? settlement.MapFaction?.Leader?.CharacterObject
                ?? settlement.OwnerClan?.AliveLords?.FirstOrDefault(h => h != null && h.IsAlive && !h.IsChild)?.CharacterObject
                ?? settlement.Notables.FirstOrDefault()?.CharacterObject;

            return heroRepresentative;
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
            if (JgumSettingsManager.EnableSiegeStarvationSallyOut && targetSettlement != null && targetSettlement.IsStarving)
            {
                StartStarvationSallyOut(targetSettlement);
            }
        }

        private static bool IsPlayerBesieger(Settlement settlement)
        {
            var playerParty = MobileParty.MainParty?.Party;
            return playerParty != null && settlement.SiegeEvent?.BesiegerCamp.HasInvolvedPartyForEventType(playerParty) == true;
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

