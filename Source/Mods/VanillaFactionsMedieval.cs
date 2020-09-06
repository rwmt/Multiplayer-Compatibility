using System.Reflection;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Vanilla Factions Expanded - Medieval by Oskar Potocki, XeoNovaDan</summary>
    /// <see href="https://github.com/AndroidQuazar/VanillaFactionsExpandedMedieval"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2023513450"/>
    [MpCompatFor("OskarPotocki.VanillaFactionsExpanded.MedievalModule")]
    class VanillaFactionsMedieval
    {
        private static FieldInfo qualityField;

        public VanillaFactionsMedieval(ModContentPack mod)
        {
            // Wine gizmo
            {
                // Select wine quality
                var outerType = AccessTools.TypeByName("VFEMedieval.Command_SetTargetWineQuality");
                var innerType = AccessTools.Inner(outerType, "<>c__DisplayClass1_0");
                qualityField = AccessTools.Field(innerType, "quality");

                MpCompat.RegisterSyncMethodByIndex(innerType, "<ProcessInput>", 1);
                MP.RegisterSyncWorker<object>(SyncWineBarrel, innerType, shouldConstruct: true);
                // Debug age wine by 1 day
                MpCompat.RegisterSyncMethodByIndex(AccessTools.TypeByName("VFEMedieval.CompWineFermenter"), "<CompGetGizmosExtra>", 0).SetDebugOnly();
            }

            //// RNG
            //{
            //    var methods = new[]
            //    {
            //        "VFEMedieval.MedievalTournament:Notify_CaravanArrived",
            //        "VFEMedieval.MedievalTournament:DoTournament",
            //    };

            //    PatchingUtilities.PatchPushPopRand(methods);
            //}
        }

        private static void SyncWineBarrel(SyncWorker sync, ref object obj)
        {
            if (sync.isWriting)
                sync.Write((byte)qualityField.GetValue(obj));
            else
                qualityField.SetValue(obj, sync.Read<byte>());
        }
    }
}
