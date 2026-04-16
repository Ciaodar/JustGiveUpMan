using System.Collections.Generic;
using System.Linq;
using JGUM.Calculators;
using JGUM.Config;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace JGUM.Behaviors
{
    // Context for managing lord encounter surrender mechanics.
    internal static class LordEncounterSurrenderContext
    {
        public static bool IsInLordSurrenderDialog { get; set; }
        public static CharacterObject? EnemyLord { get; set; }

        public static void Clear()
        {
            EnemyLord = null;
            IsInLordSurrenderDialog = false;
        }
    }

    public class LordEncounterSurrenderBehavior : CampaignBehaviorBase
    {
        private readonly LordSurrenderCalculator _calculator = new();
        private Dictionary<Hero, int> _lordSurrenderCounts = new Dictionary<Hero, int>();

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }
        

        private void OnSessionLaunched(CampaignGameStarter campaignGameStarter)
        {
            AddLordEncounterDialogs(campaignGameStarter);
        }

        private void AddLordEncounterDialogs(CampaignGameStarter starter)
        {
            // Intercept native dialog outputs to inject surrender option with high priority
            // Input from: lord_attack_verify_commit or player_threatens_enemy_lord tokens
            starter.AddDialogLine("jgum_lord_surrender_offer", "lord_attack_verify_commit", "jgum_lord_player_response",
                StringCalculator.GetString("jgum_field_surrender_offer", "STOP, We cannot fight you. We surrender!"),
                CheckLordEncounterSurrender,
                null,
                10000  // High priority to intercept before native responses
            );
//party_encounter_lord_hostile_attacker_3
            // Alternative input token for different dialog paths
            starter.AddDialogLine("jgum_lord_surrender_offer_alt1", "player_threatens_enemy_lord", "jgum_lord_player_response",
                StringCalculator.GetString("jgum_field_surrender_offer", "STOP, We cannot fight you. We surrender!"),
                CheckLordEncounterSurrender,
                null,
                10000  // High priority
            );
            
            starter.AddDialogLine("jgum_lord_surrender_offer_alt2", "party_encounter_lord_hostile_attacker_3", "jgum_lord_player_response",
                StringCalculator.GetString("jgum_field_surrender_offer", "STOP, We cannot fight you. We surrender!"),
                CheckLordEncounterSurrender,
                null,
                10000  // High priority
            );

            // Player accepts surrender
            starter.AddPlayerLine("jgum_lord_surrender_accept", "jgum_lord_player_response", "close_window",
                StringCalculator.GetString("jgum_field_surrender_accept", "We accept your surrender. Lay down your arms!"),
                LordSurrenderCondition,
                AcceptSurrenderConsequence
            );

            // Player rejects and continues confrontation
            starter.AddPlayerLine("jgum_lord_surrender_reject", "jgum_lord_player_response", "close_window",
                StringCalculator.GetString("jgum_field_surrender_reject", "No, you all will be death. Prepare!"),
                LordSurrenderCondition,
                RejectLordSurrender
            );
        }

        private bool CheckLordEncounterSurrender()
        {
            // Get the current conversation lord
            var conversationHero = Campaign.Current.ConversationManager.OneToOneConversationHero;
            if (conversationHero == null)
                return false;

            var enemyParty = conversationHero.PartyBelongedTo?.Party;
            var mainParty = MobileParty.MainParty?.Party;

            if (enemyParty == null || mainParty == null)
                return false;

            bool shouldSurrender;
            var encounter = PlayerEncounter.Current;
            var battle = PlayerEncounter.Battle;

            if (encounter == null || battle == null) return false;
                var playerSide = encounter.PlayerSide;
                var enemySide = playerSide.GetOppositeSide();

                var playerParties = GetEncounterPartiesForSide(battle, playerSide);
                var enemyParties = GetEncounterPartiesForSide(battle, enemySide);
                var enemyLeaders = GetEncounterLeaders(enemyParties);

                if (!enemyLeaders.Any())
                    enemyLeaders.Add(conversationHero);

                
            shouldSurrender = _calculator.ShouldEnemySurrenderInEncounter(enemyLeaders);
            // Check if enemy lord should surrender based on current encounter state
            if (!shouldSurrender)
                return false;

            // Set context for surrender dialog
            LordEncounterSurrenderContext.IsInLordSurrenderDialog = true;
            LordEncounterSurrenderContext.EnemyLord = conversationHero.CharacterObject;

            return true;
        }

        private static List<PartyBase> GetEncounterPartiesForSide(MapEvent? battle, BattleSideEnum side)
        {
            if (battle == null)
                return new List<PartyBase>();

            var sideData = battle.GetMapEventSide(side);
            if (sideData?.Parties == null)
                return new List<PartyBase>();

            return sideData.Parties
                .Select(p => p.Party)
                .Where(p => p != null)
                .Distinct()
                .ToList();
        }

        private static List<Hero> GetEncounterLeaders(IEnumerable<PartyBase> parties)
        {
            return parties
                .Select(p => p.LeaderHero)
                .Where(h => h != null)
                .Distinct()
                .ToList();
        }

        private bool LordSurrenderCondition()
        {
            return LordEncounterSurrenderContext.IsInLordSurrenderDialog;
        }

        private void AcceptSurrenderConsequence()
        {
            // 1. Sadece lordu kaydet, işlemi ŞİMDİ yapma.
            LordEncounterSurrenderContext.EnemyLord = Hero.OneToOneConversationHero?.CharacterObject;
            
       
            CampaignEvents.ConversationEnded.AddNonSerializedListener(this, OnConversationEnded);
        }

        private void RejectLordSurrender()
        {
            // Update player cruelty trait for rejecting surrender
            var currentMercy = Hero.MainHero.GetTraitLevel(DefaultTraits.Mercy);
            if (currentMercy > -2)
                Hero.MainHero.SetTraitLevel(DefaultTraits.Mercy, currentMercy - 1);

            // Show message about continuing fight
            var message = new InformationMessage(
                StringCalculator.GetString("jgum_field_surrender_rejected", "The battle continues!"),
                Colors.Yellow
            );
            InformationManager.DisplayMessage(message);
        }

        private void OnConversationEnded(IEnumerable<CharacterObject> involvedCharacters)
        {
            CampaignEvents.ConversationEnded.ClearListeners(this);
            LordEncounterSurrenderContext.EnemyLord ??= involvedCharacters.ToMBList()
                .Find(hero => hero.HeroObject != Hero.MainHero);

            if (PlayerEncounter.Current != null)
            {
                // 1. Eğer savaş nesnesi yoksa başlat
                if (PlayerEncounter.Battle == null)
                {
                    PlayerEncounter.StartBattle();
                }

                // 2. SADECE BAYRAKLARI KALDIRIYORUZ
                // Update()'i zorla çağırmıyoruz! Diyalog kapandığında oyunun kendi 
                // ana döngüsü (GameLoop) bu bayrakları görecek ve menüleri kendi kendine açacak.
                PlayerEncounter.EnemySurrender = true;
                PlayerEncounter.SetPlayerVictorious();
                TakePrisonerAction.Apply(Hero.MainHero.PartyBelongedTo.Party, LordEncounterSurrenderContext.EnemyLord.HeroObject);
            }
    

            var surrenderedHero = LordEncounterSurrenderContext.EnemyLord.HeroObject;

            if (!_lordSurrenderCounts.ContainsKey(surrenderedHero))
            {
                _lordSurrenderCounts[surrenderedHero] = 0;
            }

            _lordSurrenderCounts[surrenderedHero]++;

            if (_lordSurrenderCounts[surrenderedHero] >= JgumSettingsManager.RequiredSurrenderCount)
            {
                var currentMercy = surrenderedHero.GetTraitLevel(DefaultTraits.Mercy);
                if (currentMercy < 2)
                {
                    surrenderedHero.SetTraitLevel(DefaultTraits.Mercy, currentMercy + 1);

                    _lordSurrenderCounts[surrenderedHero] = 0; 
                }
            }

            TraitLevelingHelper.OnIncidentResolved(DefaultTraits.Mercy, 20); 

            LordEncounterSurrenderContext.Clear();    
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("jgum_lord_surrender_counts", ref _lordSurrenderCounts);
        }
    }
}