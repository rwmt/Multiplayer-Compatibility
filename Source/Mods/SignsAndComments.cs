using System.Reflection;
using HarmonyLib;
using Multiplayer.API;
using UnityEngine;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Signs and Comments by DarkFlame7</summary>
    /// <see href="https://github.com/JTJutajoh/RimWorld.Signs"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2576219203"/>
    [MpCompatFor("Dark.Signs")]
    public class SignsAndComments
    {
        private static ConstructorInfo renameSignDialogCtor;
        private static AccessTools.FieldRef<Dialog_Rename, Color> dialogColorField;
        private static AccessTools.FieldRef<Dialog_Rename, ThingComp> dialogSignCompField;

        public SignsAndComments(ModContentPack mod)
        {
            var clipboardType = AccessTools.TypeByName("Dark.Signs.CommentContentClipboard");
            MpCompat.RegisterLambdaDelegate(clipboardType, "CopyPasteGizmosFor", 0, 1); // Paste/copy

            var compType = AccessTools.TypeByName("Dark.Signs.Comp_Sign");
            // Some of those could have been skipped, but they will just end up being synced with host once a player rejoins...
            MpCompat.RegisterLambdaMethod(compType, nameof(ThingComp.CompGetGizmosExtra), 1, 2, 4); // Toggle hide, change size, apply color
            MpCompat.harmony.Patch(AccessTools.PropertyGetter(compType, "editOnPlacement"),
                postfix: new HarmonyMethod(typeof(SignsAndComments), nameof(CancelAutoOpenNotTargetedAtMe)));

            var renameDialogType = AccessTools.TypeByName("Dark.Signs.Dialog_RenameSign");

            renameSignDialogCtor = AccessTools.DeclaredConstructor(renameDialogType, new[] { compType });
            dialogColorField = AccessTools.FieldRefAccess<Color>(renameDialogType, "curColor");
            dialogSignCompField = AccessTools.FieldRefAccess<ThingComp>(renameDialogType, "signComp");

            MP.RegisterSyncMethod(renameDialogType, nameof(Dialog_Rename.SetName));
            MP.RegisterSyncWorker<Dialog_Rename>(SyncRenameSignDialog, renameDialogType);
        }

        private static void SyncRenameSignDialog(SyncWorker sync, ref Dialog_Rename dialog)
        {
            if (sync.isWriting)
            {
                sync.Write(dialogSignCompField(dialog));
                sync.Write(dialogColorField(dialog));
            }
            else
            {
                var comp = sync.Read<ThingComp>();
                var color = sync.Read<Color>();

                dialog = (Dialog_Rename)renameSignDialogCtor.Invoke(new object[] { comp });
                dialogColorField(dialog) = color;
            }
        }

        private static void CancelAutoOpenNotTargetedAtMe(ref bool __result)
        {
            if (__result && MP.IsInMultiplayer && !MP.IsExecutingSyncCommandIssuedBySelf)
                __result = false;
        }
    }
}