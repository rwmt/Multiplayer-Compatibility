using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using UnityEngine;
using Verse;

namespace Multiplayer.Compat;

/// <summary>Vanilla Factions Expanded - Insectoids 2 by Oskar Potocki, xrushha, Taranchuk, Sarg Bjornson</summary>
/// <see href="https://github.com/Vanilla-Expanded/VanillaFactionsExpanded-Insectoids2"/>
/// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3309003431"/>
[MpCompatFor("OskarPotocki.VFE.Insectoid2")]
public class VanillaFactionsInsectoid2
{
    #region Fields

    // Gizmo_Hive
    private static AccessTools.FieldRef<Gizmo, CompSpawnerPawn> hiveGizmoCompField;

    // GameComponent_Insectoids
    private static AccessTools.FieldRef<GameComponent> insectoidsGameCompInstanceField;
    private static AccessTools.FieldRef<GameComponent, object> insectoidsGameCompHordeManagerField;
    // HordeModeManager
    private static AccessTools.FieldRef<object, IList> hordeManagerWaveActivitiesField;
    private static FastInvokeHandler waveManagerInitializeWaveActivitiesMethod;
    // WaveActivity
    private static AccessTools.FieldRef<object, IDictionary> waveActivityInsectsField;
    private static FastInvokeHandler waveActivityFormRaidCompositionMethod;

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

