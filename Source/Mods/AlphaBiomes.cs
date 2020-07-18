using System.Linq;
using System.Reflection;
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
        private static FieldInfo buildingField;

        public AlphaBiomes(ModContentPack mod)
        {
            var type = AccessTools.TypeByName("AlphaBiomes.Command_SetStoneType");

            buildingField = AccessTools.Field(type, "building");
            // SyncWorker needed as the <ProcessInput> methods require syncing of the `building` field
            MP.RegisterSyncWorker<Command>(SyncSetStoneType, type, shouldConstruct: true);
            MpCompat.RegisterSyncMethodsByIndex(type, "<ProcessInput>", Enumerable.Range(0, 6).ToArray());

            var rngFixMethods = new[]
            {
                "AlphaBiomes.CompGasProducer:CompTick",
                "AlphaBiomes.TarSprayer:SteamSprayerTick",
                "AlphaBiomes.GameCondition_AcidRain:DoCellSteadyEffects",
            };

            PatchingUtilities.PatchSystemRand(AccessTools.Constructor(AccessTools.TypeByName("AlphaBiomes.CompGasProducer")), false);
            PatchingUtilities.PatchPushPopRand(rngFixMethods);
        }

        private static void SyncSetStoneType(SyncWorker sync, ref Command command)
        {
            if (sync.isWriting)
                sync.Write(buildingField.GetValue(command) as Thing);
            else
                buildingField.SetValue(command, sync.Read<Thing>());
        }
    }
}
