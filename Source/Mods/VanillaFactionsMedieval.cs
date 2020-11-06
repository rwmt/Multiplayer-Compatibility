using System;
using System.Linq;
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

            // Tournament dialog
            {
                NodeTreeDialogSync.EnableNodeTreeDialogSync();
                MpCompat.harmony.Patch(AccessTools.Method(AccessTools.TypeByName("VFEMedieval.MedievalTournament"), "Notify_CaravanArrived"),
                    prefix: NodeTreeDialogSync.HarmonyMethodMarkDialogAsOpen);
            }
        }

        private static void SyncWineBarrel(SyncWorker sync, ref object obj)
        {
            if (sync.isWriting)
                sync.Write((byte)qualityField.GetValue(obj));
            else
                qualityField.SetValue(obj, sync.Read<byte>());
        }

        // Leaving it here as I believe it would cause load errors due to missing GameComponent
        private class DataResetComponent : GameComponent
        {
            public DataResetComponent(Game game) { }

            public override void FinalizeInit()
            {
                if (!MP.IsInMultiplayer || (NodeTreeDialogSync.isDialogOpen && !Find.WindowStack.IsOpen<Dialog_NodeTree>()))
                    NodeTreeDialogSync.isDialogOpen = false;
            }
        }
    }
}
