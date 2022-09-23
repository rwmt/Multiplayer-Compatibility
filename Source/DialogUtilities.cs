using System;
using System.Collections.Generic;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    public static class DialogUtilities
    {
        private static bool isCloseSyncInitialized = false;
        private static bool isPauseLockInitialized = false;
        private static readonly List<Window> OpenDialogsCloseSync = new();
        private static readonly List<Window> OpenPauseLockDialogs = new();

        /// <summary>
        /// Registers a dialog (by patching declared constructors it has) to sync the
        /// interaction of closing the dialog.
        ///
        /// This should be used if a dialog allows for being closed by shortcuts like
        /// pressing escape/enter to cancel/accept, as well as cases where syncing the
        /// button or interaction to close it is in an inaccessible or hard to access
        /// location for syncing purposes.
        ///
        /// This also allows for adding a pause lock when the dialog is open.
        /// </summary>
        /// <param name="type">The type to be registered for synced closing.</param>
        /// <param name="addPauseLock">If true, the type will also be registered to pause locking.</param>
        public static void RegisterDialogCloseSync(Type type, bool addPauseLock = false)
        {
            if (type == null) return;

#if DEBUG
            Log.Message($"Registering dialog close sync type: {type.FullName}");
#endif

            InitializeDialogCloseSync(addPauseLock);

            var patch = addPauseLock
                ? new HarmonyMethod(typeof(DialogUtilities), nameof(PostDialogOpen_CloseSync_PauseLock))
                : new HarmonyMethod(typeof(DialogUtilities), nameof(PostDialogOpen_CloseSync));

            foreach (var ctor in AccessTools.GetDeclaredConstructors(type))
                MpCompat.harmony.Patch(ctor, postfix: patch);
        }

        /// <summary>
        /// Registers a dialog (by patching declared constructors it has) to pause the
        /// game while it's open.
        ///
        /// This should be used instead of <see cref="RegisterDialogCloseSync"/> when
        /// the interaction of closing the dialog is guaranteed to be synced and it
        /// doesn't require special handling.
        /// </summary>
        /// <param name="type">The type to be registered for pause lock.</param>
        public static void RegisterPauseLock(Type type)
        {
            if (type == null) return;

            InitializeDialogPauseLock();

#if DEBUG
            Log.Message($"Registering pause lock type: {type.FullName}");
#endif

            foreach (var ctor in AccessTools.GetDeclaredConstructors(type))
                MpCompat.harmony.Patch(ctor, postfix: new HarmonyMethod(typeof(DialogUtilities), nameof(PostDialogOpen_PauseLock)));
        }

        public static void InitializeDialogCloseSync(bool addPauseLock)
        {
            // Only register the pause lock if any of the dialogs requires pause lock
            if (addPauseLock)
                InitializeDialogPauseLock();

            // If we've patched the WindowStack.TryRemove method, make sure we don't do it again.
            if (isCloseSyncInitialized) return;

            MP.RegisterSyncMethod(typeof(DialogUtilities), nameof(SyncedTryCloseDialog));

            MpCompat.harmony.Patch(AccessTools.Method(typeof(WindowStack), nameof(WindowStack.TryRemove), new[] { typeof(Window), typeof(bool) }),
                prefix: new HarmonyMethod(typeof(DialogUtilities), nameof(PreTryRemove)));
            // Notify_ClickedInsideWindow is removing the window, but then inserting it into the list
            // at a different position right after. No need to touch that one at all.
            // We're assuming it won't fail doing it.

            isCloseSyncInitialized = true;
        }

        public static void InitializeDialogPauseLock()
        {
            if (isPauseLockInitialized) return;

            MP.RegisterPauseLock(IsPausingDialogOpen);
            isPauseLockInitialized = true;
        }

        private static bool IsPausingDialogOpen(Map _)
        {
            // If no pausing dialogs are open, we just return false.
            // This way it should have a minimal impact on performance.
            if (!OpenPauseLockDialogs.Any()) return false;

            // Remove all null or closed dialogs.
            // Should help situations when, for whatever reason, the dialog was without TryRemove call.
            // This will end up with a slight more performance impact, but considering
            // the game will be paused anyway: it won't be noticeable.
            OpenPauseLockDialogs.RemoveAll(x => x is not { IsOpen: true });
            return OpenPauseLockDialogs.Any();
        }

        public static void PostDialogOpen_CloseSync(Window __instance)
        {
            if (MP.IsInMultiplayer)
                OpenDialogsCloseSync.AddDistinct(__instance);
        }

        public static void PostDialogOpen_PauseLock(Window __instance)
        {
            if (MP.IsInMultiplayer)
                OpenPauseLockDialogs.AddDistinct(__instance);
        }

        public static void PostDialogOpen_CloseSync_PauseLock(Window __instance)
        {
            if (MP.IsInMultiplayer)
            {
                OpenDialogsCloseSync.AddDistinct(__instance);
                OpenPauseLockDialogs.AddDistinct(__instance);
            }
        }

        private static bool PreTryRemove(Window window, bool doCloseSound)
        {
            if (!MP.IsInMultiplayer)
                return true;

            if (MP.IsExecutingSyncCommand)
            {
                // If the dialog was removed outside of this method (in a sync method other than SyncedTryCloseDialog),
                // the dialog wouldn't be removed from the list - we need to make sure it's gone from here.
                OpenDialogsCloseSync.Remove(window);
                // Try removing the dialog from pause lock dialogs
                OpenPauseLockDialogs.Remove(window);
                return true;
            }

            var index = OpenDialogsCloseSync.IndexOf(window);

            if (index < 0)
            {
                // If it's not in OpenDialogs, it means it may only registered pause locking only.
                OpenPauseLockDialogs.Remove(window);
                return true;
            }

            SyncedTryCloseDialog(index, doCloseSound);
            return false;
        }

        private static void SyncedTryCloseDialog(int index, bool doCloseSound)
        {
            if (index >= 0 && OpenDialogsCloseSync.Count > index)
            {
                var window = OpenDialogsCloseSync[index];
                OpenDialogsCloseSync.RemoveAt(index); // Remove the window from our list just in case removing will fail
                Find.WindowStack.TryRemove(window, doCloseSound);
            }
        }
    }
}