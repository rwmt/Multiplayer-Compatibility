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
        MP.RegisterSyncMethod(AccessTools.Method("SensibleBedOwnership.Patch_CompAssignableToPawn_Bed_TryUnassignPawn:Prefix"));
        MP.RegisterSyncMethod(AccessTools.Method("SensibleBedOwnership.Patch_CompAssignableToPawn_DeathrestCasket_TryUnassignPawn:Prefix"));
        // Unassign one
        MP.RegisterSyncDelegate(AccessTools.TypeByName("SensibleBedOwnership.Patch_CompAssignableToPawn"), "<>c__DisplayClass2_2", "<Postfix>b__3");
    }
}