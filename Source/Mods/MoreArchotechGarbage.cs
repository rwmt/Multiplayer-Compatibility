using System;
using HarmonyLib;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>More Archotech Garbage by MrKociak</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2391102796"/>
    [MpCompatFor("MrKociak.MoreArchotechStuffandThingsReupload")]
    internal class MoreArchotechGarbage
    {
        public MoreArchotechGarbage(ModContentPack mod)
        {
            // RNG Fix
            {
                var rngTypes = new[]
                {
                    "RimWorld.CompSpawnerH",
                    "RimWorld.CompSpawnerResearch",
                    "RimWorld.CompSpawnerResearchMK2",
                    "RimWorld.JoinOrRaidTameOrManhunter",
                    "RimWorld.MechhSummon",
                    "RimWorld.MechhSummonNoRoyalty",
                };

                foreach (var type in rngTypes) PatchingUtilities.PatchSystemRand($"{type}:RandomNumber");
            }

            // Gizmo Fix
            {
                // Mech cluster (0), raid (2), ship part (4)
                var type = AccessTools.TypeByName("RimWorld.MechhSummon");
                MpCompat.RegisterLambdaDelegate(type, "GetGizmos", Array.Empty<string>(), 0, 2);
                MpCompat.RegisterLambdaMethod(type, "GetGizmos", 4);

                // Raid (0), ship part (2)
                type = AccessTools.TypeByName("RimWorld.MechhSummonNoRoyalty");
                MpCompat.RegisterLambdaDelegate(type, "GetGizmos", Array.Empty<string>(), 0);
                MpCompat.RegisterLambdaMethod(type, "GetGizmos", 2);

                // Summon meteorite
                MpCompat.RegisterLambdaDelegate("RimWorld.PodSummoner", "GetGizmos", Array.Empty<string>(), 0);

                // Dev mode gizmos
                MpCompat.RegisterLambdaMethod("RimWorld.CompSpawnerH", "CompGetGizmosExtra", 0).SetDebugOnly();
                MpCompat.RegisterLambdaMethod("RimWorld.CompSpawnerResearch", "CompGetGizmosExtra", 0).SetDebugOnly();
                MpCompat.RegisterLambdaMethod("RimWorld.CompSpawnerResearchMK2", "CompGetGizmosExtra", 0).SetDebugOnly();
            }
        }
    }
}
