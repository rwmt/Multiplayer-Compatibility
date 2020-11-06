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
    /// <summary>Vanilla Hair Expanded by Oskar Potocki, XeoNovaDan, MonteCristo</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=1888705256"/>
    [MpCompatFor("VanillaExpanded.VHE")]
    class VanillaHairExpanded
    {
        private static bool isSyncedCall = false;

        private static Type changeHairstyleDialogType;
        private static FieldInfo orderedHairstyleDefsField;
        private static ISyncField newHairstyleComboSync;
        private static ISyncField coloursTiedSync;

        private static FieldInfo hairDefField;
        private static FieldInfo beardDefField;
        private static FieldInfo hairColourField;
        private static FieldInfo beardColourField;

        public VanillaHairExpanded(ModContentPack mod) => LongEventHandler.ExecuteWhenFinished(LatePatch);

        public void LatePatch()
        {
            changeHairstyleDialogType = AccessTools.TypeByName("VanillaHairExpanded.Dialog_ChangeHairstyle");
            //newHairstyleComboField = AccessTools.Field(changeHairstyleDialogType, "newHairBeardCombo");
            orderedHairstyleDefsField = AccessTools.Field(changeHairstyleDialogType, "orderedHairDefs");

            newHairstyleComboSync = MP.RegisterSyncField(AccessTools.Field(changeHairstyleDialogType, "newHairBeardCombo"));
            coloursTiedSync = MP.RegisterSyncField(AccessTools.Field(changeHairstyleDialogType, "coloursTied"));
            MP.RegisterSyncMethod(changeHairstyleDialogType, "SetHairstyle");
            MP.RegisterSyncWorker<Window>(SyncDialog, changeHairstyleDialogType);

            MP.RegisterSyncMethod(typeof(VanillaHairExpanded), nameof(SyncedTryRemoveWindow));

            MpCompat.harmony.Patch(AccessTools.Method(changeHairstyleDialogType, "DoWindowContents"),
                prefix: new HarmonyMethod(typeof(VanillaHairExpanded), nameof(PreDoWindowContents)),
                postfix: new HarmonyMethod(typeof(VanillaHairExpanded), nameof(PostDoWindowContents)));

            var type = AccessTools.Inner(changeHairstyleDialogType, "HairBeardCombination");
            MP.RegisterSyncWorker<object>(SyncHairstyleCombination, type);

            hairDefField = AccessTools.Field(type, "hairDef");
            beardDefField = AccessTools.Field(type, "beardDef");
            hairColourField = AccessTools.Field(type, "hairColour");
            beardColourField = AccessTools.Field(type, "beardColour");

            MpCompat.harmony.Patch(AccessTools.Method(typeof(WindowStack), nameof(WindowStack.TryRemove), new[] { typeof(Window), typeof(bool) }),
                prefix: new HarmonyMethod(typeof(VanillaHairExpanded), nameof(PreTryRemoveWindow)));
        }

        private static void PreDoWindowContents(Window __instance)
        {
            if (!MP.IsInMultiplayer)
                return;

            MP.WatchBegin();
            newHairstyleComboSync.Watch(__instance);
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
            // We used isSyncedCall to check if it's our "captured" method call, which we stop, or the synced, which we let run as intended
            if (!MP.IsInMultiplayer || isSyncedCall || window.GetType() != changeHairstyleDialogType)
            {
                isSyncedCall = false;
                return true;
            }

            isSyncedCall = true;
            SyncedTryRemoveWindow();
            return false;
        }

        private static void SyncedTryRemoveWindow()
        {
            Find.WindowStack.TryRemove(changeHairstyleDialogType);
        }

        private static void SyncHairstyleCombination(SyncWorker sync, ref object obj)
        {
            var defs = (IList)orderedHairstyleDefsField.GetValue(null);
            
            if (sync.isWriting)
            {
                sync.Write((Color)hairColourField.GetValue(obj));
                sync.Write((Color)beardColourField.GetValue(obj));
                sync.Write(defs.IndexOf(hairDefField.GetValue(obj)));
                sync.Write(defs.IndexOf(beardDefField.GetValue(obj)));
            }
            else
            {
                hairColourField.SetValue(obj, sync.Read<Color>());
                beardColourField.SetValue(obj, sync.Read<Color>());
                int index = sync.Read<int>();
                if (index >= 0)
                    hairDefField.SetValue(obj, defs[index]);
                index = sync.Read<int>();
                if (index >= 0)
                    beardDefField.SetValue(obj, defs[index]);
            }
        }

        private static void SyncDialog(SyncWorker sync, ref Window dialog)
        {
            if (!sync.isWriting)
            {
                if (Find.WindowStack.IsOpen(changeHairstyleDialogType))
                    dialog = Find.WindowStack.Windows.First(x => x.GetType() == changeHairstyleDialogType);
            }
        }
    }
}
