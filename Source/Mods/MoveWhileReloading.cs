using System.Linq;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using Verse;
using Verse.AI;

namespace Multiplayer.Compat;

/// <summary>CE - Move while reloading (Continue) by himawari</summary>
/// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3551361813"/>
[MpCompatFor("himawari.moveWhileReloading")]
public class MoveWhileReloadingCompat
{
    private static JobDef reloadWeaponJobDef;

    public MoveWhileReloadingCompat(ModContentPack mod)
    {
        LongEventHandler.ExecuteWhenFinished(LatePatch);
    }

    private static void LatePatch()
    {
        try
        {
            reloadWeaponJobDef = GetCeReloadJobDef();
            EnsureRunAndGunBurstShotPatch();
            PatchPawnGotoForMultiplayer();
            Log.Message("MPCompat :: Initialized compatibility for himawari.moveWhileReloading");
        }
        catch (System.Exception ex)
        {
            Log.Warning($"MPCompat :: Failed to finish Move While Reloading compat setup: {ex}");
        }
    }

    /// <summary>
    /// CEMoveReload only checks roolo.RunAndGun at static init; kotobike/memegoddess forks never get TryCastNextBurstShot patched.
    /// </summary>
    private static void EnsureRunAndGunBurstShotPatch()
    {
        if (!IsAnyRunAndGunActive())
            return;

        var verbMethod = AccessTools.Method(typeof(Verb), nameof(Verb.TryCastNextBurstShot));
        var patchesType = AccessTools.TypeByName("CEMoveReload.HarmonyPatches");
        var prefix = patchesType != null
            ? AccessTools.Method(patchesType, "Prefix_TryCastNextBurstShot")
            : null;
        if (verbMethod == null || prefix == null)
            return;

        var patchInfo = Harmony.GetPatchInfo(verbMethod);
        if (patchInfo?.Prefixes?.Any(p => p.PatchMethod == prefix) == true)
            return;

        MpCompat.harmony.Patch(verbMethod, prefix: new HarmonyMethod(prefix));
    }

    private static void PatchPawnGotoForMultiplayer()
    {
        var patchesType = AccessTools.TypeByName("CEMoveReload.HarmonyPatches");
        var ceGotoPrefix = patchesType != null
            ? AccessTools.Method(patchesType, "Prefix_PawnGotoAction")
            : null;
        if (ceGotoPrefix == null)
            return;

        MpCompat.harmony.Patch(ceGotoPrefix,
            prefix: new HarmonyMethod(typeof(MoveWhileReloadingCompat), nameof(SkipCeGotoWhenNotReloading)));
    }

    /// <summary>
    /// Outside reload, CEMoveReload replaces FloatMenuMakerMap.PawnGotoAction with unsynced TryTakeOrderedJob calls.
    /// Skip its prefix in MP so vanilla/MP job sync handles movement; keep reload-specific behavior intact.
    /// </summary>
    private static bool SkipCeGotoWhenNotReloading(Pawn pawn)
    {
        if (!MP.IsInMultiplayer)
            return true;

        reloadWeaponJobDef ??= GetCeReloadJobDef();
        if (reloadWeaponJobDef != null && pawn.CurJobDef == reloadWeaponJobDef)
            return true;

        return false;
    }

    private static JobDef GetCeReloadJobDef()
    {
        var ceJobDefType = AccessTools.TypeByName("CombatExtended.CE_JobDefOf");
        if (ceJobDefType != null)
        {
            var field = AccessTools.Field(ceJobDefType, "ReloadWeapon");
            if (field != null)
                return field.GetValue(null) as JobDef;
        }

        return DefDatabase<JobDef>.GetNamedSilentFail("ReloadWeapon");
    }

    private static bool IsAnyRunAndGunActive()
    {
        foreach (var mod in LoadedModManager.RunningMods)
        {
            var id = mod.PackageId.NoModIdSuffix().ToLower();
            if (id is "roolo.runandgun" or "roolo.runandgun.kotobike" or "memegoddess.runandgun")
                return true;
        }

        return false;
    }
}
