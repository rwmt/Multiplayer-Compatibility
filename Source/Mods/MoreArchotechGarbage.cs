using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>More Archotech Garbage by MrKociak</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2391102796"/>
    [MpCompatFor("MrKociak.MoreArchotechStuffandThingsReupload")]
    [MpCompatFor("Taranchuk.MoreArchotechGarbageContinued")]
    [MpCompatFor("zal.morearchotechgarbage")]
    internal class MoreArchotechGarbage
    {
        public MoreArchotechGarbage(ModContentPack mod)
        {
            // RNG Fix
            {
                var rngTypes = new[]
                {
                    "MoreArchotechGarbage.CompSpawnerH",
                    "MoreArchotechGarbage.CompSpawnerResearch",
                    "MoreArchotechGarbage.CompSpawnerResearchMK2",
                    "MoreArchotechGarbage.JoinOrRaidTameOrManhunter",
                    "MoreArchotechGarbage.MechhSummon",
                    "MoreArchotechGarbage.MechhSummonNoRoyalty",
                    // Unused?
                    "MoreArchotechGarbage.CompSpawnerExtraRaidsOne",
                    "MoreArchotechGarbage.CompSpawnerExtraRaidsTwo",
                    "MoreArchotechGarbage.CompSpawnerExtraRaidsThree",
                };

                foreach (var type in rngTypes) PatchingUtilities.PatchSystemRand($"{type}:RandomNumber");
            }

            // Gizmo Fix
            {
                // Mech cluster (0), raid (2), ship part (4)
                var type = AccessTools.TypeByName("MoreArchotechGarbage.MechhSummon");
                MpCompat.RegisterLambdaDelegate(type, nameof(Building.GetGizmos), 0, 2);
                MpCompat.RegisterLambdaMethod(type, nameof(Building.GetGizmos), 4);

                // Raid (0), ship part (2)
                type = AccessTools.TypeByName("MoreArchotechGarbage.MechhSummonNoRoyalty");
                MpCompat.RegisterLambdaDelegate(type, nameof(Building.GetGizmos), 0);
                MpCompat.RegisterLambdaMethod(type, nameof(Building.GetGizmos), 2);

                // Summon meteorite
                MpCompat.RegisterLambdaDelegate("MoreArchotechGarbage.PodSummoner", nameof(Building.GetGizmos), 0);

                // Dev mode gizmos
                MpCompat.RegisterLambdaMethod("MoreArchotechGarbage.CompSpawnerH", nameof(ThingComp.CompGetGizmosExtra), 0).SetDebugOnly();
                MpCompat.RegisterLambdaMethod("MoreArchotechGarbage.CompSpawnerResearch", nameof(ThingComp.CompGetGizmosExtra), 0).SetDebugOnly();
                MpCompat.RegisterLambdaMethod("MoreArchotechGarbage.CompSpawnerResearchMK2", nameof(ThingComp.CompGetGizmosExtra), 0).SetDebugOnly();
                MpCompat.RegisterLambdaMethod("MoreArchotechGarbage.CompSpawnerExtraRaidsOne", nameof(ThingComp.CompGetGizmosExtra), 0).SetDebugOnly();
                MpCompat.RegisterLambdaMethod("MoreArchotechGarbage.CompSpawnerExtraRaidsTwo", nameof(ThingComp.CompGetGizmosExtra), 0).SetDebugOnly();
                MpCompat.RegisterLambdaMethod("MoreArchotechGarbage.CompSpawnerExtraRaidsThree", nameof(ThingComp.CompGetGizmosExtra), 0).SetDebugOnly();

                // Summon a person to join your colony (0), tame a random animal nearby (3)
                MpCompat.RegisterLambdaMethod("MoreArchotechGarbage.JoinOrRaidTameOrManhunter", nameof(Building.GetGizmos), 0, 3);

                // Start carging
                MP.RegisterSyncMethod(AccessTools.DeclaredMethod("MoreArchotechGarbage.Building_MAG_HibernationStarter:MAG_StartupHibernatingParts"));
            }
        }
    }
}
