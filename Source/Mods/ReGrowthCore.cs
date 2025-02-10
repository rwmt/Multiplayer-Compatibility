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
        public ReGrowthCore(ModContentPack mod)
        {
            LongEventHandler.ExecuteWhenFinished(LatePatch);
            MpCompatPatchLoader.LoadPatch(this);

            // (Dev) spawn leaves
            MpCompat.RegisterLambdaMethod("ReGrowthCore.CompLeavesSpawnerBase", "CompGetGizmosExtra", 0).SetDebugOnly();

            // RNG
            // Could be fixed by clearing the cache on join, but it affects a small graphical thing (motes). Not really worth bothering with.
            PatchingUtilities.PatchPushPopRand("ReGrowthCore.WeatherOverlay_FogMotes:TickOverlay");

            // Register the MakeCamp method to be synchronized
            var type = AccessTools.TypeByName("ReGrowthCore.Caravan_GetGizmos_Patch");
            MP.RegisterSyncMethod(type, "MakeCamp");
        }

        private static void LatePatch() => PatchingUtilities.PatchPushPopRand("ReGrowthCore.DevilDust_Tornado:ThrowDevilDustPuff");

        [MpCompatPostfix("ReGrowthCore.CaravanCamp", nameof(Site.Tick))]
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