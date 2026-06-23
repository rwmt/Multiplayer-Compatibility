using System;
using System.Collections.Generic;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using Verse;
using Verse.AI;

namespace Multiplayer.Compat;

/// <summary>Exosuit Framework (MechsuitFramework) by AobaKuma</summary>
/// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3352894993"/>
/// <see href="https://github.com/AobaKuma/MechsuitFramework"/>
[MpCompatFor("Aoba.Exosuit.Framework")]
public class ExosuitFramework
{
    private static JobDef wgSleepJobDef;
    private static Type exosuitCoreType;

    public ExosuitFramework(ModContentPack mod)
    {
        LongEventHandler.ExecuteWhenFinished(LatePatch);
    }

    private static void LatePatch()
    {
        try
        {
            PatchRand();
            PatchGizmos();
            PatchFloatMenus();
            PatchJobs();
            PatchTurrets();
            PatchCatapult();
            Log.Message("MPCompat :: Initialized compatibility for aoba.exosuit.framework");
        }
        catch (Exception ex)
        {
            Log.Warning($"MPCompat :: Failed to finish Exosuit Framework compat setup: {ex}");
        }
    }

    private static void PatchRand()
    {
        // Must run on the main thread: patching Exosuit_Core triggers its static ctor, which creates textures.
        PatchingUtilities.PatchPushPopRand([
            "Exosuit.Exosuit_Core:CheckPreAbsorbDamage",
            "Exosuit.Exosuit_Core:GetPostArmorDamage",
            "Exosuit.Exosuit_Core:ApplyDamageToModules",
            "Exosuit.Exosuit_Core:ExosuitDestory",
            "Exosuit.MechUtility:DissambleFrom",
            "Exosuit.Verb_MeleeSweep:DoSweep",
            "Exosuit.Building_AutoRepairArm:Tick",
            "Mechsuit.AsyncShootVerb:TryCastShot",
        ]);
    }

    private static void PatchGizmos()
    {
        TryRegisterLambdaMethod("Exosuit.Patch_Pawn_GetGizmos", "Postfix", 0, 1);

        // Maintenance bay gizmos use Exosuit's own [SyncMethod] locals (MP.RegisterAll); no lambda ordinals here.

        var ejectorType = AccessTools.TypeByName("Exosuit.Building_EjectorBay");
        if (ejectorType != null)
        {
            var ejectorLambdas = TryRegisterLambdaMethod(ejectorType, nameof(Building.GetGizmos), 0, 1, 2);
            if (ejectorLambdas != null)
            {
                if (ejectorLambdas.Length > 0)
                    ejectorLambdas[0].SetContext(SyncContext.CurrentMap);
                if (ejectorLambdas.Length > 1)
                    ejectorLambdas[1].SetContext(SyncContext.CurrentMap);
            }

            // Find.CurrentMap lives inside compiler-generated closures, not in GetGizmos itself.
            PatchEjectorBayLambdasCurrentMap(ejectorType);
        }

        var turretType = AccessTools.TypeByName("Mechsuit.CompTurretGun");
        if (turretType != null)
            TryRegisterLambdaMethod(turretType, "GetGizmos", 0, 1);
    }

    private static void PatchEjectorBayLambdasCurrentMap(Type ejectorType)
    {
        for (var ord = 0; ord <= 1; ord++)
        {
            try
            {
                var lambda = MpMethodUtil.GetLambda(ejectorType, nameof(Building.GetGizmos), MethodType.Normal, null, ord);
                PatchingUtilities.ReplaceCurrentMapUsage(lambda, logIfNothingPatched: false, logIfMissingMethod: false);
            }
            catch (Exception ex)
            {
                Log.Warning($"MPCompat :: Exosuit skipped ejector bay map patch for lambda {ord}: {ex.Message}");
            }
        }
    }

    private static void PatchFloatMenus()
    {
        var maintenanceBayType = AccessTools.TypeByName("Exosuit.Building_MaintenanceBay");
        if (maintenanceBayType != null)
            TryRegisterLambdaDelegate(maintenanceBayType, nameof(Building.GetFloatMenuOptions), 0);

        var ejectorType = AccessTools.TypeByName("Exosuit.Building_EjectorBay");
        if (ejectorType != null)
            TryRegisterLambdaDelegate(ejectorType, nameof(Building.GetFloatMenuOptions), 0);

        RegisterFloatMenuProviderLambdas();
    }

    private static void PatchJobs()
    {
        // Desync logs (MpDesyncs): Pawn_JobTraceker_Patch replaces LayDown with TryTakeOrderedJob,
        // issuing unsynced job IDs. Rewrite the incoming job instead so MP keeps the same job id.
        var startJob = AccessTools.Method(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.StartJob));
        MpCompat.harmony.Patch(startJob, prefix: new HarmonyMethod(typeof(ExosuitFramework), nameof(RewriteExosuitSleepJob)));

        var jobReplacer = AccessTools.Method("Exosuit.HarmonyPatches.Pawn_JobTraceker_Patch:JobReplacerPatch");
        if (jobReplacer != null)
            MpCompat.harmony.Patch(jobReplacer, prefix: new HarmonyMethod(typeof(ExosuitFramework), nameof(SkipIfMultiplayer)));

