using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Vanilla Vehicles Expanded by Oskar Potocki, xrushha, Smash Phil, Taranchuk, Reann Shepard</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3014906877"/>
    [MpCompatFor("OskarPotocki.VanillaVehiclesExpanded")]
    public class VanillaVehiclesExpanded
    {
        public VanillaVehiclesExpanded(ModContentPack mod)
        {
            MpSyncWorkers.Requires<Designation>();

            var type = AccessTools.TypeByName("VanillaVehiclesExpanded.GarageDoor");

            // Open (0), close (2), and cancel (1, 3)
            MpCompat.RegisterLambdaDelegate(type, "GetDoorGizmos", 0, 1, 2, 3);

            // Open/close gizmo from GarageAutoDoor
            MP.RegisterSyncMethod(type, "StartOpening");
            MP.RegisterSyncMethod(type, "StartClosing");

            // Toggle fridge on/off
            type = AccessTools.TypeByName("VanillaVehiclesExpanded.CompRefrigerator");
            MpCompat.RegisterLambdaMethod(type, nameof(ThingComp.CompGetGizmosExtra), 1);

            // Reloadable verb
            type = AccessTools.TypeByName("VanillaVehiclesExpanded.CompReloadableWithVerbs");
            MP.RegisterSyncMethod(type, "TryReload");
            // (Dev) reload to full
            MpCompat.RegisterLambdaMethod(type, nameof(ThingComp.CompGetGizmosExtra), 0).SetDebugOnly();

            // Restore wreck
            type = AccessTools.TypeByName("VanillaVehiclesExpanded.CompVehicleWreck");
            // Restore (0)/cancel (1)
            MpCompat.RegisterLambdaDelegate(type, nameof(ThingComp.CompGetGizmosExtra), 0, 1);
        }
    }
}