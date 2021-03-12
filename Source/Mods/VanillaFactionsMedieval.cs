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
                MP.RegisterSyncDialogNodeTree(AccessTools.TypeByName("VFEMedieval.MedievalTournament"), "Notify_CaravanArrived");
            }
        }

        private static void SyncWineBarrel(SyncWorker sync, ref object obj)
        {
            if (sync.isWriting)
                sync.Write((byte)qualityField.GetValue(obj));
            else
                qualityField.SetValue(obj, sync.Read<byte>());
        }

        // To be removed in the future, for now leaving it here to let it remove itself from game
        // Removing it here will prevent errors from appearing when loading a save file in the future that had this component once it's removed
        // It's not foolproof, but should prevent errors at least for the save files that were created after the change to this class
        [Obsolete("No longer needed, all related code was moved to MP API, and additionally the purpose of this code is being handled differently.")]
        private class DataResetComponent : GameComponent
        {
            private readonly Game game;

            public DataResetComponent(Game game) => this.game = game;

            public override void FinalizeInit() => game.components.RemoveAll(x => x.GetType() == typeof(DataResetComponent));
        }
    }
}
