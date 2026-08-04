using System;
using System.Collections.Generic;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Turn It On and Off - RePowered by Mlie</summary>
    /// <see href="https://github.com/emipa606/TurnOnOffRePowered"/>
    /// <remarks>
    /// The mod tracks bench usage from Building_WorkTable.UsedThisTick (marked during map ticks)
    /// and consumes the marks in a GameComponentTick loop (which runs under the world clock).
    /// That relies on vanilla's strict one-world-tick-then-one-map-tick interleave. MP ticks the
    /// world and each map as separate tickables and, above 1x speed, runs each tickable's ticks
    /// back to back (TickPatch.DoTick) — so consecutive world ticks rotate the usage list with no
    /// map tick in between, leaving benches permanently "idle". The loop also keys its rescan
    /// cadence off Find.CurrentMap (whichever map the local player is viewing), a client-local
    /// input feeding power state — an instant desync.
    ///
    /// Fix, in MP only:
    /// 1. Stamp each usage mark with the building's own map tick (AddBuildingUsed runs inside
    ///    that map's tick, so TicksGame is the right clock there) and prune stale stamps once per
    ///    map tick (MapPostTick, same reasoning). A mark stays fresh for a couple of the map's
    ///    own ticks, which makes it immune to how many world ticks run in a row in between.
    /// 2. After the mod rotates its usage list (BeginUsageTick), union the still-fresh stamped
    ///    buildings back in, so back-to-back rotations can't empty it.
    /// 3. Replace the GameComponentTick driver body with one that keys the rescan cadence off
    ///    the colonist building count summed across all maps instead of Find.CurrentMap.
    /// Everything the driver calls reads synced simulation state only, and MP's tickable
    /// interleave is itself deterministic, so the loop stays in lockstep across clients.
    ///
    /// Note: the mod feeds Settings.customPowerValues (client-local mod settings) into power
    /// levels; players still need identical mod settings, as with most mods of this kind.
    /// </remarks>
    [MpCompatFor("Mlie.TurnOnOffRePowered")]
    public class TurnOnOffRePowered
    {
        #region Fields

        // Mirrors TurnOnOffGameComponent's private constants
        private const int FastEvaluationInterval = 4;
        private const int PowerReconciliationInterval = 60;
        private const int RegularEvaluationInterval = 15;
        private const int RescanInterval = 2000;
        private const int SlowEvaluationInterval = 60;

        // How many of its own map's ticks a usage mark stays fresh. Benches in use are re-marked
        // every map tick, and the map clock doesn't advance while the world clock runs its batch,
        // so 2 covers the worst case without noticeably delaying the drop back to idle power.
        private const int UsageMarkGraceTicks = 2;

        private static FastInvokeHandler beginUsageTick;
        private static FastInvokeHandler scanForThings;
        private static FastInvokeHandler refreshFastBuildingUsage;
        private static FastInvokeHandler refreshRegularBuildingUsage;
        private static FastInvokeHandler refreshRimfactoryBuildingUsage;
        private static FastInvokeHandler refreshSlowBuildingUsage;
        private static FastInvokeHandler mergePolledBuildingUsage;
        private static FastInvokeHandler applyPowerStateChanges;
        private static FastInvokeHandler reconcilePowerStates;
        // The field is static readonly and only ever cleared, never reassigned, so capturing the
        // instance once is safe.
        private static HashSet<Building> buildingsThatWereUsedLastTick;

        // building -> its own map's tick when it was last marked used
        private static readonly Dictionary<Building, int> usageStamps = new();
        private static readonly List<Building> staleStampsScratch = new();

        // Replacement driver state, mirrors TurnOnOffGameComponent's fields
        private static int inUseTick;
        private static int lastTotalBuildings = -1;
        private static int nextFastEvaluationTick;
        private static int nextPowerReconciliationTick;
        private static int nextRegularEvaluationTick;
        private static int nextSlowEvaluationTick;
        private static int ticksToRescan;

        #endregion

        #region Main patch

        public TurnOnOffRePowered(ModContentPack mod)
        {
            var type = AccessTools.TypeByName("TurnOnOffRePowered.TurnItOnUtility");

            beginUsageTick = MethodInvoker.GetHandler(AccessTools.DeclaredMethod(type, "BeginUsageTick"));
            scanForThings = MethodInvoker.GetHandler(AccessTools.DeclaredMethod(type, "ScanForThings"));
            refreshFastBuildingUsage = MethodInvoker.GetHandler(AccessTools.DeclaredMethod(type, "RefreshFastBuildingUsage"));
            refreshRegularBuildingUsage = MethodInvoker.GetHandler(AccessTools.DeclaredMethod(type, "RefreshRegularBuildingUsage"));
            refreshRimfactoryBuildingUsage = MethodInvoker.GetHandler(AccessTools.DeclaredMethod(type, "RefreshRimfactoryBuildingUsage"));
            refreshSlowBuildingUsage = MethodInvoker.GetHandler(AccessTools.DeclaredMethod(type, "RefreshSlowBuildingUsage"));
            mergePolledBuildingUsage = MethodInvoker.GetHandler(AccessTools.DeclaredMethod(type, "MergePolledBuildingUsage"));
            applyPowerStateChanges = MethodInvoker.GetHandler(AccessTools.DeclaredMethod(type, "ApplyPowerStateChanges"));
            reconcilePowerStates = MethodInvoker.GetHandler(AccessTools.DeclaredMethod(type, "ReconcilePowerStates"));
            buildingsThatWereUsedLastTick = AccessTools.StaticFieldRefAccess<HashSet<Building>>(type, "buildingsThatWereUsedLastTick");

            MpCompat.harmony.Patch(AccessTools.DeclaredMethod(type, "AddBuildingUsed"),
                postfix: new HarmonyMethod(typeof(TurnOnOffRePowered), nameof(StampBuildingUsed)));
            MpCompat.harmony.Patch(AccessTools.DeclaredMethod(type, "BeginUsageTick"),
                postfix: new HarmonyMethod(typeof(TurnOnOffRePowered), nameof(RestoreFreshlyUsedBuildings)));
            // The mod calls this when (re)initializing its state for a game, clear ours with it
            MpCompat.harmony.Patch(AccessTools.DeclaredMethod(type, "ClearVariables"),
                postfix: new HarmonyMethod(typeof(TurnOnOffRePowered), nameof(ClearState)));

            MpCompat.harmony.Patch(AccessTools.DeclaredMethod("TurnOnOffRePowered.TurnOnOffGameComponent:GameComponentTick"),
                prefix: new HarmonyMethod(typeof(TurnOnOffRePowered), nameof(ReplaceDriverInMp)));

            MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(Map), nameof(Map.MapPostTick)),
                postfix: new HarmonyMethod(typeof(TurnOnOffRePowered), nameof(PruneStaleUsageStamps)));
        }

        #endregion

        #region Usage stamps

        // Postfix on TurnItOnUtility.AddBuildingUsed. Only ever called while the building's own
        // map is ticking (Building_WorkTable.UsedThisTick, JobDriver_WatchBuilding), so
        // TicksGame is that map's clock here under MP's per-map time contexts.
        private static void StampBuildingUsed(Building building)
        {
            if (!MP.IsInMultiplayer || building?.Map == null)
                return;

            usageStamps[building] = Find.TickManager.TicksGame;
        }

        // Postfix on TurnItOnUtility.BeginUsageTick. The rotation only carries marks made since
        // the previous rotation, which is an empty set for the second and later of MP's
        // back-to-back world ticks — re-add everything still fresh by stamp.
        private static void RestoreFreshlyUsedBuildings()
        {
            if (!MP.IsInMultiplayer)
                return;

            foreach (var building in usageStamps.Keys)
                buildingsThatWereUsedLastTick.Add(building);
        }

        // Postfix on Map.MapPostTick, so stamps age on the clock they were taken from. Also
        // drops stamps of despawned/destroyed buildings, whichever map's pass sees them first.
        private static void PruneStaleUsageStamps(Map __instance)
        {
            if (!MP.IsInMultiplayer || usageStamps.Count == 0)
                return;

            var currentTick = Find.TickManager.TicksGame;
            staleStampsScratch.Clear();

            foreach (var (building, stampedTick) in usageStamps)
            {
                if (building?.Map == null)
                    staleStampsScratch.Add(building);
                else if (building.Map == __instance && currentTick - stampedTick > UsageMarkGraceTicks)
                    staleStampsScratch.Add(building);
            }

            foreach (var building in staleStampsScratch)
                usageStamps.Remove(building);
            staleStampsScratch.Clear();
        }

        private static void ClearState()
        {
            usageStamps.Clear();
            inUseTick = 0;
            lastTotalBuildings = -1;
            nextFastEvaluationTick = 0;
            nextPowerReconciliationTick = 0;
            nextRegularEvaluationTick = 0;
            nextSlowEvaluationTick = 0;
            ticksToRescan = 0;
        }

        #endregion

        #region Driver

        // Prefix on TurnOnOffGameComponent.GameComponentTick. In MP, replaces the body with the
        // same loop minus its Find.CurrentMap dependencies (early-out when no map is viewed, and
        // rescan cadence keyed to the viewed map's building count — both client-local inputs
        // that fed power state, i.e. desyncs).
        private static bool ReplaceDriverInMp()
        {
            if (!MP.IsInMultiplayer)
                return true;

            try
            {
                var currentTick = Find.TickManager.TicksGame;
                if (inUseTick == 0)
                {
                    inUseTick = currentTick;
                    return false;
                }

                if (inUseTick == currentTick)
                    return false;

                inUseTick = currentTick;
                beginUsageTick(null);

                var totalBuildings = 0;
                foreach (var map in Find.Maps)
                    totalBuildings += map.listerBuildings.allBuildingsColonist.Count;

                if (totalBuildings != lastTotalBuildings)
                {
                    lastTotalBuildings = totalBuildings;
                    ticksToRescan = 0;
                }

                --ticksToRescan;
                if (ticksToRescan < 0)
                {
                    ticksToRescan = RescanInterval;
                    scanForThings(null);
                }

                if (currentTick >= nextFastEvaluationTick)
                {
                    nextFastEvaluationTick = currentTick + FastEvaluationInterval;
                    refreshFastBuildingUsage(null);
                }

                if (currentTick >= nextRegularEvaluationTick)
                {
                    nextRegularEvaluationTick = currentTick + RegularEvaluationInterval;
                    refreshRegularBuildingUsage(null);
                    refreshRimfactoryBuildingUsage(null);
                }

                if (currentTick >= nextSlowEvaluationTick)
                {
                    nextSlowEvaluationTick = currentTick + SlowEvaluationInterval;
                    refreshSlowBuildingUsage(null);
                }

                mergePolledBuildingUsage(null);
                applyPowerStateChanges(null);

                if (currentTick >= nextPowerReconciliationTick)
                {
                    nextPowerReconciliationTick = currentTick + PowerReconciliationInterval;
                    reconcilePowerStates(null);
                }
            }
            catch (Exception exception)
            {
                Log.Error($"[MpCompat] TurnOnOffRePowered driver exception: {exception}");
            }

            return false;
        }

        #endregion
    }
}
