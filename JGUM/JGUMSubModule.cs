using JGUM.Calculators;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using JGUM.Config;

namespace JGUM
{
    public class JGUMSubModule : MBSubModuleBase
    {
        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
        }
        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();


            // Initialize config backend (MCM or JSON) once on module root screen.
            JgumSettingsManager.Initialize();
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);


            if (game.GameType is Campaign)
            {
                CampaignGameStarter campaignStarter = (CampaignGameStarter)gameStarterObject;
                
                // Add siege surrender behavior for settlements under siege.
                campaignStarter.AddBehavior(new Behaviors.SiegeSurrenderBehavior());
                
                // Add lord encounter surrender behavior to intercept field lord conversations.
                campaignStarter.AddBehavior(new Behaviors.LordEncounterSurrenderBehavior());

                // Add patrol encounter surrender behavior for non-lord hostile patrol encounters.
                campaignStarter.AddBehavior(new Behaviors.PatrolEncounterSurrenderBehavior());
            }
        }
    }
}