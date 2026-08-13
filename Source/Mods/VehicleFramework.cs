using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Vehicle Framework by Smash Phil</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3014915404"/>
    /// <remarks>
    /// The framework does vehicle pathfinding, region generation and world path cost
    /// recalculation off the main thread. All of it feeds back into the simulation, and
    /// thread scheduling differs per machine, so players end up with different results
    /// and the game desyncs.
    ///
    /// Rather than rewriting the async machinery we make it run inline in multiplayer.
    /// There are only three entry points:
    ///
    ///   SmashTools.TaskManager.Run                  every background task starts here
    ///                                               (async pathfinding, world path costs)
    ///   VehiclePathingSystem.GenerateRegionsParallel Parallel.ForEach over vehicle defs;
    ///                                               the class already has a serial version
    ///   VehicleRegionGrid.GetAllRegions             Parallel.ForEach over map cells, so
    ///                                               the resulting list order depends on
    ///                                               the scheduler
    ///
    /// Inline work costs some frame time when regions rebuild, but it is the only way
    /// every player ends up with the same result.
    /// </remarks>
    [MpCompatFor("SmashPhil.VehicleFramework")]
    public class VehicleFramework
    {
        private static FastInvokeHandler generateRegionsSerialMethod;
        private static FastInvokeHandler getRegionAtMethod;
        private static FastInvokeHandler updaterEnabledGetter;
        private static AccessTools.FieldRef<object, object> regionUpdaterField;
        private static AccessTools.FieldRef<object, object> mappingField;
        private static AccessTools.FieldRef<object, bool> regionValidField;

        public VehicleFramework(ModContentPack mod)
        {
            // --- 1. background tasks run inline
            var type = AccessTools.TypeByName("SmashTools.TaskManager");
            var runMethod = AccessTools.Method(type, "Run", new[] { typeof(Action), typeof(CancellationToken) });
            if (runMethod != null)
            {
                MpCompat.harmony.Patch(runMethod,
                    prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(PreTaskManagerRun)));
            }
            else
            {
                Log.Error("MPCompat :: Vehicle Framework - SmashTools.TaskManager:Run not found. " +
                          "Vehicle pathfinding stays asynchronous and will desync.");
            }

            // --- 2. region generation: use the serial path the mod already ships with
            type = AccessTools.TypeByName("Vehicles.VehiclePathingSystem");
            var parallelGen = AccessTools.Method(type, "GenerateRegionsParallel");
            var serialGen = AccessTools.Method(type, "GenerateRegions");
            if (parallelGen != null && serialGen != null)
            {
                generateRegionsSerialMethod = MethodInvoker.GetHandler(serialGen);
                MpCompat.harmony.Patch(parallelGen,
                    prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(PreGenerateRegionsParallel)));
            }
            else
            {
                Log.Error("MPCompat :: Vehicle Framework - VehiclePathingSystem:GenerateRegionsParallel " +
                          "or :GenerateRegions not found. Region generation stays parallel.");
            }

            // --- 3. region collection: list order must not depend on the scheduler
            type = AccessTools.TypeByName("Vehicles.VehicleRegionGrid");
            var getAllRegions = AccessTools.Method(type, "GetAllRegions");
            var getRegionAt = AccessTools.Method(type, "GetRegionAt", new[] { typeof(int) });
            var regionUpdater = AccessTools.Field(type, "regionUpdater");
            var mapping = AccessTools.Field(type, "mapping");
            var regionType = AccessTools.TypeByName("Vehicles.VehicleRegion");
            var validField = regionType != null ? AccessTools.Field(regionType, "valid") : null;

            if (getAllRegions != null && getRegionAt != null && regionUpdater != null
                && mapping != null && validField != null)
            {
                getRegionAtMethod = MethodInvoker.GetHandler(getRegionAt);
                regionUpdaterField = AccessTools.FieldRefAccess<object, object>(regionUpdater);
                mappingField = AccessTools.FieldRefAccess<object, object>(mapping);
                regionValidField = AccessTools.FieldRefAccess<object, bool>(validField);
                MpCompat.harmony.Patch(getAllRegions,
                    prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(PreGetAllRegions)));
            }
            else
            {
                Log.Error("MPCompat :: Vehicle Framework - VehicleRegionGrid members not found " +
                          "(GetAllRegions/GetRegionAt/regionUpdater/mapping/VehicleRegion.valid). " +
                          "Region list order stays non-deterministic.");
            }
        }

        /// <summary>Run the action inline instead of queueing it on the thread pool.</summary>
        private static bool PreTaskManagerRun(Action action, ref Task __result)
        {
            if (!MP.IsInMultiplayer)
                return true;

            try
            {
                action();
            }
            catch (Exception e)
            {
                Log.Error($"MPCompat :: Vehicle Framework - inline task threw an exception.\n{e}");
            }

            // Callers only check Status or wait on it, so a finished task is enough.
            __result = Task.CompletedTask;
            return false;
        }

        /// <summary>Generate vehicle regions serially, using the mod's own fallback.</summary>
        private static bool PreGenerateRegionsParallel(object __instance)
        {
            if (!MP.IsInMultiplayer)
                return true;

            generateRegionsSerialMethod(__instance);
            return false;
        }

        /// <summary>
        /// Walk the cells in index order instead of letting a partitioner hand them out
        /// across threads. Same regions, same filtering as the original - only the order
        /// becomes reproducible, which is what the other players need.
        /// </summary>
        private static bool PreGetAllRegions(object __instance, object regions)
        {
            if (!MP.IsInMultiplayer)
                return true;

            var updater = regionUpdaterField(__instance);
            if (updater == null || !(bool)updaterEnabled(updater))
                return false;

            if (!(regions is IList list))
                return true;
            if (!(mappingField(__instance) is MapComponent component) || component.map == null)
                return true;

            var cells = component.map.cellIndices.NumGridCells;
            var yielded = new HashSet<object>();
            for (var i = 0; i < cells; i++)
            {
                var region = getRegionAtMethod(__instance, i);
                if (region != null && regionValidField(region) && yielded.Add(region))
                    list.Add(region);
            }

            return false;
        }

        private static object updaterEnabled(object updater)
        {
            updaterEnabledGetter ??= MethodInvoker.GetHandler(
                AccessTools.PropertyGetter(updater.GetType(), "Enabled"));
            return updaterEnabledGetter(updater);
        }
    }
}
