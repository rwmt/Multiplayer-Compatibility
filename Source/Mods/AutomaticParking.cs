using System;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat;

/// <summary>Automatic Parking by rabiosus</summary>
/// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3365473553"/>
[MpCompatFor("rabiosus.vfautoparking")]
public class AutomaticParking
{
    // CompParkingController
    private static Type parkingControllerCompType;
    private static AccessTools.FieldRef<ThingComp, object> parkingControllerCompControllerField;
    // ParkingController
    private static AccessTools.FieldRef<object, Pawn> parkingControllerVehicleField;

    public AutomaticParking(ModContentPack mod)
    {
        MpCompatPatchLoader.LoadPatch(this);

        // CompParkingController
        var type = parkingControllerCompType = AccessTools.TypeByName("VehicleAutoParking.Core.CompParkingController");
        parkingControllerCompControllerField = AccessTools.FieldRefAccess<object>(type, "parkingController");
        // ParkingController
        type = AccessTools.TypeByName("VehicleAutoParking.Core.ParkingController");
        parkingControllerVehicleField = AccessTools.FieldRefAccess<Pawn>(type, "vehicle");
        MP.RegisterSyncMethod(type, "SaveParkingPosition");
        MP.RegisterSyncMethod(type, "ResetParkingPosition");
        MP.RegisterSyncMethod(type, "MoveToParkingPosition");
    }

    [MpCompatSyncWorker("VehicleAutoParking.Core.ParkingController")]
    private static void SyncParkingController(SyncWorker sync, ref object controller)
    {
        if (sync.isWriting)
        {
            sync.Write(parkingControllerVehicleField(controller));
        }
        else
        {
            var comp = sync.Read<Pawn>().AllComps.FirstOrDefault(c => parkingControllerCompType.IsInstanceOfType(c));
            if (comp != null)
                controller = parkingControllerCompControllerField(comp);
            else
                Log.Error("A vehicle is missing CompParkingController.");
        }
    }
}