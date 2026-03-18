using System.Reflection;
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
        // handbrakeDealsDamage is a per-player mod setting that causes desync
        // when it differs between host and client (Messages.Message consumes unique ID)
        private static FieldInfo handbrakeDealsDamageField;
        private static bool savedHandbrakeValue;

        public VanillaVehiclesExpanded(ModContentPack mod)
        {
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

            // Force handbrakeDealsDamage to consistent value in MP.
            // This per-player setting controls whether Messages.Message is called
            // during vehicle slowdown, which consumes a unique ID and causes desync.
            var settingsType = AccessTools.TypeByName("VanillaVehiclesExpanded.VanillaVehiclesExpandedSettings");
            handbrakeDealsDamageField = AccessTools.Field(settingsType, "handbrakeDealsDamage");

            var slowdownMethod = AccessTools.DeclaredMethod(
                AccessTools.TypeByName("VanillaVehiclesExpanded.CompVehicleMovementController"), "Slowdown");
            if (slowdownMethod != null && handbrakeDealsDamageField != null)
            {
                MpCompat.harmony.Patch(slowdownMethod,
                    prefix: new HarmonyMethod(typeof(VanillaVehiclesExpanded), nameof(PreSlowdown)),
                    postfix: new HarmonyMethod(typeof(VanillaVehiclesExpanded), nameof(PostSlowdown)));
            }
        }

        private static void PreSlowdown()
        {
            if (!MP.IsInMultiplayer)
                return;

            savedHandbrakeValue = (bool)handbrakeDealsDamageField.GetValue(null);
            handbrakeDealsDamageField.SetValue(null, true); // Force default (true) for consistency
        }

        private static void PostSlowdown()
        {
            if (!MP.IsInMultiplayer)
                return;

            handbrakeDealsDamageField.SetValue(null, savedHandbrakeValue);
        }
    }
}