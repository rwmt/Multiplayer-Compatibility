using System;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Genetic Rim by Sarg</summary>
    /// <remarks>Tested ship, all buildings, hybrids creation, animal orders and implants</remarks>
    /// <see href="https://github.com/juanosarg/GeneticRim"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=1113137502"/>
    [MpCompatFor("sarg.geneticrim")]
    public class GeneticRimCompat
    {
        public GeneticRimCompat(ModContentPack mod)
        {
            Type type;

            // Several Gizmos
            {
                type = AccessTools.TypeByName("GeneticRim.GeneticRim_Pawn_GetGizmos_Patch");

                string[] methods = {
                    "<AddGizmo>b__0",
                    "<AddGizmo>b__1",
                    "<AddGizmo>b__10",
                    "<AddGizmo>b__11",
                    "<AddGizmo>b__12",
                    "<AddGizmo>b__13",
                    "<AddGizmo>b__14",
                    "<AddGizmo>b__15",
                    "<AddGizmo>b__16",
                    "<AddGizmo>b__17",
                    "<AddGizmo>b__3",
                    "<AddGizmo>b__5",
                    "<AddGizmo>b__7",
                    "<AddGizmo>b__8",
                    "<AddGizmo>b__9",
                };

                foreach (string method in methods) {
                    MP.RegisterSyncDelegate(type, "<>c__DisplayClass0_0", method);
                }
            }

            // ArchotechShip startup
            {
                MP.RegisterSyncMethod(AccessTools.Method("GeneticRim.ArchotechUtility:StartupHibernatingParts"));
                MP.RegisterSyncMethod(AccessTools.Method("GeneticRim.ArchotechCountdown:InitiateCountdown"));
            }
            {
                type = AccessTools.TypeByName("GeneticRim.ArchotechCountdown");

                string[] methods = {
                    "InitiateCountdown",
                    "CancelCountdown",
                    "CountdownEnded"
                };

                foreach(string method in methods) {
                    MP.RegisterSyncMethod(AccessTools.Method(type, method));
                }
            }

            // RNG patching
            {
                string[] constructorsToPatch = {
                    "GeneticRim.CompHatcherRandomizer",
                    "GeneticRim.CompIncubator",
                    "GeneticRim.CompRecombinator",
                    "GeneticRim.CompRecombinatorSerum",
                    "GeneticRim.DeathActionWorker_Eggxplosion",
                    "GeneticRim.CompExploder",
                };

                PatchingUtilities.PatchSystemRandCtor(constructorsToPatch, false);

                string[] methodsWithRand = {
                    "GeneticRim.CompHatcherRandomizer:Hatch",
                    "GeneticRim.CompIncubator:Hatch",
                    "GeneticRim.CompRecombinator:Hatch",
                    "GeneticRim.CompRecombinator:RecombinateAgain",
                    "GeneticRim.CompRecombinatorSerum:Hatch",
                    "GeneticRim.DeathActionWorker_Eggxplosion:PawnDied",
                    "GeneticRim.CompExploder:wickInitializer",
                };

                PatchingUtilities.PatchPushPopRand(methodsWithRand);
            }
        }
    }
}
