using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Vanilla Genetics Expanded by Sarg, erdelf, Oskar Potocki, Luizi, Reann Shepard</summary>
    /// <see href="https://github.com/Vanilla-Expanded/VanillaGeneticsExpanded"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2801160906"/>
    [MpCompatFor("VanillaExpanded.VGeneticsE")]
    internal class VanillaGeneticsExpanded
    {
        private static AccessTools.FieldRef<object, Map> setGenomeListMap;
        private static AccessTools.FieldRef<object, Building> setGenomeListBuilding;

        public VanillaGeneticsExpanded(ModContentPack mod)
        {
            // Sync worker
            {
                var type = AccessTools.TypeByName("GeneticRim.Command_SetGenomeList");

                setGenomeListMap = AccessTools.FieldRefAccess<Map>(type, "map");
                setGenomeListBuilding = AccessTools.FieldRefAccess<Building>(type, "building");

                MP.RegisterSyncWorker<Command>(SyncCommand, type, shouldConstruct: true);
            }

            // RNG
            {
                var constructors = new[]
                {
                    "GeneticRim.CompExploder",
                    "GeneticRim.HediffComp_PeriodicWounds",
                };

                PatchingUtilities.PatchSystemRandCtor(constructors, false);
            }

            // Gizmos
            {
                // Archocentipide former - start and (dev) finish gizmos
                MpCompat.RegisterLambdaMethod("GeneticRim.Building_ArchocentipedeFormer", "GetGizmos", 0, 1)[1].SetDebugOnly();

                // Archowomb - awaken confirmation and (dev) finish gizmo
                MpCompat.RegisterLambdaMethod("GeneticRim.Building_ArchoWomb", "GetGizmos", 1, 2)[1].SetDebugOnly();

                // Mechahybridizer - (dev) finish gizmo
                MpCompat.RegisterLambdaMethod("GeneticRim.Building_Mechahybridizer", "GetGizmos", 0).SetDebugOnly();

                // Age related disease dev gizmo
                MpCompat.RegisterLambdaMethod("GeneticRim.CompApplyAgeDiseases", "GetGizmos", 0).SetDebugOnly();

                // DNA storage bank and mechahybridizer gizmos
                var type = AccessTools.TypeByName("GeneticRim.ArchotechExtractableAnimals_MapComponent");
                MP.RegisterSyncMethod(type, "AddAnimalToCarry");
                MP.RegisterSyncMethod(type, "AddParagonToCarry");

                // DNA storage bank gizmo
                MpCompat.RegisterLambdaDelegate("GeneticRim.Command_SetGenomeList", "ProcessInput", 2);
            }

            // Float menu
            {
                MpCompat.RegisterLambdaDelegate("GeneticRim.CompArchotechGrowthCell", "CompFloatMenuOptions", 0);
            }

            LongEventHandler.ExecuteWhenFinished(LatePatch);
        }

        public static void LatePatch()
        {
            // Gizmos
            {
                // DNA storage bank - (dev) fill gizmo
                MpCompat.RegisterLambdaMethod("GeneticRim.Building_DNAStorageBank", "GetGizmos", 1).SetDebugOnly();

                // Genomorpher gizmos/windows
                // Dev finish and reset all gizmos
                MpCompat.RegisterLambdaMethod("GeneticRim.CompGenomorpher", "CompGetGizmosExtra", 1, 2).SetDebugOnly();
                // Accept hybrid options from the window (opened only for one player)
                MP.RegisterSyncMethod(AccessTools.DeclaredMethod("GeneticRim.CompGenomorpher:Initialize"));

                // Electrowomb dev mode gizmo
                MpCompat.RegisterLambdaMethod("GeneticRim.CompElectroWomb", "CompGetGizmosExtra", 0).SetDebugOnly();
            }
        }

        private static void SyncCommand(SyncWorker sync, ref Command command)
        {
            if (sync.isWriting)
            {
                sync.Write(setGenomeListMap(command));
                sync.Write(setGenomeListBuilding(command));
            }
            else
            {
                setGenomeListMap(command) = sync.Read<Map>();
                setGenomeListBuilding(command) = sync.Read<Building>();
            }
        }
    }
}