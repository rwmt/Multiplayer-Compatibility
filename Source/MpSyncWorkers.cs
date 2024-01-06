using System;
using System.Diagnostics;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using Verse;

namespace Multiplayer.Compat
{
    public static class MpSyncWorkers
    {
        public static void Requires<T>() => Requires(typeof(T));

        public static void Requires(Type type)
        {
            if (HasSyncWorker(type))
            {
                Log.Warning($"Sync worker of type {type} already exists in MP, temporary sync worker can be removed from MP Compat");
                return;
            }

            if (type == typeof(ThingDefCount))
                MP.RegisterSyncWorker<ThingDefCount>(SyncThingDefCount);
            else if (type == typeof(GameCondition))
                MP.RegisterSyncWorker<GameCondition>(SyncGameCondition, isImplicit: true);
            else if (type == typeof(DesignationManager))
                MP.RegisterSyncWorker<DesignationManager>(SyncDesignationManager);
            else if (type == typeof(Designation))
            {
                Requires<DesignationManager>();
                MP.RegisterSyncWorker<Designation>(SyncDesignation, isImplicit: true);
            }
            else
                Log.Error($"Trying to register SyncWorker of type {type}, but it's not supported.\n{new StackTrace(1)}");
        }

        private static void SyncThingDefCount(SyncWorker sync, ref ThingDefCount thingDefCount)
        {
            if (sync.isWriting)
            {
                sync.Write(thingDefCount.ThingDef);
                sync.Write(thingDefCount.Count);
            }
            else
            {
                var def = sync.Read<ThingDef>();
                var count = sync.Read<int>();

                thingDefCount = new ThingDefCount(def, count);
            }
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

        private static void SyncDesignation(SyncWorker sync, ref Designation designation)
        {
            if (sync.isWriting)
            {
                var canSync = designation?.designationManager != null;
                sync.Write(canSync);

                if (canSync)
                {
                    sync.Write(designation.designationManager);
                    sync.Write(designation.target);
                    sync.Write(designation.def);
                }
            }
            else
            {
                if (sync.Read<bool>())
                {
                    var manager = sync.Read<DesignationManager>();
                    var target = sync.Read<LocalTargetInfo>();
                    var def = sync.Read<DesignationDef>();

                    if (target.HasThing)
                        designation = manager.DesignationOn(target.Thing, def);
                    else
                        designation = manager.DesignationAt(target.Cell, def);
                }
            }
        }

        private static void SyncDesignationManager(SyncWorker sync, ref DesignationManager manager)
        {
            if (sync.isWriting)
                sync.Write(manager.map);
            else
                manager = sync.Read<Map>().designationManager;
        }

        private static bool HasSyncWorker(Type type)
        {
            const string methodPath = "Multiplayer.Client.SyncSerialization:CanHandle";

            // Don't cache the method, it'll be used very rarely (assuming it'll even be used at all), so there's no point in having a field for it.
            var method = AccessTools.DeclaredMethod(methodPath, new[] { typeof(SyncType) });

            if (method == null)
            {
                Log.Error($"Failed to check if sync worker for type {type} is already registered in MP - failed to find method {methodPath}");
                return false;
            }

            if (method.ReturnType != typeof(bool))
            {
                Log.Error($"Failed to check if sync worker for type {type} is already registered in MP - return type is not bool but {method.ReturnType} for method {methodPath}");
                return false;
            }

            return (bool)method.Invoke(null, new object[] { new SyncType(type) });
        }
    }
}