            type = AccessTools.TypeByName("VFEInsectoids.CompMindfulSpawner");
            // Dev spawn
            MP.RegisterSyncMethodLambda(type, nameof(CompSpawner.CompGetGizmosExtra), 0).SetDebugOnly();
        }

        #endregion

        #region RNG

        {
            PatchingUtilities.PatchSystemRandCtor("VFEInsectoids.Tunneler");
        }

        #endregion
    }

    private static void LatePatch()
    {
        MpCompatPatchLoader.LoadPatch<VanillaFactionsInsectoid2>();

        #region Gizmos

        {
            var type = AccessTools.TypeByName("VFEInsectoids.Gizmo_Hive");
            hiveGizmoCompField = AccessTools.FieldRefAccess<CompSpawnerPawn>(type, "compHive");
            // Change color
            MP.RegisterSyncMethodLambda(type, nameof(Gizmo.GizmoOnGUI), 1);
        }

        #endregion

        #region Horde mode wave activities

        {
            var type = AccessTools.TypeByName("VFEInsectoids.GameComponent_Insectoids");
            insectoidsGameCompInstanceField = AccessTools.StaticFieldRefAccess<GameComponent>(
                AccessTools.DeclaredField(type, "Instance"));
            insectoidsGameCompHordeManagerField = AccessTools.FieldRefAccess<object>(
                type, "hordeModeManager");

            type = AccessTools.TypeByName("VFEInsectoids.HordeModeManager");
            hordeManagerWaveActivitiesField = AccessTools.FieldRefAccess<IList>(
                type, "waveActivities");
            waveManagerInitializeWaveActivitiesMethod = MethodInvoker.GetHandler(
                AccessTools.DeclaredMethod(type, "InitializeWaveActivities"));

            type = AccessTools.TypeByName("VFEInsectoids.WaveActivity");
            waveActivityInsectsField = AccessTools.FieldRefAccess<IDictionary>(
                type, "insects");
            waveActivityFormRaidCompositionMethod = MethodInvoker.GetHandler(
                AccessTools.DeclaredMethod(type, "FormRaidComposition"));
        }

        #endregion
    }

    #endregion

    #region SyncWorkers

    [MpCompatSyncWorker("VFEInsectoids.Gizmo_Hive", shouldConstruct = true)]
    private static void SyncGizmoHive(SyncWorker sync, ref Gizmo gizmo)
    {
        if (sync.isWriting)
            sync.Write(hiveGizmoCompField(gizmo));
        else
            hiveGizmoCompField(gizmo) = sync.Read<CompSpawnerPawn>();
    }

    #endregion

    #region Fix wave overlay and MP button overlap

    private static int ModifyScreenWidthValue(int screenWidth)
    {
        // Constant values taken directly from MP's code.
        const int mpBtnMargin = 8;
        const int mpBtnWidth = 80;

        if (MP.IsInMultiplayer)
            return screenWidth - (mpBtnWidth + 2 * mpBtnMargin);
        return screenWidth;
    }

    [MpCompatTranspiler("VFEInsectoids.HordeModeManager", "DrawWaveOverlay")]
    private static IEnumerable<CodeInstruction> MoveHordeOverlayPosition(IEnumerable<CodeInstruction> instr, MethodBase baseMethod)
    {
        var target = AccessTools.DeclaredField(typeof(UI), nameof(UI.screenWidth));
        var replacement = MpMethodUtil.MethodOf(ModifyScreenWidthValue);
        var replacedCount = 0;

        foreach (var ci in instr)
        {
            yield return ci;

            if (ci.LoadsField(target))
            {
                yield return new CodeInstruction(OpCodes.Call, replacement);

                replacedCount++;
            }
        }

        const int expected = 1;
        if (replacedCount != expected)
        {
            var name = (baseMethod.DeclaringType?.Namespace).NullOrEmpty() ? baseMethod.Name : $"{baseMethod.DeclaringType!.Name}:{baseMethod.Name}";
            Log.Warning($"Patched incorrect number of Find.CameraDriver.MapPosition calls (patched {replacedCount}, expected {expected}) for method {name}");
        }
    }

    #endregion

    #region Fix wave activities issues

    // The fixes here basically need to a couple of things:
    // 
    // 1. The wave activities are generated out of interface.
    //    This generally should be the case, but when it's null
    //    or empty when drawing the overlay, the mod will attempt
    //    to create them right there. We do it by preventing the
    //    overlay method from running if it's null/empty and
    //    attempting to call a synced initialize method, as well
    //    as adding the initialization code inside of ticking as well.
    //    
    // 2. The first wave activity's raid composition is generated
    //    out of interface. InitializeWaveActivities creates wave
    //    activities without the raid composition to make the first
    //    couple of waves more challenging, as otherwise all generated
    //    compositions would be based on current threat points rather
    //    (so for example, at the game's start) rather than closer to
    //    the wave itself. When a new wave is generated afterward
    //    (AddNextWave), they are generated with a composition.
    //    We could generate the compositions for all the activities
    //    the moment they are generated, but that would lower the
    //    difficulty for the first several waves.
    //    
    // 3. The mod never accesses the wave activities or the raid
    //    composition while they aren't initialized. There's a few
    //    possible edge cases caused by our patches that had to be
    //    handled, like using debug options before the waves/composition
    //    are generated.

    [MpCompatPrefix("VFEInsectoids.HordeModeManager", "Tick")]
    private static void PreHordeManagerTick(object __instance, IList ___waveActivities)
    {
        // Initialize the wave activities if they
        // are null or empty during ticking.
        if (___waveActivities is not { Count: > 0 })
            waveManagerInitializeWaveActivitiesMethod(__instance);
    }

    [MpCompatPostfix("VFEInsectoids.HordeModeManager", "InitializeWaveActivities")]
    [MpCompatPostfix("VFEInsectoids.HordeModeManager", "AddNextWave")]
    private static void InitializeHordeManagerWaveActivities(IList ___waveActivities)
    {
        // Ensure that first wave activity gets its raid
        // compositions initialized outside of interface.
        EnsureCurrentRaidCompositionIsInitialized(___waveActivities);
    }

    [MpCompatPostfix("VFEInsectoids.HordeModeManager", "ExposeData")]
    private static void InitializeHordeManagerWaveActivitiesExposeData(IList ___waveActivities)
    {
        // Ensure that when hosting a server for the
        // first time that the first wave activity
        // has its raid composition initialized.
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
            EnsureCurrentRaidCompositionIsInitialized(___waveActivities);
    }

    private static void EnsureCurrentRaidCompositionIsInitialized(IList waveActivities)
    {
        // The mod calls FormRaidComposition when a new wave is
        // created, or in GUI when the current wave has no
        // composition (insects field is null). However, it
        // does not create the composition when initializing
        // the waves from InitializeWaveActivities method
        // (meaning it'll generate them in the GUI).

        if (waveActivities is { Count: > 0 })
        {
            var first = waveActivities[0];
            if (waveActivityInsectsField(first) == null)
                waveActivityFormRaidCompositionMethod(first);
        }
    }

    [MpCompatSyncMethod(hostOnly = true)]
    [MpCompatPrefix("VFEInsectoids.HordeModeManager", "StartWave_Debug")]
    [MpCompatPrefix("VFEInsectoids.HordeModeManager", "CompleteWave_Debug")]
    private static void InitializeWaveActivities()
    {
        // Initialize the waves when the debug options
        // are selected to prevent errors in the mod.
        // For CompleteWave_Debug we need to ensure
        // that everything is initialized beforehand,
        // but we also need a postfix to initialize
        // data for the now first wave, since it
        // may not have the raid composition setup.
        // This situation should be rather unlikely,
        // but this should handle issues if it happens.

        var manager = insectoidsGameCompHordeManagerField(insectoidsGameCompInstanceField());
        var waveActivities = hordeManagerWaveActivitiesField(manager);

        // Initialize the wave activities if the list
        // is null or there are no elements.
        if (waveActivities is not { Count: > 0 })
            waveManagerInitializeWaveActivitiesMethod(manager);
        // If we don't need to initialize the wave
        // activities, we instead make sure that
        // a raid composition is generated.
        else
            EnsureCurrentRaidCompositionIsInitialized(waveActivities);
    }

    [MpCompatPrefix("VFEInsectoids.HordeModeManager", "DrawWaveOverlay")]
    private static bool PreHordeManagerGUI(IList ___waveActivities)
    {
        // Let it run if not in MP or (if for some reason,
        // as it seems it can happen?) in interface.
        if (!MP.IsInMultiplayer || !MP.InInterface)
            return true;
        // Ensure the list is not null, contains at least a
        // single element, and that the first element's
        // raid composition is initialized. If at least one
        // of those conditions is not met then we can't let
        // the GUI method be called, as it would cause desync.
        if (___waveActivities is { Count: > 0 } && waveActivityInsectsField(___waveActivities[0]) != null)
            return true;

        // It will be initialized when the game gets unpaused,
        // but this should handle a situation of a long pause.
        // Check only once every 100 frames to prevent spam.
        // We could introduce a field with last synced tick,
        // but this method should basically never end up being
        // called anyway, as it seems the mod only does it as
        // a safety precaution, so it would be a bit of a waste.
        if (MP.IsHosting && Time.frameCount % 100 == 0)
            InitializeWaveActivities();

        return false;
    }

    #endregion
}