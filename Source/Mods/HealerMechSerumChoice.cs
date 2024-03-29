﻿using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Healer Mech Serum Choice by Syrus</summary>
    /// <see href="https://github.com/XT-0xFF/HealerMechSerumChoice"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2714095848"/>
    [MpCompatFor("Syrus.HMSChoice")]
    internal class HealerMechSerumChoice
    {
        private static Type hediffSelectionDialogType;
        private static AccessTools.FieldRef<object, Hediff> dialogSelectedHediffField;

        public HealerMechSerumChoice(ModContentPack mod)
        {
            MP.RegisterSyncMethod(typeof(HealerMechSerumChoice), nameof(SyncedSetHediff));

            // Dialog
            hediffSelectionDialogType = AccessTools.TypeByName("HMSChoice.Dialog_HediffSelection");

            dialogSelectedHediffField = AccessTools.FieldRefAccess<Hediff>(hediffSelectionDialogType, "SelectedHediff");
            MP.RegisterSyncMethod(AccessTools.Method(hediffSelectionDialogType, "Close", new[] { typeof(Hediff) }));
            MP.RegisterSyncWorker<Window>(SyncHediffDialog, hediffSelectionDialogType);
            MpCompat.harmony.Patch(AccessTools.Method(hediffSelectionDialogType, nameof(Window.DoWindowContents)),
                prefix: new HarmonyMethod(typeof(HealerMechSerumChoice), nameof(PreDoWindowContents)),
                postfix: new HarmonyMethod(typeof(HealerMechSerumChoice), nameof(PostDoWindowContents)));

            DialogUtilities.RegisterDialogCloseSync(hediffSelectionDialogType, true);
        }

        private static void PreDoWindowContents(Hediff ___SelectedHediff, ref Hediff __state)
        {
            if (!MP.IsInMultiplayer) return;

            __state = ___SelectedHediff;
        }

        private static void PostDoWindowContents(ref Hediff ___SelectedHediff, Hediff __state)
        {
            if (!MP.IsInMultiplayer) return;

            if (___SelectedHediff != __state)
            {
                SyncedSetHediff(___SelectedHediff);
                ___SelectedHediff = __state;
            }
        }

        private static void SyncedSetHediff(Hediff hediff)
        {
            var window = Find.WindowStack.Windows.FirstOrDefault(x => x.GetType() == hediffSelectionDialogType);

            if (window != null)
                dialogSelectedHediffField(window) = hediff;
        }

        private static void SyncHediffDialog(SyncWorker sync, ref Window window)
        {
            if (!sync.isWriting)
                window = Find.WindowStack.Windows.FirstOrDefault(x => x.GetType() == hediffSelectionDialogType);
        }
    }
}
