using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using Verse;

namespace Multiplayer.Compat;

/// <summary>Vanilla Gravship Expanded 2 by Oskar Potocki and the Vanilla Expanded team</summary>
/// <see href="https://github.com/Vanilla-Expanded/VanillaGravshipExpanded2"/>
/// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3799737423"/>
[MpCompatFor("vanillaexpanded.gravship2")]
public class VanillaGravshipExpanded2
{
    public VanillaGravshipExpanded2(ModContentPack mod)
    {
        // Patching these methods can initialize DefOf caches and load gizmo textures.
        LongEventHandler.ExecuteWhenFinished(LatePatch);
    }

    private static void LatePatch()
    {
        var worldComponentType = AccessTools.TypeByName("VanillaGravshipExpanded2.WorldComponent_GravshipCombat");
        var warpodType = AccessTools.TypeByName("VanillaGravshipExpanded2.CompLaunchable_Warpod");
        var vacuumLightType = AccessTools.TypeByName("VanillaGravshipExpanded2.CompVacuumWarningLight");
        var escapePodType = AccessTools.TypeByName("VanillaGravshipExpanded2.CompEscapePod");

        // Escape pods
        MP.RegisterSyncMethod(AccessTools.TypeByName("VanillaGravshipExpanded2.VGE2_MapComponent"), "EvacuationActive");
        MP.RegisterSyncMethod(escapePodType, "ClaimIfNeeded");
        MpCompat.RegisterLambdaMethod(escapePodType, nameof(ThingComp.CompGetGizmosExtra), 1);
        MpCompat.RegisterLambdaDelegate(escapePodType, nameof(ThingComp.CompFloatMenuOptions), 0, 2, 3);

        // Vacuum warning lights
        MP.RegisterSyncMethod(vacuumLightType, "ConcerningVacuumLevel");
        MP.RegisterSyncMethod(vacuumLightType, "EvacuatePawns");

        // Salvager tribute dialog
        MP.RegisterSyncMethod(worldComponentType, "PayTribute");
        MP.RegisterSyncMethod(worldComponentType, "SpawnActiveWarplatform");
        MpCompat.RegisterLambdaDelegate(worldComponentType, "ShowTributeDemandDialog", 0);

        // Salvager station comms job
        MpCompat.RegisterLambdaDelegate(
            "VanillaGravshipExpanded2.Building_CommsConsole_GetFloatMenuOptions_Patch", "Postfix", 0);

        MP.RegisterSyncMethod(warpodType, "LaunchWarpodTo");
        MP.RegisterSyncMethod(warpodType, "LaunchHellpodTo");

        // ProcessInput creates the map directly; MP reconstructs the designator and ignores its unused Event.
        MP.RegisterSyncMethod(AccessTools.TypeByName("VanillaGravshipExpanded2.Designator_GenerateEmptyOrbit"), nameof(Designator.ProcessInput))
            .SetContext(SyncContext.CurrentMap);

        // Developer gizmos
        // These callbacks capture no comp; their target is the compiler-generated singleton.
        MpCompat.RegisterLambdaDelegate(
                "VanillaGravshipExpanded2.CompPowerEmergencyGravshipGenerator", nameof(ThingComp.CompGetGizmosExtra), 0, 1, 2)
            .SetDebugOnly();
        MpCompat.RegisterLambdaMethod(
                "VanillaGravshipExpanded2.CompGravshipShieldGeneratorWithHeat", nameof(ThingComp.CompGetGizmosExtra), 0, 1, 2)
            .SetDebugOnly();
        MpCompat.RegisterLambdaMethod(
                "VanillaGravshipExpanded2.CompPower_InputOnlyBattery", nameof(ThingComp.CompGetGizmosExtra), 0, 1, 2)
            .SetDebugOnly();
        MpCompat.RegisterLambdaMethod(
                "VanillaGravshipExpanded2.CompApparelVerbOwner_Oxygen", "CompGetWornGizmosExtra", 0)
            .SetDebugOnly();
    }
}
