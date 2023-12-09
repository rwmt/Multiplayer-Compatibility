using HarmonyLib;
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
                // Start insertion (0), remove all genes (1), cancel all jobs (2), engage/start (3)
                MpCompat.RegisterLambdaMethod(type, "GetGizmos", 0, 1, 2, 3);

                type = AccessTools.TypeByName("InsectoidBioengineering.GenomeListClass");
                // Select none (0), or a specific genome (2), handles all slots
                MpCompat.RegisterLambdaDelegate(type, "Process", 0, 2);
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
    }
}
