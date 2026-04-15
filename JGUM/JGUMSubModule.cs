using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
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
            

            TextObject testText = new TextObject("{=jgum_test_msg}JGUM Translate Error");
            
            InformationManager.DisplayMessage(new InformationMessage(testText.ToString(), Colors.Red));

            //MCM Check
            _ = JGUMSettings.Instance;
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
            }
        }
    }
}