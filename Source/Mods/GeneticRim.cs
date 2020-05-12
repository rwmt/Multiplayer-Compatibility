using System;

using HarmonyLib;
using Multiplayer.API;
using RimWorld;
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
                type = AccessTools.TypeByName("DraftingPatcher.Pawn_GetGizmos_Patch");

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
                MP.RegisterSyncMethod(AccessTools.Method("NewMachinery.ArchotechUtility:StartupHibernatingParts"));
                MP.RegisterSyncMethod(AccessTools.Method("NewMachinery.ArchotechCountdown:InitiateCountdown"));
            }
            {
                type = AccessTools.TypeByName("NewMachinery.ArchotechCountdown");

                string[] methods = {
                    "InitiateCountdown",
                    "CancelCountdown",
                    "CountdownEnded"
                };

                foreach(string method in methods) {
                    MP.RegisterSyncMethod(AccessTools.Method(type, method));
                }
            }

            // Genepod
            {
                type = AccessTools.TypeByName("NewMachinery.Building_NewGenePod");

                MP.RegisterSyncMethod(type, "<GetGizmos>b__20_0");
                MP.RegisterSyncMethod(type, "<GetGizmos>b__20_1");
            }

            // Commands
            {
                MP.RegisterSyncMethod(AccessTools.Method("NewMachinery.Command_SetGene2List:ProcessInput"));
                MP.RegisterSyncMethod(AccessTools.Method("NewMachinery.Command_SetGeneList:ProcessInput"));
            }

            // RNG patching
            {
                string[] methodsWithRand = {
                    "NewHatcher.CompHatcherRandomizer:Hatch",
                    "NewHatcher.CompIncubator:Hatch",
                    "NewHatcher.CompRecombinator:Hatch",
                    "NewHatcher.CompRecombinator:RecombinateAgain",
                    "NewHatcher.CompRecombinatorSerum:Hatch",
                };

                PatchingUtilities.PatchSystemRand(methodsWithRand);
            }
        }
    }
}
