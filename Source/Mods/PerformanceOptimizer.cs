using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
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
        #region Init

        public PerformanceOptimizer(ModContentPack mod)
        {
            // Time controls
            {
                var doTimeControlsHotkeys = AccessTools.DeclaredMethod("Multiplayer.Client.AsyncTime.TimeControlPatch:DoTimeControlsHotkeys");
                if (doTimeControlsHotkeys != null)
                {
                    doTimeControlsHotkeysMethod = MethodInvoker.GetHandler(doTimeControlsHotkeys);
                    MpCompat.harmony.Patch(AccessTools.DeclaredMethod("PerformanceOptimizer.Optimization_DoPlaySettings_DoTimespeedControls:DoTimeControlsGUI"),
                        prefix: new HarmonyMethod(typeof(PerformanceOptimizer), nameof(PreDoTimeControlsGUI)));
                }
                else Log.Error("Could not find TimeControlPatch:DoTimeControlsHotkeys, speed control hot keys won't work with disabled/hidden speed control UI.");
            }

            // Clear cache on join, etc.
            {
                var resetDataMethod = AccessTools.DeclaredMethod("PerformanceOptimizer.PerformanceOptimizerMod:ResetStaticData");
                MpCompat.harmony.Patch(resetDataMethod,
                    prefix: new HarmonyMethod(typeof(PerformanceOptimizer), nameof(CancelIfAutosaving)));
                refreshCache = MethodInvoker.GetHandler(resetDataMethod);

                var method = AccessTools.DeclaredMethod("Multiplayer.Client.Autosaving:SaveGameToFile_Overwrite");
                // Backwards compat
                method ??= AccessTools.DeclaredMethod("Multiplayer.Client.MultiplayerSession:SaveGameToFile_Overwrite");
                // Even more backwards compat
                method ??= AccessTools.DeclaredMethod("Multiplayer.Client.MultiplayerSession:SaveGameToFile");
                if (method != null)
                {
                    MpCompat.harmony.Patch(method,
                        prefix: new HarmonyMethod(typeof(PerformanceOptimizer), nameof(PreSaveToFile)),
                        postfix: new HarmonyMethod(typeof(PerformanceOptimizer), nameof(PostSaveToFile)));
                }
                else Log.Error("Couldn't find MP SaveGameToFile method, PerformanceOptimizer will now likely cause desyncs because the patch failed.");

                // Big shoutout to NotFood for pointing me to the correct method to clear the cache in.
                // I spent hours trying to find a correct method where to clear the cache but failed.
                MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(GameComponentUtility), nameof(GameComponentUtility.FinalizeInit)),
                    postfix: new HarmonyMethod(typeof(PerformanceOptimizer), nameof(RefreshCachePrefix)));
            }

            // Late patch, needs to run after Performance Optimizer init call.
            // The usual LongEventHandler call won't work here.
            {
                MpCompat.harmony.Patch(
                    AccessTools.DeclaredMethod("PerformanceOptimizer.PerformanceOptimizerSettings:Initialize"),
                    postfix: new HarmonyMethod(typeof(PerformanceOptimizer), nameof(PostPerformanceOptimizerInitialize)));
            }
        }

        private static void PostPerformanceOptimizerInitialize()
        {
            // Separate caches from simulation and interface
            // Those caches can be called from both interface and simulation code. This causes issues
            // as the cache could end up getting recalculated from interface code and then accessed
            // from simulation code, causing a desync. Our patches add another cache for all of them,
            // and patches the methods so they access the proper cache based if it's simulation or interface.
            // This ensures that those methods benefit from caching no matter the context, while also making
            // sure that the game doesn't desync due to simulation relevant caches don't get modified from interface.
            {
                var transpiler = new HarmonyMethod(typeof(PerformanceOptimizer), nameof(SeparateCachesTranspiler));
                var optimizationsList = AccessTools.DeclaredField("PerformanceOptimizer.PerformanceOptimizerSettings:throttles").GetValue(null) as IList;

                if (optimizationsList != null)
                {
                    foreach (var optimization in optimizationsList)
                    {
                        var type = optimization.GetType();
                        var (prefixName, postfixName) = type.Name switch
                        {
                            // Exceptions to default:
                            // May seem pointless, but let's not risk issues
                            "Optimization_InspectGizmoGrid_DrawInspectGizmoGridFor" => ("GetGizmosFast", null),
                            // No postfix method
                            "Optimization_Precept_RoleMulti_RecacheActivity" => ("Prefix", null),
                            "Optimization_Precept_RoleSingle_RecacheActivity" => ("Prefix", null),
                            "Optimization_Plant_TickLong" => ("Prefix", null),
                            "Optimization_JobGiver_ConfigurableHostilityResponse" => ("Prefix", null),
                            // Different name for prefix/postfix
                            "Optimization_Building_Door_DoorRotationAt" => ("DoorRotationAtPrefix", "DoorRotationAtPostfix"),
                            // Default methods to patch
                            _ => ("Prefix", "Postfix")
                        };

                        if (prefixName != null)
                        {
                            var prefix = AccessTools.DeclaredMethod(type, prefixName);
                            if (prefix != null)
                                MpCompat.harmony.Patch(prefix, transpiler: transpiler);
                            else
                                Log.Error($"Type {type.FullName} is missing {prefixName} method, patching failed.");
                        }

                        if (postfixName != null)
                        {
                            var postfix = AccessTools.DeclaredMethod(type, postfixName);
                            if (postfix != null)
                                MpCompat.harmony.Patch(postfix, transpiler: transpiler);
                            else
                                Log.Error($"Type {type.FullName} is missing {postfixName} method, patching failed.");
                        }
                    }
                }
                else Log.Error("PerformanceOptimizer.PerformanceOptimizerSettings:throttles was null or empty, the patch was likely called too early.");
            }
        }

        #endregion

        #region Cache clearing patch

        private static FastInvokeHandler refreshCache;
        private static bool isAutoSaving = false;

        // While the game is saving, PerformanceOptimizer clears the cache.
        // However, in MP only the host is saving, but not the clients - we need to either clear for all, or for none.
        private static bool CancelIfAutosaving()
        {
            if (MP.IsInMultiplayer && MP.IsHosting)
            {
#if DEBUG
                if (Find.CurrentMap != null) 
                    Log.Message($"{(isAutoSaving ? "Autosaving" : "Refreshing cache")} at: {Find.TickManager?.TicksGame}");
#endif
                // Stop cache clearing when auto saving
                if (isAutoSaving)
                    return false;

                // Clear our custom caches
                ClearInterfaceCaches();
                return true;
            }

#if DEBUG
            if (MP.IsInMultiplayer && Find.CurrentMap != null) 
                Log.Message($"Refreshing cache at: {Find.TickManager?.TicksGame}");
#endif
            isAutoSaving = false;
            // Clear our custom caches
            ClearInterfaceCaches();
            return true;
        }

        private static void RefreshCachePrefix() => refreshCache(null);

        private static void PreSaveToFile() => isAutoSaving = true;
        private static void PostSaveToFile() => isAutoSaving = false;

        #endregion

        #region Context-sensitive caches

        private static readonly Dictionary<IDictionary, IDictionary> SimulationToInterfaceCaches = new();

        // Should the cache clearing get postfixed to the Clear() method call of the class they inject into?
        private static void ClearInterfaceCaches()
        {
            foreach (var cache in SimulationToInterfaceCaches.Values)
                cache.Clear();
        }

        private static IDictionary ReturnCorrectCache(IDictionary simulationCache)
        {
            if (simulationCache == null)
                return null;

            // If simulation, return normal cache
            if (!MP.InInterface)
                return simulationCache;

            // If interface, try to return the cache from our dictionary
            if (SimulationToInterfaceCaches.TryGetValue(simulationCache, out var interfaceCache))
                return interfaceCache;

            // This shouldn't ever run, but is here just in case something breaks.
            Log.Warning($"Trying to get interface cache for dictionary of type: {simulationCache.GetType()}");
            interfaceCache = Activator.CreateInstance(simulationCache.GetType()) as IDictionary;
            SimulationToInterfaceCaches[simulationCache] = interfaceCache;
            return interfaceCache;
        }

        private static IEnumerable<CodeInstruction> SeparateCachesTranspiler(IEnumerable<CodeInstruction> instr)
        {
            var replacementCall = AccessTools.DeclaredMethod(typeof(PerformanceOptimizer), nameof(ReturnCorrectCache));

            foreach (var ci in instr)
            {
                yield return ci;

                // Check for static is a bit pointless since the opcode is Ldsfld, but keeping it for safety anyway.
                // Checking for the field name is a bit less unreliable in case mod changes, but so far all caches use the same name.
                if (ci.opcode == OpCodes.Ldsfld && ci.operand is FieldInfo { IsStatic: true, Name: "cachedResults" } field)
                {
                    if (field.GetValue(null) is not IDictionary dict)
                        continue;

                    // Prepare the simulation-to-interface dictionary
                    if (!SimulationToInterfaceCaches.ContainsKey(dict))
                        SimulationToInterfaceCaches[dict] = Activator.CreateInstance(dict.GetType()) as IDictionary;

                    // After the field with cache was accessed, call the method that'll replace it (if needed).
                    yield return new CodeInstruction(OpCodes.Call, replacementCall);
                }
            }

            // A lot of postfixes don't access the cache (they have the value out of cache passed as __state from prefix),
            // so don't really bother logging if we haven't patched anything (unless debugging).
        }

        #endregion

        #region Time controls

        private static FastInvokeHandler doTimeControlsHotkeysMethod;

        private static bool PreDoTimeControlsGUI()
        {
            if (!MP.IsInMultiplayer)
                return true;

            doTimeControlsHotkeysMethod(null);
            return false;
        }

        #endregion
    }
}