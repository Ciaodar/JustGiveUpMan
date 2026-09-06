using System.Collections.Generic;
using System.Linq;
using JGUM.Calculators;
using JGUM.Config;
using JGUM.Interop;
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
                "{=!}{JGUM_SIEGE_DEFENDER_GREETING}",
                () => {
                    if (!SurrenderCondition()) return false;
                    StringCalculator.SetDialogVariable("jgum_siege_defender_greeting", "Your siege has broken us, my {?PLAYER.GENDER}Lady{?}Lord{\\?}. We are ready to yield.");
                    return true;
                },
                OnConversationRelationshipChanges(2),
                9999
            );

            // Player initiates surrender negotiation
            starter.AddPlayerLine("jgum_siege_player_surrender_start", "jgum_player_start",
                "jgum_siege_defender_surrenders",
                "{=!}{JGUM_SIEGE_PLAYER_SIEGE_SURRENDER_OFFER}",
                () => {
                    if (!SurrenderCondition()) return false;
                    StringCalculator.SetDialogVariable("jgum_siege_player_siege_surrender_offer", "State your purpose for seeking this audience.");
                    return true;
                },
                null
            );

            // Defender responds with surrender plea
            starter.AddDialogLine("jgum_siege_defender_surrenders", "jgum_siege_defender_surrenders",
                "jgum_siege_player_surrender_response",
                "{=!}{JGUM_SIEGE_SURRENDER_OFFER}",
                () => {
                    if (!SurrenderCondition()) return false;
                    StringCalculator.SetDialogVariable("jgum_siege_surrender_offer", "We beg for clemency. Grant us our lives, and this holding is yours to command.");
                    return true;
                },
                null
            );


            // Player accepts surrender
            starter.AddPlayerLine("jgum_siege_player_accepts_surrender", "jgum_siege_player_surrender_response",
                "jgum_siege_merciful",
                "{=!}{JGUM_SIEGE_SURRENDER_ACCEPT}",
                () => {
                    if (!PlayerResponseCondition()) return false;
                    StringCalculator.SetDialogVariable("jgum_siege_surrender_accept", "You better know how to thank me for listening your cry for mercy!");
                    return true;
                },
                AcceptSurrender
            );

            // Player rejects surrender
            starter.AddPlayerLine("jgum_siege_player_rejects_surrender", "jgum_siege_player_surrender_response",
                "jgum_siege_cruel",
                "{=!}{JGUM_SIEGE_SURRENDER_REJECT}",
                () => {
                    if (!PlayerResponseCondition()) return false;
                    StringCalculator.SetDialogVariable("jgum_siege_surrender_reject", "Your pleas fall on deaf ears. There will be no quarter.");
                    return true;
                },
                RejectSurrender
            );

            // Merciful ending response
            starter.AddDialogLine("jgum_siege_merciful", "jgum_siege_merciful", "close_window",
                "{=!}{JGUM_SIEGE_DEFENDER_ACCEPTED}",
                () => {
                    if (!PlayerResponseCondition()) return false;
                    StringCalculator.SetDialogVariable("jgum_siege_defender_accepted", "Your name will be praised, my {?PLAYER.GENDER}Lady{?}Lord{\\?}. We are forever in your debt.");
                    return true;
                },
                null
            );

            // Cruel ending response
            starter.AddDialogLine("jgum_siege_cruel", "jgum_siege_cruel", "close_window",
                "{=!}{JGUM_SIEGE_DEFENDER_REJECTED}",
                () => {
                    if (!PlayerResponseCondition()) return false;
                    StringCalculator.SetDialogVariable("jgum_siege_defender_rejected", "Then may the gods judge us by our steel!");
                    return true;
                },
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
                        
                        JgumInteropEvents.RaiseSurrenderResolved(new JgumSurrenderRecord
                        {
                            Kind = JgumSurrenderKind.SiegeAutoSurrender,
                            SettlementId = settlement.StringId,
                            SurrenderingHeroId = settlement.OwnerClan?.Leader?.StringId,
                            SurrenderingPartyName = settlement.Name?.ToString(),
                            WinnerHeroId = besiegerLeader.StringId,
                            WinnerClanId = besiegerLeader.Clan?.StringId,
                            LoserFactionId = settlement.MapFaction?.StringId,
                            CampaignTimeDays = (float)CampaignTime.Now.ToDays,
                            AcceptedByPlayer = false,
                            Outcome = JgumSurrenderOutcome.Accepted
                        });
                    }
                }
            }
            else if (JgumSettingsManager.EnableSiegeStarvationSallyOut && settlement.IsStarving &&
                     IsPlayerBesieger(settlement))
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
                Hero.MainHero.SetPersonalRelation(currentConvHero,
                    (int)currentConvHero.GetRelationWithPlayer() + change);
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
                var defenderData = new ConversationCharacterData(
                    character: defenderCharacter,
                    party: null,
                    noHorse: true,
                    noWeapon: true,
                    spawnAfterFight: false,
                    isCivilianEquipmentRequiredForLeader: defenderCharacter.IsHero,
                    isCivilianEquipmentRequiredForBodyGuardCharacters: false,
                    noBodyguards: true);

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
            if (settlement.Town?.Governor != null)
                return settlement.Town.Governor.CharacterObject;

            Hero? defenderLeader = settlement.SiegeEvent != null
                ? Campaign.Current.Models.EncounterModel.GetLeaderOfSiegeEvent(settlement.SiegeEvent,
                    BattleSideEnum.Defender)
                : null;
            if (defenderLeader != null && defenderLeader.CurrentSettlement == settlement)
                return defenderLeader.CharacterObject;

            var lordInSettlement = settlement.Parties.FirstOrDefault(p => p.LeaderHero != null && p.LeaderHero.IsLord)
                                       ?.LeaderHero
                                   ?? settlement.Town?.GetDefenderParties(MapEvent.BattleTypes.None)
                                       .FirstOrDefault(p => p.LeaderHero != null && p.LeaderHero.IsLord)?.LeaderHero;
            if (lordInSettlement != null)
                return lordInSettlement.CharacterObject;

            var garrisonParty = settlement.Town?.GarrisonParty;
            if (garrisonParty?.LeaderHero != null)
                return garrisonParty.LeaderHero.CharacterObject;

            CharacterObject? troopRepresentative = GetTroopFromParty(garrisonParty)
                                                   ?? GetTroopFromParty(settlement.MilitiaPartyComponent?.MobileParty);

            if (troopRepresentative != null)
                return troopRepresentative;

            return settlement.Notables.FirstOrDefault()?.CharacterObject;
        }

        private static CharacterObject? GetTroopFromParty(MobileParty? party)
        {
            var roster = party?.MemberRoster;
            if (roster == null) return null;

            for (int i = 0; i < roster.Count; i++)
            {
                var element = roster.GetElementCopyAtIndex(i);
                if (element.Number > 0 && element.Character != null && !element.Character.IsHero)
                {
                    return element.Character;
                }
            }

            return null;
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
            if (PlayerEncounter.Current != null)
                PlayerEncounter.Finish();
            siegeEvent.FinalizeSiegeEvent();
            
            JgumInteropEvents.RaiseSurrenderResolved(new JgumSurrenderRecord
            {
                Kind = JgumSurrenderKind.SiegeAutoSurrender,
                SettlementId = settlement.StringId,
                SurrenderingHeroId = settlement.OwnerClan?.Leader?.StringId,
                SurrenderingPartyName = settlement.Name?.ToString(),
                WinnerHeroId = besiegerLeader.StringId,
                WinnerClanId = besiegerLeader.Clan?.StringId,
                LoserFactionId = settlement.MapFaction?.StringId,
                CampaignTimeDays = (float)CampaignTime.Now.ToDays,
                AcceptedByPlayer = true,
                Outcome = JgumSurrenderOutcome.Accepted
            });

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
            if (JgumSettingsManager.EnableSiegeStarvationSallyOut && targetSettlement != null &&
                targetSettlement.IsStarving)
            {
                StartStarvationSallyOut(targetSettlement);
            }

            var sEvent = targetSettlement?.SiegeEvent;
            var bLeader = sEvent?.BesiegerCamp?.LeaderParty?.LeaderHero;
            
            if (targetSettlement != null && bLeader != null)
            {
                JgumInteropEvents.RaiseSurrenderResolved(new JgumSurrenderRecord
                {
                    Kind = JgumSurrenderKind.SiegeAutoSurrender,
                    SettlementId = targetSettlement.StringId,
                    SurrenderingHeroId = targetSettlement.OwnerClan?.Leader?.StringId,
                    SurrenderingPartyName = targetSettlement.Name?.ToString(),
                    WinnerHeroId = bLeader.StringId,
                    WinnerClanId = bLeader.Clan?.StringId,
                    LoserFactionId = targetSettlement.MapFaction?.StringId,
                    CampaignTimeDays = (float)CampaignTime.Now.ToDays,
                    AcceptedByPlayer = false,
                    Outcome = JgumSurrenderOutcome.Rejected
                });
            }
        }

        private static bool IsPlayerBesieger(Settlement settlement)
        {
            var playerParty = MobileParty.MainParty?.Party;
            return playerParty != null &&
                   settlement.SiegeEvent?.BesiegerCamp.HasInvolvedPartyForEventType(playerParty) == true;
        }


        private static void StartStarvationSallyOut(Settlement settlement)
        {
            if (!settlement.IsUnderSiege || !IsPlayerBesieger(settlement))
                return;

            var playerParty = MobileParty.MainParty;
            var settlementFaction = settlement.MapFaction;
            if (playerParty != null && settlementFaction != null)
            {
                var detectionRange = JgumSettingsManager.NearbyEnemyLordDetectionRange;
                var settlementPosition = settlement.GatePosition;

                var nearbyDefenders = MobileParty.All
                    .Where(p => p?.Party != null && p.LeaderHero != null && p.LeaderHero.IsLord)
                    .Where(p => p.MapFaction != null && p.MapFaction == settlementFaction)
                    .Where(p => p.CurrentSettlement == null || p.CurrentSettlement == settlement)
                    .Where(p => (p.Position - settlementPosition).Length <= detectionRange)
                    .ToList();

                foreach (var party in nearbyDefenders)
                {
                    if (party != playerParty && party.CurrentSettlement == null)
                    {
                        party.Position = playerParty.Position;
                    }
                }
            }

            if (PlayerEncounter.Current == null)
            {
                if (settlement.IsFortification)
                    EncounterManager.StartPartyEncounter(settlement.Town?.GarrisonParty.Party,
                        settlement.SiegeEvent.BesiegerCamp.LeaderParty.Party);
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
                StringCalculator.GetString("jgum_siege_starvation_sally_out",
                    "The starving defenders have launched a desperate sally out!"),
                Colors.Yellow));
        }



        private void OnConversationEnded(IEnumerable<CharacterObject> involvedCharacters)
        {
            if (SurrenderDialogContext.IsInSurrenderConversation)
            {
                SurrenderDialogContext.IsInSurrenderConversation = false;
                SurrenderDialogContext.SurrenderingSettlement = null;
            }
            StringCalculator.ClearDialogVariables();
        }

        public override void SyncData(IDataStore dataStore)
        {
        }
    }
}

