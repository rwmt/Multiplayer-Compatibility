using System.Linq;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Giddy-Up 2 by Roolo, Owlchemist</summary>
    /// <see href="https://github.com/Owlchemist/GiddyUp2"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2934245647"/>
    [MpCompatFor("Owlchemist.GiddyUp")]
    public class GiddyUp2
    {
        private static FastInvokeHandler transferableAdjustTo;

        private static AccessTools.FieldRef<object, Pawn> extendedPawnDataPawn;
        private static FastInvokeHandler getGUData;

        private static AccessTools.FieldRef<object, object> parentClass;
        private static AccessTools.FieldRef<object, TransferableOneWay> transferableField;

        public GiddyUp2(ModContentPack mod)
        {
            // Gizmos
            {
                // Release animals
                MpCompat.RegisterLambdaDelegate("GiddyUp.Harmony.Patch_PawnGetGizmos", "Postfix", 0);

                // Stop waiting for rider
                MP.RegisterSyncMethod(AccessTools.TypeByName("GiddyUpRideAndRoll.Harmony.Pawn_GetGizmos"), "PawnEndCurrentJob");
            }

            // FloatMenus
            {
                // Dismount/Mount/Switch mount/Godmode mount
                MpCompat.RegisterLambdaDelegate("GiddyUp.Harmony.FloatMenuUtility", "AddMountingOptions", 0, 1, 2, 3).Last().SetDebugOnly();

                // Select/clear rider pawn for caravan
                var type = AccessTools.TypeByName("GiddyUpCaravan.Harmony.Patch_TransferableOneWayWidget");
                MP.RegisterSyncMethod(type, "SelectMountRider");
                MP.RegisterSyncMethod(type, "ClearMountRider");

                // Sync changes to TransferableOneWay.CountToTransfer
                var method = MpMethodUtil.GetLambda(type, "HandleAnimal", lambdaOrdinal: 0);
                transferableField = AccessTools.FieldRefAccess<TransferableOneWay>(method.DeclaringType, "trad");
                MpCompat.harmony.Patch(method, 
                    prefix: new HarmonyMethod(typeof(GiddyUp2), nameof(WatchTranferableCount)));

                method = MpMethodUtil.GetLambda(type, "HandleAnimal", lambdaOrdinal: 1);
                parentClass = AccessTools.FieldRefAccess<object>(method.DeclaringType, "CS$<>8__locals1");
                MpCompat.harmony.Patch(method, 
                    prefix: new HarmonyMethod(typeof(GiddyUp2), nameof(PreSetRider)));
            }

            // PawnColumnWorker
            {
                MP.RegisterSyncMethod(AccessTools.TypeByName("GiddyUpRideAndRoll.PawnColumnWorker_Mountable_Colonists"), "SetValue");
                MP.RegisterSyncMethod(AccessTools.TypeByName("GiddyUpRideAndRoll.PawnColumnWorker_Mountable_Slaves"), "SetValue");
            }

            // Sync
            {
                var type = AccessTools.TypeByName("GiddyUp.ExtendedPawnData");
                extendedPawnDataPawn = AccessTools.FieldRefAccess<Pawn>(type, "pawn");
                MP.RegisterSyncWorker<object>(SyncExtendedPawnData, type);

                getGUData = MethodInvoker.GetHandler(AccessTools.DeclaredMethod("GiddyUp.StorageUtility:GetGUData"));
                transferableAdjustTo = MethodInvoker.GetHandler(AccessTools.DeclaredMethod("Multiplayer.Client.SyncFields:TransferableAdjustTo"));
            }
        }

        private static void WatchTranferableCount(TransferableOneWay ___trad) 
            => transferableAdjustTo(null, ___trad);

        private static void PreSetRider(object __instance) 
            => WatchTranferableCount(transferableField(parentClass(__instance)));

        private static void SyncExtendedPawnData(SyncWorker sync, ref object extendedPawnData)
        {
            if (sync.isWriting)
                sync.Write(extendedPawnDataPawn(extendedPawnData));
            else
            {
                var pawn = sync.Read<Pawn>();
                extendedPawnData = getGUData(null, pawn);
            }
        }
    }
}