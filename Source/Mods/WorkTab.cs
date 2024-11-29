using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Work Tab by Fluffy</summary>
    /// <see href="https://github.com/fluffy-mods/WorkTab"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=725219116"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2552065963"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3253535347"/>
    [MpCompatFor("Fluffy.WorkTab")]
    [MpCompatFor("arof.fluffy.worktab")]
    [MpCompatFor("arof.fluffy.worktab.continued")]
    internal class WorkTab
    {
        // Delegates
        private delegate void PasteTo(PawnColumnWorker_CopyPasteWorkPriorities instance, Pawn pawn);

        // Changing priorities
        private static Type copyPasteColumnWorkerType;
        private static AccessTools.FieldRef<Dictionary<WorkGiverDef, int[]>> clipboardField;
        private static PasteTo pasteToMethod;

        public WorkTab(ModContentPack mod)
        {
            // Changing priorities
            var type = AccessTools.TypeByName("WorkTab.PriorityManager");
            MP.RegisterSyncMethod(AccessTools.PropertySetter(type, "ShowPriorities"));

            type = AccessTools.TypeByName("WorkTab.Pawn_Extensions");
            MP.RegisterSyncMethod(AccessTools.Method(type, "SetPriority", [typeof(Pawn), typeof(WorkTypeDef), typeof(int), typeof(List<int>)]));
            MP.RegisterSyncMethod(AccessTools.Method(type, "SetPriority", [typeof(Pawn), typeof(WorkTypeDef), typeof(int), typeof(int), typeof(bool)]));
            MP.RegisterSyncMethod(AccessTools.Method(type, "SetPriority", [typeof(Pawn), typeof(WorkGiverDef), typeof(int), typeof(List<int>)]));
            MP.RegisterSyncMethod(AccessTools.Method(type, "SetPriority", [typeof(Pawn), typeof(WorkGiverDef), typeof(int), typeof(int), typeof(bool)]));
            // This one not needed as it calls SetPriority, but it'll
            // end up calling it numerous times - let's just do it in one command.
            MP.RegisterSyncMethod(type, "DisableAll");

            // Technically we don't have to do this, as pasting calls SetPriority...
            // But well, it ends up being called almost 2000 times in vanilla with DLCs alone...
            // So I felt like it'll be smarter to sync it as a single command instead of potentially
            // couple thousand with mods.
            type = copyPasteColumnWorkerType = AccessTools.TypeByName("WorkTab.PawnColumnWorker_CopyPasteDetailedWorkPriorities");
            clipboardField = AccessTools.StaticFieldRefAccess<Dictionary<WorkGiverDef, int[]>>(AccessTools.Field(type, "clipboard"));
            var method = AccessTools.Method(type, "PasteTo");
            pasteToMethod = AccessTools.MethodDelegate<PasteTo>(method);
            MpCompat.harmony.Patch(method,
                prefix: new HarmonyMethod(typeof(WorkTab), nameof(PrePasteTo)));

            MP.RegisterSyncMethod(typeof(WorkTab), nameof(SyncedPasteTo));

            // We don't really need to sync those, but not doing so will end up with
            // a bunch of unnecessary synced calls (one per colonists)
            // Sadly, there isn't a call like that in case of checkbox priorities - only numeric ones
            var types = new (string typeName, Type parameterType)[]
            {
                ("WorkTab.WorkType_Extensions", typeof(WorkTypeDef)),
                ("WorkTab.WorkGiver_Extensions", typeof(WorkGiverDef)),
            };

            foreach (var (typeName, parameterType) in types)
            {
                type = AccessTools.TypeByName(typeName);
                MP.RegisterSyncMethod(AccessTools.Method(type, "DecrementPriority", [parameterType, typeof(List<Pawn>), typeof(int), typeof(List<int>), typeof(bool)]));
                MP.RegisterSyncMethod(AccessTools.Method(type, "IncrementPriority", [parameterType, typeof(List<Pawn>), typeof(int), typeof(List<int>), typeof(bool)]));
            }

            // Same deal as before, stops the call from being synced hundreds of times
            type = AccessTools.TypeByName("WorkTab.PawnColumnWorker_WorkTabLabel");
            MP.RegisterSyncMethod(type, "Decrement");
            MP.RegisterSyncMethod(type, "Increment");
            MP.RegisterSyncWorker<PawnColumnWorker_Label>(SyncPawnColumnLabel, type, shouldConstruct: true);

            // Favorites took me quite a bit of effort to sync... And well, it's a bit too much for too little reward.
            // It would probably require syncing favorite directory (WorkTab_Favourites directory in appdata)
            // So they aren't supported, unless someone else wants to take this task on themselves.
        }

        private static bool PrePasteTo(Pawn pawn)
        {
            if (!MP.IsInMultiplayer || MP.IsExecutingSyncCommand)
                return true;

            SyncedPasteTo(pawn, clipboardField());
            return false;
        }

        private static void SyncedPasteTo(Pawn pawn, Dictionary<WorkGiverDef, int[]> targetClipboard)
        {
            var current = clipboardField();
            try
            {
                clipboardField() = targetClipboard;
                // Too much effort to try and find the existing instance, so create one
                // It doesn't really matter, as it doesn't have anything important
                // besides the static field
                var tab = (PawnColumnWorker_CopyPasteWorkPriorities)Activator.CreateInstance(copyPasteColumnWorkerType);
                pasteToMethod(tab, pawn);
            }
            finally
            {
                clipboardField() = current;
            }
        }

        // Blank sync worker - we don't have anything to sync and only care about the
        // object being initialized so we can call the synced method on something
        private static void SyncPawnColumnLabel(SyncWorker sync, ref PawnColumnWorker_Label obj)
        {
        }
    }
}