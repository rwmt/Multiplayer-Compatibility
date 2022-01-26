using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Multiplayer.API;
using UnityEngine;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Vanilla Factions Expanded - Vikings by Oskar Potocki, Erin, Sarg Bjornson, erdelf, Kikohi, Taranchuk, Helixien, Chowder</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2231295285"/>
    /// <see href="https://github.com/AndroidQuazar/Vanilla-Factions-Expanded-Vikings"/>
    [MpCompatFor("OskarPotocki.VFE.Vikings")]
    class VanillaFactionsVikings
    {
        private static Type changeFacepaintDialogType;
        private static IList orderedFacepaintDefs;
        private static ISyncField newFacepaintComboSync;
        private static ISyncField coloursTiedSync;

        private static FieldInfo facepaintDefOneField;
        private static FieldInfo facepaintDefTwoField;
        private static FieldInfo facepaintColourOneField;
        private static FieldInfo facepaintColourTwoField;

        public VanillaFactionsVikings(ModContentPack mod)
        {
            LongEventHandler.ExecuteWhenFinished(LatePatch);
            
            // Debug stuff
            var type = AccessTools.TypeByName("VFEV.Apiary");
            MpCompat.RegisterLambdaMethod(type, "GetGizmos", 0, 1).SetDebugOnly();

            // This method seems unused... But I guess it's better to be safe than sorry.
            PatchingUtilities.PatchSystemRand(AccessTools.Method(type, "ResetTend"), false);
        }

        public void LatePatch()
        {
            // Facepaint
            changeFacepaintDialogType = AccessTools.TypeByName("VFEV.Facepaint.Dialog_ChangeFacepaint");
            orderedFacepaintDefs = (IList)AccessTools.Field(changeFacepaintDialogType, "orderedFacepaintDefs").GetValue(null);

            newFacepaintComboSync = MP.RegisterSyncField(AccessTools.Field(changeFacepaintDialogType, "newFacepaintCombo"));
            coloursTiedSync = MP.RegisterSyncField(AccessTools.Field(changeFacepaintDialogType, "coloursTied"));
            MP.RegisterSyncMethod(changeFacepaintDialogType, "SetHairstyle");
            MP.RegisterSyncWorker<Window>(SyncDialog, changeFacepaintDialogType);

            MP.RegisterSyncMethod(typeof(VanillaFactionsVikings), nameof(SyncedTryRemoveWindow));

            MpCompat.harmony.Patch(AccessTools.Method(changeFacepaintDialogType, "DoWindowContents"),
                prefix: new HarmonyMethod(typeof(VanillaFactionsVikings), nameof(PreDoWindowContents)),
                postfix: new HarmonyMethod(typeof(VanillaFactionsVikings), nameof(PostDoWindowContents)));

            var type = AccessTools.Inner(changeFacepaintDialogType, "FacepaintCombination");
            MP.RegisterSyncWorker<object>(SyncFacepaintCombination, type);

            facepaintDefOneField = AccessTools.Field(type, "facepaintDefOne");
            facepaintDefTwoField = AccessTools.Field(type, "facepaintDefTwo");
            facepaintColourOneField = AccessTools.Field(type, "colourOne");
            facepaintColourTwoField = AccessTools.Field(type, "colourTwo");

            MpCompat.harmony.Patch(AccessTools.Method(typeof(WindowStack), nameof(WindowStack.TryRemove), new[] { typeof(Window), typeof(bool) }),
                prefix: new HarmonyMethod(typeof(VanillaFactionsVikings), nameof(PreTryRemoveWindow)));
        }

        private static void PreDoWindowContents(Window __instance)
        {
            if (!MP.IsInMultiplayer)
                return;
            
            MP.WatchBegin();
            newFacepaintComboSync.Watch(__instance);
            coloursTiedSync.Watch();
        }

        private static void PostDoWindowContents()
        {
            if (!MP.IsInMultiplayer)
                return;

            MP.WatchEnd();
        }

        private static bool PreTryRemoveWindow(Window window)
        {
            // Let the method run only if it's synced call
            if (!MP.IsInMultiplayer || MP.IsExecutingSyncCommand || window.GetType() != changeFacepaintDialogType)
                return true;

            SyncedTryRemoveWindow();
            return false;
        }

        private static void SyncedTryRemoveWindow()
        {
            Find.WindowStack.TryRemove(changeFacepaintDialogType);
        }

        private static void SyncFacepaintCombination(SyncWorker sync, ref object obj)
        {
            if (sync.isWriting)
            {
                sync.Write((Color)facepaintColourOneField.GetValue(obj));
                sync.Write((Color)facepaintColourTwoField.GetValue(obj));
                sync.Write(orderedFacepaintDefs.IndexOf(facepaintDefOneField.GetValue(obj)));
                sync.Write(orderedFacepaintDefs.IndexOf(facepaintDefTwoField.GetValue(obj)));
            }
            else
            {
                facepaintColourOneField.SetValue(obj, sync.Read<Color>());
                facepaintColourTwoField.SetValue(obj, sync.Read<Color>());
                var index = sync.Read<int>();
                if (index >= 0)
                    facepaintDefOneField.SetValue(obj, (Def)orderedFacepaintDefs[index]);
                index = sync.Read<int>();
                if (index >= 0)
                    facepaintDefTwoField.SetValue(obj, (Def)orderedFacepaintDefs[index]);
            }
        }

        private static void SyncDialog(SyncWorker sync, ref Window dialog)
        {
            if (!sync.isWriting)
            {
                if (Find.WindowStack.IsOpen(changeFacepaintDialogType))
                    dialog = Find.WindowStack.Windows.First(x => x.GetType() == changeFacepaintDialogType);
            }
        }
    }
}
