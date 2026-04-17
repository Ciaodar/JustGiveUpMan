using JGUM.Calculators;
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
            AddPatrolEncounterDialogs(campaignGameStarter);
        }

        private void AddPatrolEncounterDialogs(CampaignGameStarter starter)
        {
            starter.AddDialogLine(
                "jgum_patrol_surrender_offer",
                "patrol_talk_start_attack",
                "jgum_patrol_player_response",
                StringCalculator.GetString("jgum_field_surrender_offer", "STOP, We cannot fight you. We surrender!"),
                CheckPatrolEncounterSurrender,
                null,
                10000);
            
            starter.AddDialogLine(
                "jgum_patrol_surrender_offer_attack_final",
                "patrol_talk_start_attack_final",
                "jgum_patrol_player_response",
                StringCalculator.GetString("jgum_field_surrender_offer", "STOP, We cannot fight you. We surrender!"),
                CheckPatrolEncounterSurrender,
                null,
                10000);

            starter.AddPlayerLine(
                "jgum_patrol_surrender_accept",
                "jgum_patrol_player_response",
                "close_window",
                StringCalculator.GetString("jgum_field_surrender_accept", "We accept your surrender. Lay down your arms!"),
                PatrolSurrenderCondition,
                AcceptPatrolSurrenderConsequence);

            starter.AddPlayerLine(
                "jgum_patrol_surrender_reject",
                "jgum_patrol_player_response",
                "close_window",
                StringCalculator.GetString("jgum_field_surrender_reject", "No, you all will be death. Prepare!"),
                PatrolSurrenderCondition,
                RejectPatrolSurrender);
        }

        private bool CheckPatrolEncounterSurrender()
        {
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
                StringCalculator.GetString("jgum_field_surrender_rejected", "The battle continues!"),
                Colors.Yellow));
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

                var enemyHero = PatrolEncounterSurrenderContext.EnemyParty?.LeaderHero;
                var mainParty = Hero.MainHero.PartyBelongedTo?.Party;
                if (enemyHero != null && mainParty != null)
                    TakePrisonerAction.Apply(mainParty, enemyHero);
            }

            TraitLevelingHelper.OnIncidentResolved(DefaultTraits.Mercy, 20);
            PatrolEncounterSurrenderContext.Clear();
        }

        public override void SyncData(IDataStore dataStore)
        {
        }
    }
}

