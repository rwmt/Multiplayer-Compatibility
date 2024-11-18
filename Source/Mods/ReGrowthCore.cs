using Verse;
using Multiplayer.API;
using HarmonyLib;

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

            // (Dev) spawn leaves
            MpCompat.RegisterLambdaMethod("ReGrowthCore.CompLeavesSpawnerBase", "CompGetGizmosExtra", 0).SetDebugOnly();

            // RNG
            // Could be fixed by clearing the cache on join, but it affects a small graphical thing (motes). Not really worth bothering with.
            PatchingUtilities.PatchPushPopRand("ReGrowthCore.WeatherOverlay_FogMotes:TickOverlay");

            // Register the MakeCamp method to be synchronized
            var type = AccessTools.TypeByName("ReGrowthCore.Caravan_GetGizmos_Patch");
            MP.RegisterSyncMethod(type, "MakeCamp");

            MpCompat.RegisterLambdaDelegate(type, "Postfix", 0);
        }

        private static void LatePatch() => PatchingUtilities.PatchPushPopRand("ReGrowthCore.DevilDust_Tornado:ThrowDevilDustPuff");
    }
}
