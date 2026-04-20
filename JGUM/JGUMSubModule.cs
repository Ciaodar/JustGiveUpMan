using System;
using System.IO;
using System.Linq;
using System.Reflection;
using JGUM.Calculators;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using JGUM.Config;
using SiegeNegotiation = JGUM.Behaviors.SiegeNegotiationBehavior;

namespace JGUM
{
    public class JGUMSubModule : MBSubModuleBase
    {
        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();

            // Activate optional bridge as early as possible so MCM can discover its settings page.
            TryActivateOptionalMcmBridge();
        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();

            // Initialize core JSON settings first.
            JgumSettingsManager.Initialize();
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);

            if (game.GameType is Campaign)
            {
                CampaignGameStarter campaignStarter = (CampaignGameStarter)gameStarterObject;

                campaignStarter.AddBehavior(new Behaviors.SiegeSurrenderBehavior());
                campaignStarter.AddBehavior(new Behaviors.LordEncounterSurrenderBehavior());
                campaignStarter.AddBehavior(new Behaviors.PatrolEncounterSurrenderBehavior());
                campaignStarter.AddBehavior(new SiegeNegotiation.SiegeNegotiationBehavior());
            }
        }

        private static void TryActivateOptionalMcmBridge()
        {
            try
            {
                // Require MCM base assembly at runtime. If unavailable, do nothing.
                Assembly.Load("MCMv5");

                string? gameRoot = BasePath.Name;
                if (string.IsNullOrWhiteSpace(gameRoot))
                    return;

                string bridgePath = Path.Combine(
                    gameRoot,
                    "Modules",
                    "JGUM",
                    "bin",
                    "Win64_Shipping_Client",
                    "JGUM.MCMBridge.dll");

                if (!File.Exists(bridgePath))
                    return;

                Assembly bridgeAssembly = AppDomain.CurrentDomain
                                              .GetAssemblies()
                                              .FirstOrDefault(a => string.Equals(a.GetName().Name, "JGUM.MCMBridge", StringComparison.OrdinalIgnoreCase))
                                          ?? Assembly.LoadFrom(bridgePath);

                Type? bootstrapType = bridgeAssembly.GetType("JGUM.MCMBridge.BridgeBootstrap");
                MethodInfo? tryRegister = bootstrapType?.GetMethod("TryRegister", BindingFlags.Public | BindingFlags.Static);
                tryRegister?.Invoke(null, null);
            }
            catch
            {
                // Optional bridge should never crash core startup.
            }
        }
    }
}