        // JobGiver_GetRest/Work GetPriority call these every think tick and spawn local jobs.
        var gearOn = AccessTools.Method("Exosuit.MechUtility:TryMakeJob_GearOn");
        var gearOff = AccessTools.Method("Exosuit.MechUtility:TryMakeJob_GearOff");
        if (gearOn != null)
            MpCompat.harmony.Patch(gearOn, prefix: new HarmonyMethod(typeof(ExosuitFramework), nameof(SkipIfMultiplayer)));
        if (gearOff != null)
            MpCompat.harmony.Patch(gearOff, prefix: new HarmonyMethod(typeof(ExosuitFramework), nameof(SkipIfMultiplayer)));
    }

    private static void PatchTurrets()
    {
        var turretType = AccessTools.TypeByName("Mechsuit.CompTurretGun");
        if (turretType == null)
            return;

        // ITab/gizmo actions are already covered by Exosuit's MP.RegisterAll(); only sync explicit API entry points.
        TryRegisterSyncMethod(turretType, "OrderAttack", typeof(LocalTargetInfo));
        TryRegisterSyncMethod(turretType, "ClearForcedTarget");
    }

    private static void PatchCatapult()
    {
        var flyerType = AccessTools.TypeByName("Exosuit.WG_PawnFlyer");
        if (flyerType != null)
            TryRegisterLambdaDelegate(flyerType, "GetOptionsForTile", 0, 1);

        var jumpType = AccessTools.TypeByName("Exosuit.WG_AbilityVerb_QuickJump");
        var doJump = AccessTools.Method(jumpType, "DoJump",
        [
            typeof(Pawn), typeof(Map), typeof(LocalTargetInfo), typeof(LocalTargetInfo), typeof(bool), typeof(bool)
        ]);
        if (doJump != null)
            MP.RegisterSyncMethod(doJump);
    }

    private static ISyncMethod[] TryRegisterLambdaMethod(Type parentType, string parentMethod, params int[] lambdaOrdinals)
    {
        var registered = new List<ISyncMethod>();
        foreach (var ord in lambdaOrdinals)
        {
            try
            {
                registered.AddRange(MpCompat.RegisterLambdaMethod(parentType, parentMethod, ord));
            }
            catch (Exception ex)
            {
                Log.Warning($"MPCompat :: Exosuit skipped lambda sync for {parentType?.FullName}.{parentMethod} ordinal {ord}: {ex.Message}");
            }
        }

        return registered.Count > 0 ? registered.ToArray() : null;
    }

    private static ISyncMethod[] TryRegisterLambdaMethod(string parentType, string parentMethod, params int[] lambdaOrdinals)
    {
        var type = AccessTools.TypeByName(parentType);
        return type == null ? null : TryRegisterLambdaMethod(type, parentMethod, lambdaOrdinals);
    }

    private static void TryRegisterLambdaDelegate(Type parentType, string parentMethod, params int[] lambdaOrdinals)
    {
        try
        {
            MpCompat.RegisterLambdaDelegate(parentType, parentMethod, lambdaOrdinals);
        }
        catch (Exception ex)
        {
            Log.Warning($"MPCompat :: Exosuit skipped lambda delegate for {parentType?.FullName}.{parentMethod}: {ex.Message}");
        }
    }

    private static void TryRegisterSyncMethod(Type parentType, string methodName, params Type[] args)
    {
        var method = args.Length == 0
            ? AccessTools.Method(parentType, methodName)
            : AccessTools.Method(parentType, methodName, args);

        if (method == null)
        {
            Log.Warning($"MPCompat :: Exosuit skipped missing sync method {parentType?.FullName}.{methodName}");
            return;
        }

        MP.RegisterSyncMethod(method);
    }

    private static void RewriteExosuitSleepJob(ref Job newJob, Pawn ___pawn)
    {
        if (!MP.IsInMultiplayer || newJob == null || ___pawn == null)
            return;

        wgSleepJobDef ??= DefDatabase<JobDef>.GetNamedSilentFail("WG_SleepInWalkerCore");
        if (wgSleepJobDef == null)
            return;

        if (newJob.def != JobDefOf.LayDown && newJob.def != JobDefOf.Wait_Asleep)
            return;

        if (!WearingExosuitCore(___pawn))
            return;

        newJob.def = wgSleepJobDef;
    }

    private static bool WearingExosuitCore(Pawn pawn)
    {
        exosuitCoreType ??= AccessTools.TypeByName("Exosuit.Exosuit_Core");
        if (exosuitCoreType == null || pawn?.apparel?.WornApparel == null)
            return false;

        foreach (var apparel in pawn.apparel.WornApparel)
        {
            if (exosuitCoreType.IsInstanceOfType(apparel))
                return true;
        }

        return false;
    }

    /// <summary>Blocks exosuit patches that create jobs outside of MP's synced StartJob path.</summary>
    private static bool SkipIfMultiplayer() => !MP.IsInMultiplayer;

    private static void RegisterFloatMenuProviderLambdas()
    {
        var providerType = AccessTools.TypeByName("Exosuit.FloatMenuOptionProvider_ExosuitDown");
        if (providerType == null)
            return;

        var floatMenuContextType = AccessTools.TypeByName("RimWorld.FloatMenuContext");
        var pawnMethod = AccessTools.Method(providerType, "GetOptionsFor", [typeof(Pawn), floatMenuContextType]);
        if (pawnMethod == null)
            return;

        try
        {
            foreach (var ord in new[] { 0, 1 })
            {
                var lambda = MpMethodUtil.GetLambda(providerType, pawnMethod.Name, MethodType.Normal,
                    [typeof(Pawn), floatMenuContextType], ord);
                MP.RegisterSyncDelegate(providerType, lambda.DeclaringType!.Name, lambda.Name);
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"MPCompat :: Exosuit skipped float menu provider sync: {ex.Message}");
        }
    }
}
