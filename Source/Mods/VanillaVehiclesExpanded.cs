using System.Linq;
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
        private static DesignationDef designationOpen;
        private static DesignationDef designationClose;
        private static AccessTools.FieldRef<object, Thing> innerClassParentField;

        public VanillaVehiclesExpanded(ModContentPack mod)
        {
            LongEventHandler.ExecuteWhenFinished(LatePatch);

            // Gizmos
            {
                var type = AccessTools.TypeByName("VanillaVehiclesExpanded.GarageDoor");

                // Open (0), close (2), and cancel (1, 3)
                var methods = MpMethodUtil.GetLambda(type, "GetDoorGizmos", MethodType.Normal, null, 0, 1, 2, 3).ToList();
                foreach (var method in methods)
                {
                    // Sync only with `<>4__this` field. `Designation` type doesn't have a sync worker, so we'll work around that.
                    MP.RegisterSyncDelegate(type, method.DeclaringType.Name, method.Name, new[] { "<>4__this" });
                }

                innerClassParentField = AccessTools.FieldRefAccess<Thing>(methods[0].DeclaringType, "<>4__this");
                // A workaround for the lack of sync worker for `Designation`. Add a prefix that will set up the fields as needed.
                MpCompat.harmony.Patch(methods[1], prefix: new HarmonyMethod(typeof(VanillaVehiclesExpanded), nameof(PreCancelOpen)));
                MpCompat.harmony.Patch(methods[3], prefix: new HarmonyMethod(typeof(VanillaVehiclesExpanded), nameof(PreCancelClose)));

                // Open/close gizmo from GarageAutoDoor
                MP.RegisterSyncMethod(type, "StartOpening");
                MP.RegisterSyncMethod(type, "StartClosing");
            }
        }

        private static void LatePatch()
        {
            designationOpen = DefDatabase<DesignationDef>.GetNamed("VVE_Open");
            designationClose = DefDatabase<DesignationDef>.GetNamed("VVE_Close");
        }

        private static bool PreCancelOpen(object __instance, ref Designation ___openDesignation)
        {
            if (MP.IsInMultiplayer)
                return GetTargetDesignation(__instance, ref ___openDesignation, designationOpen);
            return true;
        }

        private static bool PreCancelClose(object __instance, ref Designation ___closeDesignation)
        {
            if (MP.IsInMultiplayer)
                return GetTargetDesignation(__instance, ref ___closeDesignation, designationClose);
            return true;
        }

        private static bool GetTargetDesignation(object instance, ref Designation fieldRef, DesignationDef targetDesignation)
        {
            if (targetDesignation == null)
                return false;

            var building = innerClassParentField(instance);
            fieldRef = building.Map.designationManager.DesignationOn(building, targetDesignation);

            return true;
        }
    }
}