using System;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Linked Planters by rswallen</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3037074487"/>
    [MpCompatFor("rswallen.linkedplanters")]
    public class LinkedPlanters
    {
        private static Type gizmoCacheType;
        private static AccessTools.FieldRef<object, Building_PlantGrower> gizmoCacheRootField;

        public LinkedPlanters(ModContentPack mod)
        {
            var type = AccessTools.TypeByName("LinkedPlanters.GrowerGroup");
            type = gizmoCacheType = AccessTools.Inner(type, "GizmoCache");

            gizmoCacheRootField = AccessTools.FieldRefAccess<Building_PlantGrower>(type, "root");
            MP.RegisterSyncWorker<object>(SyncGrowerGroupGizmoCache, type);

            // Can't use MpCompat.RegisterLambdaDelegate, need to pass constructor as parent
            // 0 - link, 2 - unlink
            var methods = MpMethodUtil.GetLambda(type, null, MethodType.Constructor, new[] { typeof(Building_PlantGrower) }, 0, 2);
            foreach (var method in methods)
                MP.RegisterSyncDelegate(type, method.DeclaringType!.Name, method.Name).SetContext(SyncContext.MapSelected);
        }

        private static void SyncGrowerGroupGizmoCache(SyncWorker sync, ref object obj)
        {
            if (sync.isWriting)
                sync.Write(gizmoCacheRootField(obj));
            else
                obj = Activator.CreateInstance(gizmoCacheType, sync.Read<Building_PlantGrower>());
        }
    }
}