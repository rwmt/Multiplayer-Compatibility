using System;
using System.Reflection;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Vanilla Factions Expanded - Ancients</summary>
    /// <see href="https://github.com/AndroidQuazar/VanillaFactionsExpandedAncients"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2654846754"/>
    [MpCompatFor("VanillaExpanded.VFEA")]
    internal class VanillaFactionsAncients
    {
        private static FieldInfo operationPodField;

        public VanillaFactionsAncients(ModContentPack mod)
        {
            // Supply slingshot launch gizmo (after 2 possible confirmation)
            MP.RegisterSyncMethod(AccessTools.TypeByName("VFEAncients.CompSupplySlingshot"), "TryLaunch");

            // VFEAncients.CompGeneTailoringPod:StartOperation requires SyncWorker for Operation
            // (Method inside of LatePatch)
            var type = AccessTools.TypeByName("VFEAncients.Operation");
            operationPodField = AccessTools.Field(type, "Pod");
            MP.RegisterSyncWorker<object>(SyncOperation, type, true);

            LongEventHandler.ExecuteWhenFinished(LatePatch);
        }

        public static void LatePatch()
        {
            // Ancient PD turret - toggle aiming at drop pods, enemies, explosive projectiles
            MpCompat.RegisterLambdaMethod("VFEAncients.Building_TurretPD", "GetGizmos", 1, 3, 5);

            var type = AccessTools.TypeByName("VFEAncients.CompGeneTailoringPod");
            // Start gene tailoring operation (after danger warning confirmation)
            MP.RegisterSyncMethod(type, "StartOperation");
            // Cancel operation (before starting it)
            MpCompat.RegisterLambdaMethod(type, "CompGetGizmosExtra", 8);

            // (Dev) instant success/failure 
            MpCompat.RegisterLambdaMethod(type, "CompGetGizmosExtra", 9, 10).SetDebugOnly();
            // (Dev) instant finish, random result not synced, as it calls CompleteOperation
            // would cause a tiny conflict, not worth bothering with it
            // (I think it would need to be done without SetDebugOnly, or it would cause issues)
        }

        private static void SyncOperation(SyncWorker sync, ref object operation)
        {
            if (sync.isWriting)
            {
                sync.Write((ThingComp)operationPodField.GetValue(operation));
                // Right now we have 2 types it could be, but there could be more in the future
                sync.Write(operation.GetType());
            }
            else
            {
                var pod = sync.Read<ThingComp>();
                var type = sync.Read<Type>();

                // All the current types right now have 1 argument
                operation = Activator.CreateInstance(type, pod);
            }
        }
    }
}
