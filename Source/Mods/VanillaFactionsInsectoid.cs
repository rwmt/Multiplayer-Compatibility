using System.Collections.Generic;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Vanilla Factions Expanded - Insectoids by Oskar Potocki, Sarg Bjornson, Kikohi</summary>
    /// <see href="https://github.com/Vanilla-Expanded/VanillaFactionsExpanded-Insectoids"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2149755445"/>
    [MpCompatFor("OskarPotocki.VFE.Insectoid")]
    class VanillaFactionsInsectoid
    {
        public VanillaFactionsInsectoid(ModContentPack mod)
        {
            // Gizmos
            {
                // These two methods aren't patched, but supposedly the teleporter isn't included in the mod right now due to being bugged
                // Working on them could be problematic, as it seems that they reimplement (or at least used to reimplement) vanilla caravan forming screen completely from scratch
                // VFEI.Comps.ItemComps.CompArchotechTeleporter
                // VFEI.Comps.ItemComps.CompCustomTransporter

                var type = AccessTools.TypeByName("InsectoidBioengineering.Building_BioengineeringIncubator");
                MpCompat.RegisterLambdaMethod(type, "GetGizmos", 0, 1, 2, 3);

                // Keep an eye on this in the future, seems like something the devs might combine into a single class at some point
                foreach (var geneNumber in new[] { "First", "Second", "Third" })
                {
                    type = AccessTools.TypeByName($"InsectoidBioengineering.Command_Set{geneNumber}GenomeList");
                    MP.RegisterSyncWorker<Command>(SyncSetGenomeCommand, type, shouldConstruct: true);
                    MP.RegisterSyncMethod(AccessTools.Method(type, $"TryInsert{geneNumber}Genome"));
                }
            }

            // RNG
            {
                var constructors = new[]
                {
                    "InsectoidBioengineering.Building_BioengineeringIncubator",
                    //"VFEI.CompFilthProducer",
                };

                PatchingUtilities.PatchSystemRandCtor(constructors, false);

                var methods = new[]
                {
                    "VFEI.CompTargetEffect_Tame:RandomNumber",
                };

                PatchingUtilities.PatchSystemRand(methods);
            }
        }

        private static void SyncSetGenomeCommand(SyncWorker sync, ref Command command)
        {
            var traverse = Traverse.Create(command);
            var building = traverse.Field("building");
            var genomeList = traverse.Field("genome");

            if (sync.isWriting)
            {
                sync.Write(building.GetValue() as Thing);
                var list = genomeList.GetValue() as List<Thing>;
                sync.Write(list.Count);
                foreach (var item in list)
                    sync.Write(item as Thing);
            }
            else
            {
                building.SetValue(sync.Read<Thing>());
                int count = sync.Read<int>();
                var list = new List<Thing>(count);
                for (int i = 0; i < count; i++)
                    list.Add(sync.Read<Thing>());
                genomeList.SetValue(list);
            }
        }
    }
}
