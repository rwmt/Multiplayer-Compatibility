using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat;

/// <summary>Sensible Bed Ownership by 1trickPwnyta</summary>
/// <remarks>Fixes for gizmos</remarks>
/// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3328702448"/>
[MpCompatFor("sensiblebedownership.1trickPwnyta")]
public class SensibleBedOwnershipCompat
{
    public SensibleBedOwnershipCompat(ModContentPack mod)
    {
        // Unassign all
        MP.RegisterSyncMethod(AccessTools.TypeByName("SensibleBedOwnership.Patch_CompAssignableToPawn_Bed_TryUnassignPawn"), "Prefix");
        // Unassign one
        MP.RegisterSyncMethodLambda(AccessTools.TypeByName("SensibleBedOwnership.Patch_CompAssignableToPawn"), "Postfix", 2);
    }
}