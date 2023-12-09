using System.Linq;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Alpha Biomes by Sarg Bjornson</summary>
    /// <see href="https://github.com/juanosarg/AlphaBiomes"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=1841354677"/>
    [MpCompatFor("sarg.alphabiomes")]
    class AlphaBiomes
    {
        private static AccessTools.FieldRef<object, Building> buildingField;

        public AlphaBiomes(ModContentPack mod)
        {
            var type = AccessTools.TypeByName("AlphaBiomes.Command_SetStoneType");

            buildingField = AccessTools.FieldRefAccess<Building>(type, "building");
            // SyncWorker needed as the <ProcessInput> methods require syncing of the `building` field
            MP.RegisterSyncWorker<Command>(SyncSetStoneType, type, shouldConstruct: true);
            MpCompat.RegisterLambdaMethod(type, "ProcessInput", Enumerable.Range(0, 6).ToArray());

            var rngFixMethods = new[]
            {
                "AlphaBiomes.CompGasProducer:CompTick",
                "AlphaBiomes.TarSprayer:SteamSprayerTick",
                // AlphaBiomes.TarSprayer:ThrowAirPuffUp - only contained by above method
                "AlphaBiomes.GameCondition_AcidRain:DoCellSteadyEffects",
            };

            var systemRngFixConstructor = new[]
            {
                "AlphaBiomes.CompGasProducer",
                "AlphaBiomes.HediffComp_GangreneWounds",
            }.Select(x => AccessTools.DeclaredConstructor(AccessTools.TypeByName(x)));

            PatchingUtilities.PatchSystemRand(systemRngFixConstructor, false);
            PatchingUtilities.PatchPushPopRand(rngFixMethods);
        }

        private static void SyncSetStoneType(SyncWorker sync, ref Command command)
        {
            if (sync.isWriting)
                sync.Write(buildingField(command));
            else
                buildingField(command) = sync.Read<Building>();
        }
    }
}
