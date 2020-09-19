using System;
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
        private static bool isTournamentDialogOpen = false;
        private static FieldInfo qualityField;

        private static FieldInfo dialogNodeTreeCurrent;
        private static MethodInfo diaOptionActivate;

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
                diaOptionActivate = AccessTools.Method(typeof(DiaOption), "Activate");
                MpCompat.harmony.Patch(diaOptionActivate, prefix: new HarmonyMethod(typeof(VanillaFactionsMedieval), nameof(PreSyncDialog)));
                dialogNodeTreeCurrent = AccessTools.Field(typeof(Dialog_NodeTree), "curNode");
                MP.RegisterSyncMethod(typeof(VanillaFactionsMedieval), nameof(SyncDialog));

                var tournamentObjectType = AccessTools.TypeByName("VFEMedieval.MedievalTournament");
                MpCompat.harmony.Patch(AccessTools.Method(tournamentObjectType, "Notify_CaravanArrived"), prefix: new HarmonyMethod(typeof(VanillaFactionsMedieval), nameof(PreSyncTournament)));
            }
        }

        private static void SyncWineBarrel(SyncWorker sync, ref object obj)
        {
            if (sync.isWriting)
                sync.Write((byte)qualityField.GetValue(obj));
            else
                qualityField.SetValue(obj, sync.Read<byte>());
        }

        private static void PreSyncTournament()
        {
            // Mark the dialog as open
            if (MP.IsInMultiplayer)
                isTournamentDialogOpen = true;
        }

        private static bool PreSyncDialog(DiaOption __instance)
        {
            // Just in case
            if (!MP.IsInMultiplayer || !isTournamentDialogOpen || !(__instance.dialog is Dialog_NodeTree))
            {
                isTournamentDialogOpen = false;
                return true;
            }

            // Get the current node, find the index of the option on it, and call a (synced) method
            var currentNode = (DiaNode)dialogNodeTreeCurrent.GetValue(__instance.dialog);
            int index = currentNode.options.FindIndex(x => x == __instance);
            if (index >= 0)
                SyncDialog(__instance.dialog.optionalTitle ?? string.Empty, index);

            return false;
        }

        private static void SyncDialog(string optionalTitle, int position)
        {
            // Make sure we have the correct dialog and data
            if (position >= 0 && Find.WindowStack.IsOpen<Dialog_NodeTree>())
            {
                var dialog = Find.WindowStack.WindowOfType<Dialog_NodeTree>();

                // Check if the title (if present) matches
                if ((dialog.optionalTitle ?? string.Empty) == optionalTitle)
                {
                    isTournamentDialogOpen = false; // Prevents infinite loop, otherwise PreSyncDialog would call this method over and over again
                    var option = ((DiaNode)dialogNodeTreeCurrent.GetValue(dialog)).options[position]; // Get the correct DiaOption
                    diaOptionActivate.Invoke(option, Array.Empty<object>()); // Call the Activate method to actually "press" the button

                    if (!option.resolveTree) isTournamentDialogOpen = true; // In case dialog is still open, we mark it as such
                }
                else isTournamentDialogOpen = false;
            }
            else isTournamentDialogOpen = false;
        }

        private class DataResetComponent : GameComponent
        {
            public DataResetComponent(Game game) { }

            public override void FinalizeInit()
            {
                if (!MP.IsInMultiplayer || (isTournamentDialogOpen && !Find.WindowStack.IsOpen<Dialog_NodeTree>()))
                    isTournamentDialogOpen = false;
            }
        }
    }
}
