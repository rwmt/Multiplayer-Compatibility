using System;
using System.Collections.Generic;
using System.Diagnostics;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Multiplayer.Compat
{
    public static class MpSyncWorkers
    {
        private static readonly HashSet<Type> AlreadyRegistered = [];

        public static void Requires<T>() => Requires(typeof(T));

        public static void Requires(Type type)
        {
            // Registering the same sync worker multiple times would result in
            // the warning about sync worker existing in MP. Store a list of
            // sync workers we registered to avoid the warning if we registered
            // it, as well as prevent duplicate warnings if the sync worker exists
            // in MP already, and we call this method multiple times for the same type.
            if (!AlreadyRegistered.Add(type))
                return;

            // HasSyncWorker would return true, since MP has an implicit sync worker for
            // WorldObject, but it currently cannot handle WorldObject (fixed by PR #504).
            if (type == typeof(PocketMapParent))
            {
                MP.RegisterSyncWorker<PocketMapParent>(SyncPocketMapParent, isImplicit: true);
                return;
            }

            if (HasSyncWorker(type))
            {
                Log.Warning($"Sync worker of type {type} already exists in MP, temporary sync worker can be removed from MP Compat");
                return;
            }

            if (type == typeof(GameCondition))
                MP.RegisterSyncWorker<GameCondition>(SyncGameCondition, isImplicit: true);
            else
                Log.Error($"Trying to register SyncWorker of type {type}, but it's not supported.\n{new StackTrace(1)}");
        }

        private static void SyncGameCondition(SyncWorker sync, ref GameCondition gameCondition)
        {
            if (sync.isWriting)
            {
                sync.Write(gameCondition.gameConditionManager.ownerMap);
                sync.Write(gameCondition.uniqueID);
            }
            else
            {
                var map = sync.Read<Map>();
                var id = sync.Read<int>();

                var manager = map == null
                    ? Find.World.GameConditionManager
                    : map.GameConditionManager;

                gameCondition = manager.ActiveConditions.FirstOrDefault(condition => condition.uniqueID == id);
            }
        }

        private static void SyncPocketMapParent(SyncWorker sync, ref PocketMapParent pmp)
        {
            if (sync.isWriting)
            {
                // This will sync ID for PocketMapParent twice, since it'll also use
                // the sync worker for WorldObject first. However, that sync worker
                // will fail as it doesn't support pocket maps yet (fixed by PR #504).
                sync.Write(pmp?.ID ?? -1);
            }
            else
            {
                var id = sync.Read<int>();
                // Skip if the pocket map is null. Also make sure to not
                // overwrite the object if it happens to not be null.
                if (id != -1)
                    pmp ??= Find.World.pocketMaps.Find(p => p.ID == id);
            }
        }

        private static bool HasSyncWorker(Type type)
        {
            const string fieldPath = "Multiplayer.Client.Multiplayer:serialization";
            const string methodName = "CanHandle";
            void Error(string message) => Log.Error($"Failed to check if sync worker for type {type} is already registered in MP - {message}");

            // Don't cache the field/method, they will be used very rarely (assuming they will even be used at all), so there's no point in having a field for it.
            var field = AccessTools.DeclaredField(fieldPath);

            if (field == null)
            {
                Error($"failed to find field {fieldPath}");
                return false;
            }

            if (!field.IsStatic)
            {
                Error($"field {fieldPath} is not static");
                return false;
            }

            var instance = field.GetValue(null);

            if (instance == null)
            {
                Error($"field {fieldPath} is null");
                return false;
            }

            var method = AccessTools.DeclaredMethod(field.FieldType, methodName, [typeof(SyncType)]);

            if (method == null)
            {
                Error($"failed to find method {methodName}");
                return false;
            }

            if (method.ReturnType != typeof(bool))
            {
                Error($"return type is not bool but {method.ReturnType} for method {methodName}");
                return false;
            }

            if (method.IsStatic)
            {
                Log.Warning($"Method {methodName} is static, but we expected it to be non-static - the results may potentially be incorrect.");
                return (bool)method.Invoke(null, [new SyncType(type)]);
            }

            return (bool)method.Invoke(instance, [new SyncType(type)]);
        }
    }
}