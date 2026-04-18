using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Verse;
using Multiplayer.API;
using HarmonyLib;
using RimWorld.Planet;

namespace Multiplayer.Compat
{
    /// <summary>ReGrowth: Core by Helixien, Taranchuk</summary>
    /// <see href="https://github.com/Helixien/ReGrowth-Core"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2260097569"/>
    [MpCompatFor("ReGrowth.BOTR.Core")]
    public class ReGrowthCore
    {
        private static Type smartFarmingCompType;
        private static FieldInfo compCacheField;
        private static FieldInfo growZoneRegistryField;

        public ReGrowthCore(ModContentPack mod)
        {
            LongEventHandler.ExecuteWhenFinished(LatePatch);
            MpCompatPatchLoader.LoadPatch(this);

            // (Dev) spawn leaves
            MpCompat.RegisterLambdaMethod("ReGrowthCore.CompLeavesSpawnerBase", "CompGetGizmosExtra", 0).SetDebugOnly();

            // RNG
            // Could be fixed by clearing the cache on join, but it affects a small graphical thing (motes). Not really worth bothering with.
            PatchingUtilities.PatchPushPopRand("ReGrowthCore.WeatherOverlay_FogMotes:TickOverlay");
            // Rain splash flecks use CreateFleck which calls Verse.Rand internally
            PatchingUtilities.PatchPushPopRand("ReGrowthCore.SplashesUtility:ProcessSplashes");
            // ColdGlow fires only on the client viewing the map, causing Rand divergence every tick in cold rooms
            PatchingUtilities.PatchPushPopRand("ReGrowthCore.Patch_DoCellSteadyEffects:ColdGlow");

            // Visit abandoned camp gizmo (caravan arrives at AbandonedCamp world object)
            // Only sync the gizmo delegate; Visit is also called by CaravanArrivalAction server-side
            MpCompat.RegisterLambdaDelegate("ReGrowthCore.AbandonedCamp", "SetupCampGizmo", 0);

            // SmartFarming grow zone gizmos (ZoneData.Init lambdas):
            // 0: sowGizmo.action, 1: priorityGizmo.action,
            // 3: pettyJobsGizmo.toggleAction, 5: allowHarvestGizmo.toggleAction,
            // 6: harvestGizmo.action, 8: orchardGizmo.toggleAction
            smartFarmingCompType = AccessTools.TypeByName("ReGrowthCore.MapComponent_SmartFarming");
            compCacheField = AccessTools.Field(AccessTools.TypeByName("ReGrowthCore.ReGrowthCore_SmartFarming"), "compCache");
            growZoneRegistryField = AccessTools.Field(smartFarmingCompType, "growZoneRegistry");
            MP.RegisterSyncWorker<object>(SyncZoneData, AccessTools.TypeByName("ReGrowthCore.ZoneData"), shouldConstruct: false);
            MP.RegisterSyncWorker<object>(SyncSmartFarmingComp, smartFarmingCompType, shouldConstruct: false);
            MpCompat.RegisterLambdaDelegate("ReGrowthCore.ZoneData", "Init", 0, 1, 3, 5, 6, 8);
            // Multi-zone gizmos: 1=batch sow, 2=batch priority, 4=merge confirmation callback (3 just opens dialog)
            MpCompat.RegisterLambdaDelegate("ReGrowthCore.Patch_GetGizmos", "GetMultiZoneGizmos", 1, 2, 4).SetContext(SyncContext.MapSelected);

            var caravanCampType = AccessTools.TypeByName("ReGrowthCore.CaravanCamp");
            var postfix = new HarmonyMethod(typeof(ReGrowthCore), nameof(PostCaravanCompTick));
            var tick = AccessTools.DeclaredMethod(caravanCampType, "Tick");
            var tickInterval = AccessTools.DeclaredMethod(caravanCampType, "TickInterval");
            if (tick != null) MpCompat.harmony.Patch(tick, postfix: postfix);
            if (tickInterval != null) MpCompat.harmony.Patch(tickInterval, postfix: postfix);
        }

        private static void SyncSmartFarmingComp(SyncWorker sync, ref object comp)
        {
            if (sync.isWriting)
                sync.Write(((MapComponent)comp).map);
            else
            {
                var map = sync.Read<Map>();
                comp = map?.components.FirstOrDefault(c => c.GetType() == smartFarmingCompType);
            }
        }

        private static void SyncZoneData(SyncWorker sync, ref object zoneData)
        {
            if (sync.isWriting)
            {
                Map foundMap = null;
                int foundZoneId = -1;
                var compCache = (IDictionary)compCacheField.GetValue(null);
                foreach (DictionaryEntry entry in compCache)
                {
                    var registry = (IDictionary)growZoneRegistryField.GetValue(entry.Value);
                    foreach (DictionaryEntry kv in registry)
                    {
                        if (kv.Value != zoneData) continue;
                        foundMap = ((MapComponent)entry.Value).map;
                        foundZoneId = (int)kv.Key;
                        break;
                    }
                    if (foundMap != null) break;
                }
                sync.Write(foundMap);
                sync.Write(foundZoneId);
            }
            else
            {
                var map = sync.Read<Map>();
                var zoneId = sync.Read<int>();
                if (map == null) return;
                var comp = map.components.FirstOrDefault(c => c.GetType() == smartFarmingCompType);
                var registry = (IDictionary)growZoneRegistryField.GetValue(comp);
                zoneData = registry[(object)zoneId];
            }
        }

        private static void LatePatch() => PatchingUtilities.PatchPushPopRand("ReGrowthCore.DevilDust_Tornado:ThrowDevilDustPuff");

        private static void PostCaravanCompTick(MapParent __instance)
        {
            // MP has a multifaction patch to prevent maps from being automatically
            // removed as long as they belong to any player facionts. This is not
            // that big of a deal MP + vanilla only, but with other mods it can cause
            // situations where a temporary map (like this one) is prevented from
            // being remove due to belonging to one of the player.
            // 
            // The patch here is to basically remove the faction from the map once
            // the map should be removed naturally, allowing the map to be removed.
            // Also, by having no faction, the world object won't have associated
            // player faction's color.

            // Make sure there is owner and it's a player faction.
            if (__instance.Faction?.IsPlayer != true)
                return;

            // If there's no map there's no point in having owner faction.
            // If there's a map, check if the map should be removed.
            // If yes, set the faction to null to allow the map to
            // be removed, leaving "abandoned camp" object behind.
            if (!__instance.HasMap || __instance.ShouldRemoveMapNow(out _))
                __instance.SetFaction(null);
        }
    }
}