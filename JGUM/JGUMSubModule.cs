using System;
using System.IO;
using System.Linq;
using System.Reflection;
using JGUM.Config;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using SiegeNegotiation = JGUM.Behaviors.SiegeNegotiationBehavior;

namespace JGUM
{
    public class JGUMSubModule : MBSubModuleBase
    {
        private static bool _mcmResolverRegistered;
        private static bool _mcmLoadHookRegistered;
        private static bool _isActivatingMcmBridge;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();

            // Activate optional bridge as early as possible so MCM can discover its settings page.
            TryActivateOptionalMcmBridge();
        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();

            // Ensure MCM bridge is activated and subscribed after MCM builds its service container.
            TryActivateOptionalMcmBridge();

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
                campaignStarter.AddBehavior(new Behaviors.FiefPurchaseOfferBehavior());
                campaignStarter.AddBehavior(new Behaviors.VoluntarySurrenderBehavior());
                campaignStarter.AddBehavior(new AIBehaviors.AILordEncounterSurrenderBehavior());
                campaignStarter.AddBehavior(new AIBehaviors.AISiegeSurrenderBehavior());
            }
        }

        private static void TryActivateOptionalMcmBridge()
        {
            if (_isActivatingMcmBridge)
                return;

            _isActivatingMcmBridge = true;
            try
            {
                // Require MCM base assembly at runtime. If unavailable yet, hook load events and probe once.
                Assembly? mcmAssembly = FindLoadedAssembly("MCMv5");
                if (mcmAssembly == null)
                {
                    EnsureMcmAssemblyLoadHook();
                    try
                    {
                        mcmAssembly = Assembly.Load("MCMv5");
                    }
                    catch
                    {
                        return;
                    }
                }

                if (mcmAssembly == null)
                    return;

                EnsureMcmAssemblyResolver();

                string? gameRoot = BasePath.Name;
                if (string.IsNullOrWhiteSpace(gameRoot))
                    return;

                string assemblyLocation = typeof(JGUMSubModule).Assembly.Location;
                if (string.IsNullOrWhiteSpace(assemblyLocation))
                    return;

                string? binDir = Path.GetDirectoryName(assemblyLocation);
                if (string.IsNullOrWhiteSpace(binDir))
                    return;

                string bridgePath = Path.Combine(binDir, "JGUM.MCMBridge.dll");
                if (!File.Exists(bridgePath))
                    return;

                Assembly bridgeAssembly = FindLoadedAssembly("JGUM.MCMBridge")
                                          ?? Assembly.LoadFrom(bridgePath);

                Type? bootstrapType = bridgeAssembly.GetType("JGUM.MCMBridge.BridgeBootstrap");
                MethodInfo? tryRegister = bootstrapType?.GetMethod("TryRegister", BindingFlags.Public | BindingFlags.Static);
                tryRegister?.Invoke(null, null);
            }
            catch
            {
                // Optional bridge should never crash core startup.
            }
            finally
            {
                _isActivatingMcmBridge = false;
            }
        }

        private static Assembly? FindLoadedAssembly(string shortName)
        {
            return AppDomain.CurrentDomain
                .GetAssemblies()
                .FirstOrDefault(a => string.Equals(a.GetName().Name, shortName, StringComparison.OrdinalIgnoreCase));
        }

        private static void EnsureMcmAssemblyResolver()
        {
            if (_mcmResolverRegistered)
                return;

            _mcmResolverRegistered = true;
            AppDomain.CurrentDomain.AssemblyResolve += OnMcmAssemblyResolve;
        }

        private static Assembly? OnMcmAssemblyResolve(object sender, ResolveEventArgs args)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(args?.Name))
                    return null;

                string? requestedName = new AssemblyName(args!.Name).Name;
                if (string.Equals(requestedName, "MCMv5", StringComparison.OrdinalIgnoreCase))
                    return FindLoadedAssembly("MCMv5");
            }
            catch
            {
                // Ignore malformed assembly resolve requests.
            }

            return null;
        }

        private static void EnsureMcmAssemblyLoadHook()
        {
            if (_mcmLoadHookRegistered)
                return;

            _mcmLoadHookRegistered = true;
            AppDomain.CurrentDomain.AssemblyLoad += OnMcmAssemblyLoaded;
        }

        private static void OnMcmAssemblyLoaded(object sender, AssemblyLoadEventArgs args)
        {
            try
            {
                if (string.Equals(args.LoadedAssembly?.GetName().Name, "MCMv5", StringComparison.OrdinalIgnoreCase))
                {
                    AppDomain.CurrentDomain.AssemblyLoad -= OnMcmAssemblyLoaded;
                    TryActivateOptionalMcmBridge();
                }
            }
            catch
            {
                // Optional bridge should never crash core startup.
            }
        }
    }
}