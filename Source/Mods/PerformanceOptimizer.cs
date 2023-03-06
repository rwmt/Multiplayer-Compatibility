using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Performance Optimizer by Taranchuk</summary>
    /// <see href="https://github.com/Taranchuk/PerformanceOptimizer"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2664723367"/>
    [MpCompatFor("Taranchuk.PerformanceOptimizer")]
    public class PerformanceOptimizer
    {
        private static FastInvokeHandler refreshCache;
        private static bool isAutoSaving = false;

        public PerformanceOptimizer(ModContentPack mod)
        {
            // Time controls
            {
                var doTimeControlsHotkeys = AccessTools.DeclaredMethod("Multiplayer.Client.AsyncTime.TimeControlPatch:DoTimeControlsHotkeys");
                if (doTimeControlsHotkeys != null)
                    MpCompat.harmony.Patch(AccessTools.DeclaredMethod("PerformanceOptimizer.Optimization_DoPlaySettings_DoTimespeedControls:DoTimeControlsGUI"),
                        prefix: new HarmonyMethod(doTimeControlsHotkeys));
            }

            // Clear cache on join, etc.
            {
                var resetDataMethod = AccessTools.DeclaredMethod("PerformanceOptimizer.PerformanceOptimizerMod:ResetStaticData");
                MpCompat.harmony.Patch(resetDataMethod,
                    prefix: new HarmonyMethod(typeof(PerformanceOptimizer), nameof(CancelIfAutosaving)));
                refreshCache = MethodInvoker.GetHandler(resetDataMethod);

                MpCompat.harmony.Patch(AccessTools.DeclaredMethod("Multiplayer.Client.MultiplayerSession:SaveGameToFile"),
                    prefix: new HarmonyMethod(typeof(PerformanceOptimizer), nameof(PreSaveToFile)),
                    postfix: new HarmonyMethod(typeof(PerformanceOptimizer), nameof(PostSaveToFile)));

                // Big shoutout to NotFood for pointing me to the correct method to clear the cache in.
                // I spent hours trying to find a correct method where to clear the cache but failed.
                MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(GameComponentUtility), nameof(GameComponentUtility.FinalizeInit)),
                    postfix: new HarmonyMethod(typeof(PerformanceOptimizer), nameof(RefreshCachePrefix)));
            }
        }

        // While the game is saving, PerformanceOptimizer clears the cache.
        // However, in MP only the host is saving, but not the clients - we need to either clear for all, or for none.
        private static bool CancelIfAutosaving()
        {
            if (MP.IsInMultiplayer && MP.IsHosting)
            {
#if DEBUG
                if (Find.CurrentMap != null) 
                    Log.Message($"{(isAutoSaving ? "Autosaving" : "Refreshing cache at")} at: {Find.TickManager?.TicksGame}");
#endif
                return !isAutoSaving;
            }

#if DEBUG
            if (MP.IsInMultiplayer && Find.CurrentMap != null) 
                Log.Message($"Refreshing cache at: {Find.TickManager?.TicksGame}");
#endif
            isAutoSaving = false;
            return true;
        }

        private static void RefreshCachePrefix() => refreshCache(null);

        private static void PreSaveToFile() => isAutoSaving = true;
        private static void PostSaveToFile() => isAutoSaving = false;
    }
}