using System;
using System.Reflection;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    static class NodeTreeDialogSync
    {
        // Autosave check and reset in VanillFactionsMedieval, leaving it there as it could cause some errors on load
        public static bool isDialogOpen = false;

        private static FieldInfo dialogNodeTreeCurrent;
        private static MethodInfo diaOptionActivate;

        // For convenience
        public static HarmonyMethod HarmonyMethodMarkDialogAsOpen => new HarmonyMethod(typeof(NodeTreeDialogSync), nameof(MarkDialogAsOpen));

        public static void EnableNodeTreeDialogSync()
        {
            if (dialogNodeTreeCurrent != null) return;

            diaOptionActivate = AccessTools.Method(typeof(DiaOption), "Activate");
            MpCompat.harmony.Patch(diaOptionActivate, prefix: new HarmonyMethod(typeof(NodeTreeDialogSync), nameof(PreSyncDialog)));
            dialogNodeTreeCurrent = AccessTools.Field(typeof(Dialog_NodeTree), "curNode");
            MP.RegisterSyncMethod(typeof(NodeTreeDialogSync), nameof(SyncDialogOptionByIndex));
        }

        public static void MarkDialogAsOpen()
        {
            // Mark the dialog as open and let NodeTreeDialogSync handle it
            if (MP.IsInMultiplayer) isDialogOpen = true;
        }

        private static bool PreSyncDialog(DiaOption __instance)
        {
            // Just in case check for MP
            if (!MP.IsInMultiplayer || !isDialogOpen || !(__instance.dialog is Dialog_NodeTree))
            {
                isDialogOpen = false;
                return true;
            }

            // Get the current node, find the index of the option on it, and call a (synced) method
            var currentNode = (DiaNode)dialogNodeTreeCurrent.GetValue(__instance.dialog);
            int index = currentNode.options.FindIndex(x => x == __instance);
            if (index >= 0)
                SyncDialogOptionByIndex(__instance.dialog.optionalTitle ?? string.Empty, index);

            return false;
        }

        private static void SyncDialogOptionByIndex(string optionalTitle, int position)
        {
            // Make sure we have the correct dialog and data
            if (position >= 0 && Find.WindowStack.IsOpen<Dialog_NodeTree>())
            {
                var dialog = Find.WindowStack.WindowOfType<Dialog_NodeTree>();

                // Check if the title (if present) matches
                if ((dialog.optionalTitle ?? string.Empty) == optionalTitle)
                {
                    isDialogOpen = false; // Prevents infinite loop, otherwise PreSyncDialog would call this method over and over again
                    var option = ((DiaNode)dialogNodeTreeCurrent.GetValue(dialog)).options[position]; // Get the correct DiaOption
                    diaOptionActivate.Invoke(option, Array.Empty<object>()); // Call the Activate method to actually "press" the button

                    if (!option.resolveTree) isDialogOpen = true; // In case dialog is still open, we mark it as such
                }
                else isDialogOpen = false;
            }
            else isDialogOpen = false;
        }
    }
}
