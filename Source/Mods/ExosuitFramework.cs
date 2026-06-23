using System;
using System.Collections.Generic;
using System.Reflection;
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
    private static JobDef wgGetInJobDef;
    private static JobDef wgGetInNonDraftedJobDef;
    private static JobDef wgGetOffJobDef;
    private static Type exosuitCoreType;
    private static MethodInfo getClosestCoreForPawn;
    private static MethodInfo getClosestBay;
    private static FastInvokeHandler applyDamageToModules;

    public ExosuitFramework(ModContentPack mod)
    {
        LongEventHandler.ExecuteWhenFinished(LatePatch);
    }

    private static void LatePatch()
    {
        try
        {
            PatchGizmos();
            PatchFloatMenus();
            PatchJobs();
            PatchTurrets();
            PatchCatapult();
            PatchExosuitDamage();
        }
        catch (Exception ex)
        {
            Log.Warning($"MPCompat :: Failed to finish Exosuit Framework compat setup: {ex}");
        }
    }

    private static void PatchGizmos()
    {
        // Pawn get-in / get-out buttons (Command_Action lambdas in Patch_Pawn_GetGizmos.Postfix).
        MpCompat.RegisterLambdaDelegate("Exosuit.Patch_Pawn_GetGizmos", "Postfix", 0, 1);

        // Eject to location (0), launch to map (1). Release (2) calls synced Eject on the core.
        var ejectorType = AccessTools.TypeByName("Exosuit.Building_EjectorBay");
        if (ejectorType != null)
        {
            MpCompat.RegisterLambdaMethod(ejectorType, nameof(Building.GetGizmos), 0, 1)
                .SetContext(SyncContext.CurrentMap);

            // Find.CurrentMap lives inside compiler-generated closures, not in GetGizmos itself.
            PatchEjectorBayLambdasCurrentMap(ejectorType);
        }

        // Get in (0), toggle auto repair (2). Eject uses Exosuit_Core:Eject (synced in LatePatch).
        var maintenanceBayType = AccessTools.TypeByName("Exosuit.Building_MaintenanceBay");
        if (maintenanceBayType != null)
            MpCompat.RegisterLambdaMethod(maintenanceBayType, nameof(Building.GetGizmos), 0, 2);

        // Toggle safety (1). Synced for parity with other emergency UI; eject itself goes through Eject().
        var emergencyEjectType = AccessTools.TypeByName("Exosuit.ModuleComp_EmergencyEject");
        if (emergencyEjectType != null)
            MpCompat.RegisterLambdaMethod(emergencyEjectType, nameof(ThingComp.CompGetWornGizmosExtra), 1);
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
        {
            // Add/replace/remove modules, called from ITab_MechGear.
            MP.RegisterSyncMethod(maintenanceBayType, "AddOrReplaceModule");
            MP.RegisterSyncMethod(maintenanceBayType, "RemoveModules");
            MpCompat.RegisterLambdaDelegate(maintenanceBayType, nameof(Building.GetFloatMenuOptions), 0);
        }

        var ejectorType = AccessTools.TypeByName("Exosuit.Building_EjectorBay");
        if (ejectorType != null)
            MpCompat.RegisterLambdaDelegate(ejectorType, nameof(Building.GetFloatMenuOptions), 0);

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

        // JobGiver_GetRest/Work call these every think tick via TryTakeOrderedJob (unsynced job ids in MP).
        // Redirect to StartJob so gear on/off keeps working without desyncing.
        var gearOn = AccessTools.Method("Exosuit.MechUtility:TryMakeJob_GearOn");
        var gearOff = AccessTools.Method("Exosuit.MechUtility:TryMakeJob_GearOff");
        if (gearOn != null)
            MpCompat.harmony.Patch(gearOn, prefix: new HarmonyMethod(typeof(ExosuitFramework), nameof(RedirectExosuitGearOn)));
        if (gearOff != null)
            MpCompat.harmony.Patch(gearOff, prefix: new HarmonyMethod(typeof(ExosuitFramework), nameof(RedirectExosuitGearOff)));

        // Pawn get-in/out gizmos call TryTakeOrderedJob (unsynced job ids on clients).
        var tryTake = AccessTools.Method(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.TryTakeOrderedJob),
            [typeof(Job), typeof(JobTag), typeof(bool)]);
        if (tryTake != null)
            MpCompat.harmony.Patch(tryTake, prefix: new HarmonyMethod(typeof(ExosuitFramework), nameof(RedirectExosuitTryTakeOrderedJob)));

        // Eject, only called from emergency UI. Safer to sync the method than every eject lambda.
        var eject = AccessTools.DeclaredMethod("Exosuit.Exosuit_Core:Eject");
        if (eject != null)
            MP.RegisterSyncMethod(eject);
    }

    private static void PatchExosuitDamage()
    {
        var coreType = AccessTools.TypeByName("Exosuit.Exosuit_Core");
        if (coreType == null)
            return;

        var healthField = AccessTools.Field(coreType, "healthInt");
        if (healthField != null)
            MP.RegisterSyncField(healthField);

        var applyMethod = AccessTools.Method(coreType, "ApplyDamageToModules");
        if (applyMethod != null)
            applyDamageToModules = MethodInvoker.GetHandler(applyMethod);

        var onHealthChanged = AccessTools.Method(coreType, "OnHealthChanged");
        if (onHealthChanged != null)
        {
            MpCompat.harmony.Patch(onHealthChanged,
                prefix: new HarmonyMethod(typeof(ExosuitFramework), nameof(ApplyExosuitModuleDamageImmediately)));
        }
    }

    /// <summary>Exosuit defers module damage via LongEventHandler — causes visible hit delay in MP.</summary>
    private static bool ApplyExosuitModuleDamageImmediately(float amount, Apparel __instance)
    {
        if (!MP.IsInMultiplayer)
            return true;

        var healthProp = __instance.GetType().GetProperty("Health");
        if (healthProp != null && Convert.ToSingle(healthProp.GetValue(__instance)) <= 0f)
            return true;

        if (amount <= 0f)
            return true;

        if (applyDamageToModules == null)
            return true;

        applyDamageToModules(__instance, amount);
        return false;
    }

    private static void PatchTurrets()
    {
        var turretType = AccessTools.TypeByName("Mechsuit.CompTurretGun");
        if (turretType == null)
            return;

        // ITab/gizmo actions are already covered by Exosuit's MP.RegisterAll(); only sync explicit API entry points.
        var orderAttack = AccessTools.Method(turretType, "OrderAttack", [typeof(LocalTargetInfo)]);
        if (orderAttack != null)
            MP.RegisterSyncMethod(orderAttack);

        var clearTarget = AccessTools.Method(turretType, "ClearForcedTarget");
        if (clearTarget != null)
            MP.RegisterSyncMethod(clearTarget);
    }

    private static void PatchCatapult()
    {
        // WG_PawnFlyer catapult UI — not covered by ability sync. DoJump is already synced by MP ability sync.
        var flyerType = AccessTools.TypeByName("Exosuit.WG_PawnFlyer");
        if (flyerType != null)
            MpCompat.RegisterLambdaDelegate(flyerType, "GetOptionsForTile", 0, 1);
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
    private static bool SkipIfMultiplayer() => MP.IsInMultiplayer;

    private static bool RedirectExosuitGearOn(Pawn pawn)
    {
        if (!MP.IsInMultiplayer)
            return true;

        EnsureGearJobHelpers();
        var closest = getClosestCoreForPawn?.Invoke(null, [pawn]) as Thing;
        if (closest == null)
            return false;

        TryStartExosuitGearJob(pawn, wgGetInNonDraftedJobDef, closest);
        return false;
    }

    private static bool RedirectExosuitGearOff(Pawn pawn)
    {
        if (!MP.IsInMultiplayer)
            return true;

        EnsureGearJobHelpers();
        var closest = getClosestBay?.Invoke(null, [pawn, true]) as Thing;
        if (closest == null)
            return false;

        TryStartExosuitGearJob(pawn, wgGetOffJobDef, closest);
        return false;
    }

    private static void EnsureExosuitGizmoJobDefs()
    {
        wgGetInJobDef ??= DefDatabase<JobDef>.GetNamedSilentFail("WG_GetInWalkerCore");
        wgGetInNonDraftedJobDef ??= DefDatabase<JobDef>.GetNamedSilentFail("WG_GetInWalkerCore_NonDrafted");
        wgGetOffJobDef ??= DefDatabase<JobDef>.GetNamedSilentFail("WG_GetOffWalkerCore");
    }

    private static bool RedirectExosuitTryTakeOrderedJob(Pawn ___pawn, Job job, ref bool __result)
    {
        if (!MP.IsInMultiplayer || job == null || ___pawn?.jobs == null)
            return true;

        EnsureExosuitGizmoJobDefs();
        if (job.def != wgGetInJobDef && job.def != wgGetOffJobDef && job.def != wgGetInNonDraftedJobDef)
            return true;

        ___pawn.jobs.StartJob(job, JobCondition.InterruptOptional);
        __result = true;
        return false;
    }

    private static void EnsureGearJobHelpers()
    {
        EnsureExosuitGizmoJobDefs();

        var mechUtility = AccessTools.TypeByName("Exosuit.MechUtility");
        if (mechUtility == null)
            return;

        getClosestCoreForPawn ??= AccessTools.Method(mechUtility, "GetClosestCoreForPawn");
        getClosestBay ??= AccessTools.Method(mechUtility, "GetClosestBay", [typeof(Pawn), typeof(bool)]);
    }

    private static void TryStartExosuitGearJob(Pawn pawn, JobDef jobDef, Thing target)
    {
        if (jobDef == null || pawn?.jobs == null || target == null)
            return;

        var curJob = pawn.jobs.curJob;
        if (curJob?.def == jobDef && curJob.targetA.Thing == target)
            return;

        foreach (var queued in pawn.jobs.jobQueue)
        {
            if (queued.job.def == jobDef && queued.job.targetA.Thing == target)
                return;
        }

        var job = JobMaker.MakeJob(jobDef, target);
        pawn.jobs.StartJob(job, JobCondition.InterruptOptional, null, resumeCurJobAfterwards: false);
    }

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
                MpCompat.RegisterLambdaDelegateInternal(providerType, pawnMethod.Name, MethodType.Normal, null, ord,
                    [typeof(Pawn), floatMenuContextType]);
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"MPCompat :: Exosuit skipped float menu provider sync: {ex.Message}");
        }
    }
}
