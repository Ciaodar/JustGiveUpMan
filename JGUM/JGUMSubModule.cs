using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using JGUM.Config; // JGUMSettings'e erişim için eklendi

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
            
            // Eğer XML çalışmıyorsa ekranda parantez içindeki varsayılan metin görünür.
            // Çalışıyorsa XML'den çekeceği "BAŞARILI!" mesajı görünür.
            TextObject testText = new TextObject("{=jgum_test_msg}JGUM Translate Error");
            
            InformationManager.DisplayMessage(new InformationMessage(testText.ToString(), Colors.Red));

            // MCM ayarlarının yüklendiğinden emin olmak için Instance'a erişim.
            // MCM, AttributeGlobalSettings'i otomatik olarak yükler, bu sadece bir kontrol.
            _ = JGUMSettings.Instance;
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);

            if (game.GameType is Campaign)
            {
                CampaignGameStarter campaignStarter = (CampaignGameStarter)gameStarterObject;
                
                // Behavior'ı (Game Loop'a denk gelen yapı) ekliyoruz.
                campaignStarter.AddBehavior(new Behaviors.SiegeSurrenderBehavior());
            }
        }
    }
}