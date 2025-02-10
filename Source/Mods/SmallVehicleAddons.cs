using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat;

/// <summary>Small Vehicle Add-ons by いのしし_3</summary>
/// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3420948947"/>
[MpCompatFor("Inoshishi3.SmallVehicleAddons")]
public class SmallVehicleAddons
{
    public SmallVehicleAddons(ModContentPack mod)
    {
        // Quickly add all nearby items to transferables
        MP.RegisterSyncMethod(AccessTools.DeclaredMethod("SmallVehicleAddons.CompFastCargo:AddItemsToTheVehicleTransferables"));
        // Honk the car horn
        MP.RegisterSyncMethod(AccessTools.DeclaredMethod("SmallVehicleAddons.CompHonk:DoHonkHonk"));
        // Toggle car lights on/off
        MP.RegisterSyncMethod(AccessTools.DeclaredMethod("SmallVehicleAddons.CompLights:ToggleDeployment"));
    }
}