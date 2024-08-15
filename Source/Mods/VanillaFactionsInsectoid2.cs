using System;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using Verse;

namespace Multiplayer.Compat;

/// <summary>Vanilla Factions Expanded - Insectoids 2 by Oskar Potocki, xrushha, Taranchuk, Sarg Bjornson</summary>
/// GitHub page not up yet.
/// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3309003431"/>
[MpCompatFor("OskarPotocki.VFE.Insectoid2")]
public class VanillaFactionsInsectoid2
{
    #region Fields

    private static Type hiveGizmoType;
    private static AccessTools.FieldRef<Gizmo, CompSpawnerPawn> hiveGizmoCompField;

    #endregion

    #region Main patch

    public VanillaFactionsInsectoid2(ModContentPack mod)
    {
        LongEventHandler.ExecuteWhenFinished(LatePatch);

        #region Gizmos

        {
            var type = AccessTools.TypeByName("VFEInsectoids.CompHive");
            // Change pawn kind to spawn, called from Gizmo_Hive
            MP.RegisterSyncMethod(type, "ChangePawnKind");
            // Dev mode spawn pawn, called from gizmo lambda
            MP.RegisterSyncMethod(type, "DoSpawn").SetDebugOnly();

            type = AccessTools.TypeByName("VFEInsectoids.HediffComp_Spawn");
            // Advance severity
            MP.RegisterSyncMethodLambda(type, nameof(HediffComp.CompGetGizmos), 0).SetDebugOnly();
        }

        #endregion

        #region RNG

        {
            PatchingUtilities.PatchSystemRandCtor("VFEInsectoids.Tunneler");
        }

        #endregion
    }

    #endregion

    #region Late patch

    private static void LatePatch()
    {
        #region Gizmos

        {
            hiveGizmoType = AccessTools.TypeByName("VFEInsectoids.Gizmo_Hive");
            hiveGizmoCompField = AccessTools.FieldRefAccess<CompSpawnerPawn>(hiveGizmoType, "compHive");
            MP.RegisterSyncWorker<Gizmo>(SyncGizmoHive, hiveGizmoType, shouldConstruct: true);
            // Change color
            MP.RegisterSyncMethodLambda(hiveGizmoType, "GizmoOnGUI", 1);
        }

        #endregion
    }

    #endregion

    #region SyncWorkers

    private static void SyncGizmoHive(SyncWorker sync, ref Gizmo gizmo)
    {
        if (sync.isWriting)
            sync.Write(hiveGizmoCompField(gizmo));
        else
            hiveGizmoCompField(gizmo) = sync.Read<CompSpawnerPawn>();
    }

    #endregion
}