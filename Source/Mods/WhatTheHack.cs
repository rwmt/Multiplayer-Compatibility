using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>What the hack?! by Roolo and updated by Ogliss</summary>
    /// <see href="https://github.com/rheirman/WhatTheHack/"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=1505914869"/>
    [MpCompatFor("roolo.whatthehack")]
    [MpCompatFor("zal.whatthehack")]
    class WhatTheHack
    {
        private static Dictionary<int, Thing> thingsById;

        private static object extendedDataStorageInstance; // WorldComponent
        private static AccessTools.FieldRef<object, IDictionary> pawnStorageDictionary;
        private static FastInvokeHandler getExtendedDataForPawnMethod;

        private static AccessTools.FieldRef<object, Need> maintenanceNeedField;
        private static AccessTools.FieldRef<object, Need> powerNeedField;

        public WhatTheHack(ModContentPack mod)
        {
            thingsById = (Dictionary<int, Thing>)AccessTools.Field(AccessTools.TypeByName("Multiplayer.Client.ThingsById"), "thingsById").GetValue(null);

            // Setup
            {
                // Base mod class
                var type = AccessTools.TypeByName("WhatTheHack.Base");
                MpCompat.harmony.Patch(AccessTools.Method(type, "WorldLoaded"),
                    postfix: new HarmonyMethod(typeof(WhatTheHack), nameof(PostWorldLoaded)));
                // ExtendedDataStorage class
                type = AccessTools.TypeByName("WhatTheHack.Storage.ExtendedDataStorage");
                pawnStorageDictionary = AccessTools.FieldRefAccess<IDictionary>(type, "_store");
                getExtendedDataForPawnMethod = MethodInvoker.GetHandler(AccessTools.Method(type, "GetExtendedDataFor", new[] { typeof(Pawn) }));
                // ExtendedPawnData class, needs to be synced for some sync methods
                type = AccessTools.TypeByName("WhatTheHack.Storage.ExtendedPawnData");
                MP.RegisterSyncWorker<object>(SyncExtendedPawnData, type);
            }

            // RNG
            {
                var methods = new[]
                {
                    "WhatTheHack.Harmony.IncidentWorker_Raid_TryExecuteWorker:Prefix",
                    "WhatTheHack.Harmony.Pawn_JobTracker_DetermineNextJob:HackedPoorlyEvent",
                    "WhatTheHack.Needs.Need_Maintenance:MaybeUnhackMechanoid",
                    "WhatTheHack.Recipes.Recipe_Hacking:CheckHackingFail",
                };

                PatchingUtilities.PatchSystemRand(methods, false);

                var type = AccessTools.TypeByName("WhatTheHack.Harmony.Thing_ButcherProducts");
                PatchingUtilities.PatchSystemRand(MpMethodUtil.GetMethod(type, "GenerateExtraButcherProducts", MethodType.Enumerator, [typeof(IEnumerable<Thing>), typeof(Pawn), typeof(float)]));
            }

            // Gizmos
            {
                // Activate the beacon to boost rogue AI
                MP.RegisterSyncMethod(AccessTools.Method("WhatTheHack.Buildings.Building_MechanoidBeacon:StartupHibernatingParts"));

                // Mechanoid platform (bed)
                // Toggle regenerate missing parts (1), toggle repair (3)
                MpCompat.RegisterLambdaMethod("WhatTheHack.Buildings.Building_MechanoidPlatform", "GetGizmos", 1, 3);

                // Rogue AI, it's got a bit of functions
                var type = AccessTools.TypeByName("WhatTheHack.Buildings.Building_RogueAI");
                // Methods
                MpCompat.RegisterLambdaMethod(type, "GetTalkGibberishGizmo", 0);
                MpCompat.RegisterLambdaMethod(type, "GetManagePowerNetworkGizmo", 1);
                MpCompat.RegisterLambdaMethod(type, "GetControlTurretAvtivateGizmo", 0);
                MpCompat.RegisterLambdaMethod(type, "GetControlMechanoidActivateGizmo", 0);
                MpCompat.RegisterLambdaMethod(type, "GetHackingAvtivateGizmo", 0);
                // Delegates
                MpCompat.RegisterLambdaDelegate(type, "GetControlMechanoidCancelGizmo", 0);
                MpCompat.RegisterLambdaDelegate(type, "GetControlTurretCancelGizmo", 0);
                MpCompat.RegisterLambdaDelegate(type, "GetHackingCancelGizmo", 0);

                // Gizmo for hacked mechanoids, setting min value for maintenance
                type = AccessTools.TypeByName("WhatTheHack.Command_SetMaintenanceThreshold");
                maintenanceNeedField = AccessTools.FieldRefAccess<Need>(type, "maintenanceNeed");
                MpCompat.RegisterLambdaMethod(type, "ProcessInput", 1);
                MP.RegisterSyncWorker<Command>(SyncCommand_SetMaintenanceThreshold, type, shouldConstruct: true);

                // Gizmo for hacked mechanoids, setting min value for power
                type = AccessTools.TypeByName("WhatTheHack.Command_SetWorkThreshold");
                powerNeedField = AccessTools.FieldRefAccess<Need>(type, "powerNeed");
                MpCompat.RegisterLambdaMethod(type, "ProcessInput", 1);
                MP.RegisterSyncWorker<Command>(SyncCommand_SetWorkThreshold, type, shouldConstruct: true);

                // 
                type = AccessTools.TypeByName("WhatTheHack.Harmony.Pawn_GetGizmos");
                MpCompat.RegisterLambdaDelegate(type, "CreateGizmo_SearchAndDestroy", 1);
                MpCompat.RegisterLambdaDelegate(type, "CreateGizmo_AutoRecharge", 1);
                MpCompat.RegisterLambdaDelegate(type, "CreateGizmo_Work", 1);
                MpCompat.RegisterLambdaDelegate(type, "CreateGizmo_SelfDestruct", 0);
                MpCompat.RegisterLambdaDelegate(type, "CreateGizmo_SelfRepair", 0);
                MpCompat.RegisterLambdaDelegate(type, "CreateGizmo_Repair", 0);
                MpCompat.RegisterLambdaDelegate(type, "CreateGizmo_EquipBelt", 0);
                MpCompat.RegisterLambdaDelegate(type, "CreateGizmo_Overdrive", 0);

                type = AccessTools.TypeByName("WhatTheHack.RemoteController");
                MpCompat.RegisterLambdaDelegate(type, "GetRemoteControlDeActivateGizmo", 0);
                MpCompat.RegisterLambdaDelegate(type, "GetRemoteControlActivateGizmo", 0);

                type = AccessTools.TypeByName("WhatTheHack.Harmony.CompLongRangeMineralScanner_CompGetGizmosExtra");
                MpCompat.RegisterLambdaDelegate(type, "AddMechPartsOption", 1).SetContext(SyncContext.MapSelected);
            }
        }

        private static void PostWorldLoaded(WorldComponent ____extendedDataStorage)
            => extendedDataStorageInstance = ____extendedDataStorage;

        private static void SyncCommand_SetMaintenanceThreshold(SyncWorker sync, ref Command command)
        {
            if (sync.isWriting) sync.Write(maintenanceNeedField(command));
            else maintenanceNeedField(command) = sync.Read<Need>();
        }

        private static void SyncCommand_SetWorkThreshold(SyncWorker sync, ref Command command)
        {
            if (sync.isWriting) sync.Write(powerNeedField(command));
            else powerNeedField(command) = sync.Read<Need>();
        }

        private static void SyncExtendedPawnData(SyncWorker sync, ref object data)
        {
            if (sync.isWriting)
            {
                var id = int.MaxValue;

                foreach (DictionaryEntry dictionaryEntry in pawnStorageDictionary(extendedDataStorageInstance))
                {
                    if (dictionaryEntry.Value == data)
                    {
                        id = (int)dictionaryEntry.Key;
                        break;
                    }
                }

                sync.Write(id);
            }
            else
            {
                var id = sync.Read<int>();

                if (id != int.MaxValue && thingsById.TryGetValue(id, out var thing) && thing is Pawn)
                    data = getExtendedDataForPawnMethod.Invoke(extendedDataStorageInstance, new object[] { thing });
            }
        }
    }
}
