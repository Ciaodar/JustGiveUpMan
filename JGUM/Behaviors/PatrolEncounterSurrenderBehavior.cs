using JGUM.Calculators;
using JGUM.Interop;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace JGUM.Behaviors
{
    internal static class PatrolEncounterSurrenderContext
    {
        public static bool IsInPatrolSurrenderDialog { get; set; }
        public static PartyBase? EnemyParty { get; set; }

        public static void Clear()
        {
            IsInPatrolSurrenderDialog = false;
            EnemyParty = null;
        }
    }

    public class PatrolEncounterSurrenderBehavior : CampaignBehaviorBase
    {
        private readonly LordSurrenderCalculator _calculator = new();

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        private void OnSessionLaunched(CampaignGameStarter campaignGameStarter)
        {
            if (!JGUM.Config.JgumSettingsManager.EnablePatrolSurrender)
                return;

            AddPatrolEncounterDialogs(campaignGameStarter);
        }

        private void AddPatrolEncounterDialogs(CampaignGameStarter starter)
        {
            int priority = JGUM.Config.JgumSettingsManager.PatrolDialogPriority;

            starter.AddDialogLine(
                "jgum_patrol_surrender_offer",
                "patrol_talk_start_attack",
                "jgum_patrol_player_response",
                "{=!}{JGUM_FIELD_SURRENDER_OFFER}",
                () => {
                    if (!CheckPatrolEncounterSurrender()) return false;
                    StringCalculator.SetDialogVariable("jgum_field_surrender_offer", "Wait! We yield, {?PLAYER.GENDER}madam{?}sir{\\?}! Don't cut us down!");
                    return true;
                },
                null,
                priority);
            
            starter.AddDialogLine(
                "jgum_patrol_surrender_offer_attack_final",
                "patrol_talk_start_attack_final",
                "jgum_patrol_player_response",
                "{=!}{JGUM_FIELD_SURRENDER_OFFER}",
                () => {
                    if (!CheckPatrolEncounterSurrender()) return false;
                    StringCalculator.SetDialogVariable("jgum_field_surrender_offer", "Wait! We yield, {?PLAYER.GENDER}madam{?}sir{\\?}! Don't cut us down!");
                    return true;
                },
                null,
                priority);

            starter.AddPlayerLine(
                "jgum_patrol_surrender_accept",
                "jgum_patrol_player_response",
                "close_window",
                "{=!}{JGUM_FIELD_SURRENDER_ACCEPT}",
                () => {
                    if (!PatrolSurrenderCondition()) return false;
                    StringCalculator.SetDialogVariable("jgum_field_surrender_accept", "Drop your weapons and you may yet live.");
                    return true;
                },
                AcceptPatrolSurrenderConsequence);

            starter.AddPlayerLine(
                "jgum_patrol_surrender_reject",
                "jgum_patrol_player_response",
                "close_window",
                "{=!}{JGUM_FIELD_SURRENDER_REJECT}",
                () => {
                    if (!PatrolSurrenderCondition()) return false;
                    StringCalculator.SetDialogVariable("jgum_field_surrender_reject", "You should have fled when you had the chance. No mercy!");
                    return true;
                },
                RejectPatrolSurrender);
        }

        private bool CheckPatrolEncounterSurrender()
        {
            if (!JGUM.Config.JgumSettingsManager.EnablePatrolSurrender)
                return false;

            var encounter = PlayerEncounter.Current;
            var mainParty = MobileParty.MainParty?.Party;
            var enemyParty = PlayerEncounter.EncounteredParty;
            if (encounter == null || mainParty == null || enemyParty == null)
                return false;

            // Keep lord flow in LordEncounterSurrenderBehavior.
            if (enemyParty.LeaderHero != null && enemyParty.LeaderHero.IsLord)
                return false;

            var battle = PlayerEncounter.Battle;
            float playerStrength;
            float enemyStrength;

            if (battle != null)
            {
                var playerSide = encounter.PlayerSide;
                var enemySide = playerSide.GetOppositeSide();
                playerStrength = battle.StrengthOfSide[(int)playerSide];
                enemyStrength = battle.StrengthOfSide[(int)enemySide];
            }
            else
            {
                playerStrength = mainParty.CalculateCurrentStrength();
                enemyStrength = enemyParty.CalculateCurrentStrength();
            }

            bool shouldSurrender = _calculator.ShouldEnemySurrenderInEncounter(null, playerStrength, enemyStrength);
            if (!shouldSurrender)
                return false;

            PatrolEncounterSurrenderContext.IsInPatrolSurrenderDialog = true;
            PatrolEncounterSurrenderContext.EnemyParty = enemyParty;
            return true;
        }

        private static bool PatrolSurrenderCondition()
        {
            return PatrolEncounterSurrenderContext.IsInPatrolSurrenderDialog;
        }

        private void AcceptPatrolSurrenderConsequence()
        {
            CampaignEvents.ConversationEnded.AddNonSerializedListener(this, OnConversationEnded);
        }

        private void RejectPatrolSurrender()
        {
            var currentMercy = Hero.MainHero.GetTraitLevel(DefaultTraits.Mercy);
            if (currentMercy > -2)
                Hero.MainHero.SetTraitLevel(DefaultTraits.Mercy, currentMercy - 1);

            InformationManager.DisplayMessage(new InformationMessage(
                StringCalculator.GetString("jgum_field_surrender_rejected", "The dogs will get their scraps after all!"),
                Colors.Yellow));

            var enemyParty = PatrolEncounterSurrenderContext.EnemyParty;
            if (enemyParty != null)
            {
                JgumInteropEvents.RaiseSurrenderResolved(new JgumSurrenderRecord
                {
                    Kind = JgumSurrenderKind.PatrolEncounter,
                    SurrenderingHeroId = enemyParty.LeaderHero?.StringId,
                    SurrenderingPartyName = enemyParty.Name?.ToString(),
                    WinnerHeroId = Hero.MainHero.StringId,
                    WinnerClanId = Hero.MainHero.Clan?.StringId,
                    LoserFactionId = enemyParty.MapFaction?.StringId,
                    CampaignTimeDays = (float)CampaignTime.Now.ToDays,
                    AcceptedByPlayer = false,
                    Outcome = JgumSurrenderOutcome.Rejected
                });
            }
        }

        private void OnConversationEnded(System.Collections.Generic.IEnumerable<CharacterObject> involvedCharacters)
        {
            CampaignEvents.ConversationEnded.ClearListeners(this);

            if (PlayerEncounter.Current != null)
            {
                if (PlayerEncounter.Battle == null)
                    PlayerEncounter.StartBattle();

                PlayerEncounter.EnemySurrender = true;
                PlayerEncounter.SetPlayerVictorious();

                var encounteredParty = PatrolEncounterSurrenderContext.EnemyParty;
                if (encounteredParty != null && Hero.MainHero.MapFaction != null)
                {
                    int totalCasualties = encounteredParty.MemberRoster.TotalManCount;
                    JGUM.Actions.WarScoreHelper.RecordFieldSurrenderCasualties(encounteredParty, Hero.MainHero.MapFaction, totalCasualties);
                }

                var enemyHero = PatrolEncounterSurrenderContext.EnemyParty?.LeaderHero;
                var mainParty = Hero.MainHero.PartyBelongedTo?.Party;
                if (enemyHero != null && mainParty != null)
                    TakePrisonerAction.Apply(mainParty, enemyHero);
            }

            var surrenderedParty = PatrolEncounterSurrenderContext.EnemyParty;
            if (surrenderedParty != null)
            {
                JgumInteropEvents.RaiseSurrenderResolved(new JgumSurrenderRecord
                {
                    Kind = JgumSurrenderKind.PatrolEncounter,
                    SurrenderingHeroId = surrenderedParty.LeaderHero?.StringId,
                    SurrenderingPartyName = surrenderedParty.Name?.ToString(),
                    WinnerHeroId = Hero.MainHero.StringId,
                    WinnerClanId = Hero.MainHero.Clan?.StringId,
                    LoserFactionId = surrenderedParty.MapFaction?.StringId,
                    CampaignTimeDays = (float)CampaignTime.Now.ToDays,
                    AcceptedByPlayer = true,
                    Outcome = JgumSurrenderOutcome.Accepted
                });
            }

            TraitLevelingHelper.OnIncidentResolved(DefaultTraits.Mercy, 20);
            PatrolEncounterSurrenderContext.Clear();
        }

        public override void SyncData(IDataStore dataStore)
        {
        }
    }
}

