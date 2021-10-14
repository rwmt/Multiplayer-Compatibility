using System.Runtime.Serialization;
using Multiplayer.API;
using RimFridge;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>RimFridge by KiameV</summary>
    /// <remarks>Fixes for gizmos</remarks>
    /// <see href="https://github.com/KiameV/rimworld-rimfridge"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=1180721235"/>
    [MpCompatFor("rimfridge.kv.rw")]
    public class RimFridge
    {
        public RimFridge(ModContentPack mod)
        {
            MpCompat.RegisterLambdaDelegate(typeof(CompRefrigerator), nameof(CompRefrigerator.CompGetGizmosExtra), 1, 2, 3, 4, 5);
            MpCompat.RegisterLambdaMethod(typeof(CompToggleGlower), nameof(CompToggleGlower.CompGetGizmosExtra), 0);

            MP.RegisterSyncWorker<Dialog_RenameFridge>(SyncDialog);
            MP.RegisterSyncMethod(typeof(Dialog_RenameFridge), nameof(Dialog_RenameFridge.SetName));
        }

        private static void SyncDialog(SyncWorker sync, ref Dialog_RenameFridge dialog)
        {
            if (sync.isWriting)
                sync.Write(dialog.fridge);
            else
            {
                dialog = (Dialog_RenameFridge)FormatterServices.GetUninitializedObject(typeof(Dialog_RenameFridge));
                dialog.fridge = sync.Read<CompRefrigerator>();
            }
        }
    }
}
