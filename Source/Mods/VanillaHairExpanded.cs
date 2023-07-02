using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using UnityEngine;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Vanilla Hair Expanded by Oskar Potocki, XeoNovaDan, MonteCristo</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=1888705256"/>
    /// <see href="https://github.com/Vanilla-Expanded/VanillaHairExpanded"/>
    [MpCompatFor("VanillaExpanded.VHE")]
    class VanillaHairExpanded
    {
        private static Type changeHairstyleDialogType;
        private static List<HairDef> orderedHairstyleDefs;
        private static List<BeardDef> orderedBeardDefs;
        private static ISyncField newHairstyleComboSync;

        private static FieldInfo hairDefField;
        private static FieldInfo beardDefField;
        private static FieldInfo hairColourField;

        public VanillaHairExpanded(ModContentPack mod) => LongEventHandler.ExecuteWhenFinished(LatePatch);

        public void LatePatch()
        {
            changeHairstyleDialogType = AccessTools.TypeByName("VanillaHairExpanded.Dialog_ChangeHairstyle");
            orderedHairstyleDefs = (List<HairDef>)AccessTools.Field(changeHairstyleDialogType, "orderedHairDefs").GetValue(null);
            orderedBeardDefs = (List<BeardDef>)AccessTools.Field(changeHairstyleDialogType, "orderedBeardDefs").GetValue(null);

            newHairstyleComboSync = MP.RegisterSyncField(AccessTools.Field(changeHairstyleDialogType, "newHairBeardCombo"));
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

            MpCompat.harmony.Patch(AccessTools.Method(typeof(WindowStack), nameof(WindowStack.TryRemove), new[] { typeof(Window), typeof(bool) }),
                prefix: new HarmonyMethod(typeof(VanillaHairExpanded), nameof(PreTryRemoveWindow)));

            DialogUtilities.RegisterDialogCloseSync(changeHairstyleDialogType, true);
        }

        private static void PreDoWindowContents(Window __instance)
        {
            if (!MP.IsInMultiplayer)
                return;

            MP.WatchBegin();
            newHairstyleComboSync.Watch(__instance);
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
            if (!MP.IsInMultiplayer || MP.IsExecutingSyncCommand || window.GetType() != changeHairstyleDialogType)
                return true;

            SyncedTryRemoveWindow();
            return false;
        }

        private static void SyncedTryRemoveWindow()
        {
            Find.WindowStack.TryRemove(changeHairstyleDialogType);
        }

        private static void SyncHairstyleCombination(SyncWorker sync, ref object obj)
        {
            if (sync.isWriting)
            {
                sync.Write((Color?)hairColourField?.GetValue(obj) ?? Color.black);
                sync.Write(orderedHairstyleDefs?.IndexOf(hairDefField.GetValue(obj) as HairDef) ?? -1);
                sync.Write(orderedBeardDefs?.IndexOf(beardDefField.GetValue(obj) as BeardDef) ?? -1);
            }
            else
            {
                hairColourField.SetValue(obj, sync.Read<Color>());
                var index = sync.Read<int>();
                if (index >= 0)
                    hairDefField.SetValue(obj, orderedHairstyleDefs[index]);
                index = sync.Read<int>();
                if (index >= 0)
                    beardDefField.SetValue(obj, orderedBeardDefs[index]);
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
