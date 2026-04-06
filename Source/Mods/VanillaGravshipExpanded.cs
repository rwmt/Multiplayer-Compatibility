using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Multiplayer.Compat
{
    /// <summary>Vanilla Gravship Expanded by Oskar Potocki, Taranchuk, Kentington, Sarg Bjornson</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3609835606"/>
    /// <see href="https://github.com/Vanilla-Expanded/VanillaGravshipExpanded"/>
    [MpCompatFor("vanillaexpanded.gravship")]
    class VanillaGravshipExpanded
    {
        // Dialog_ConfigureVacuumRequirement
        private static AccessTools.FieldRef<object, float> vacCheckpointResistanceField;
        private static AccessTools.FieldRef<object, bool> vacCheckpointAllowDraftedField;

        // Window_SetDesiredMaintenance
        private static ISyncField maintenanceThresholdSync;

        // Window_RenameAsteroid
        private static AccessTools.FieldRef<object, object> renameWindowWorldObjectField;
        private static string cachedAsteroidName;

        // Flag to preserve VGE state during our SyncedGravshipTileSelected flow
        private static bool inSyncedTileSelectedFlow;

        // Gizmo_OxygenProvider
        private static Type oxygenGizmoType;
        private static AccessTools.FieldRef<object, object> oxygenProviderFromGizmo;
        private static ISyncField oxygenRechargeAtField;
        private static ISyncField oxygenAutoRechargeField;

        // Building_VacBarrier_Recolorable color sync
        private static Type vacBarrierRecolorableType;
        private static AccessTools.FieldRef<object, Color> barrierColorField;
        private static FieldInfo colorClipboardFieldInfo;
        private static AccessTools.FieldRef<object, object> vacBarrierDialogBarriersField;
        private static MethodInfo notifyBarrierColorChangedMethod;

        // Mod settings - orbitalObjectsMultiplier must be consistent in MP
        private static AccessTools.FieldRef<float> orbitalObjectsMultiplierField;
        private static float savedMultiplier;

        // VGE launch flow - sync tile selection for gravship destinations
        private static AccessTools.FieldRef<object> vgeLaunchStateField;
        private static AccessTools.FieldRef<object, PlanetTile> vgeLaunchStateTargetTileField;
        private static AccessTools.FieldRef<object, object> vgeLaunchStateInstanceField;
        private static AccessTools.FieldRef<object, TargetInfo> vgeLaunchStateTargetInfoField;
        private static AccessTools.FieldRef<object, object> vgeLaunchStateForObligationField;
        private static AccessTools.FieldRef<object, Pawn> vgeLaunchStateSelectedPawnField;
        private static AccessTools.FieldRef<object, object> vgeLaunchStateForcedForRoleField;
        private static AccessTools.FieldRef<PlanetTile> vgeCheckConfirmSettleTargetTileField;
        private static Action<PlanetTile> closeGravshipSession;
        private static Func<PlanetTile, bool> hasGravshipSession;
        private static MethodInfo stopTilePickerInt;

        // TakeoffEnded map decision - cached VGE extension methods
        private static MethodInfo canEverKeepThisMapMethod;
        private static MethodInfo shouldAlwaysKeepThisMapMethod;
        private static MethodInfo shouldHaveKeepMapUIMethod;

        // VGE PreLaunchConfirmation sync - VGE replaces the vanilla launch action
        private static Action capturedOriginalLaunchAction;
        private static MethodInfo destroyTreesAroundSubstructureMethod;
        private static MethodInfo consumeFuelMethod;
        private static MethodInfo initiateTakeoffMethod;
        private static PropertyInfo validSubstructureProperty;

        public VanillaGravshipExpanded(ModContentPack mod)
        {
            LongEventHandler.ExecuteWhenFinished(LatePatch);

            // RNG fixes
            PatchingUtilities.PatchPushPopRand("VanillaGravshipExpanded.ArtilleryUtility:SpawnArtilleryProjectile");
            PatchingUtilities.PatchPushPopRand("VanillaGravshipExpanded.Astrofire:SpawnSetup");
            PatchingUtilities.PatchPushPopRand("VanillaGravshipExpanded.Astrofire:TickInterval");
            PatchingUtilities.PatchPushPopRand("VanillaGravshipExpanded.GameCondition_MicrometeorStorm:GameConditionTick");
            PatchingUtilities.PatchPushPopRand("VanillaGravshipExpanded.GameCondition_Comet:Init");
            PatchingUtilities.PatchPushPopRand("VanillaGravshipExpanded.LandingOutcomeWorker_AstrofuelPipeRupture:ApplyOutcome");
            PatchingUtilities.PatchPushPopRand("VanillaGravshipExpanded.LandingOutcomeWorker_VerminInfestation:ApplyOutcome");
            PatchingUtilities.PatchPushPopRand("VanillaGravshipExpanded.LandingOutcomeWorker_OxygenLeak:ApplyOutcome");
            PatchingUtilities.PatchPushPopRand("VanillaGravshipExpanded.LandingOutcomeWorker_VacBarrierFlicker:ApplyOutcome");
            PatchingUtilities.PatchPushPopRand("VanillaGravshipExpanded.LandingOutcomeWorker_UnwantedAttention:ApplyOutcome");

            // Launch boon RNG fixes
            PatchingUtilities.PatchPushPopRand("VanillaGravshipExpanded.LaunchBoonWorker_AsteroidDiscovery:ApplyBoon");
            PatchingUtilities.PatchPushPopRand("VanillaGravshipExpanded.LaunchBoonWorker_CaravanEncounter:ApplyBoon");
            PatchingUtilities.PatchPushPopRand("VanillaGravshipExpanded.LaunchBoonWorker_LandmarkSpotted:ApplyBoon");
            PatchingUtilities.PatchPushPopRand("VanillaGravshipExpanded.LaunchBoonWorker_RichDepositDetected:ApplyBoon");

            // Landing flow RNG fixes - crash landing and boon selection
            PatchingUtilities.PatchPushPopRand("VanillaGravshipExpanded.WorldComponent_GravshipController_LandingEnded_Patch:ApplyCrashlanding");
            PatchingUtilities.PatchPushPopRand("VanillaGravshipExpanded.WorldComponent_GravshipController_LandingEnded_Patch:TryTriggerLaunchBoon");
            PatchingUtilities.PatchPushPopRand("VanillaGravshipExpanded.Projectile_ArtilleryBeam:Impact");
            PatchingUtilities.PatchPushPopRand("VanillaGravshipExpanded.Projectile_Asteroid:TryDropLoot");
            PatchingUtilities.PatchPushPopRand("VanillaGravshipExpanded.Projectile_SpaceDebris:TryDropLoot");

            // VGE's CompPowerPlantGravEngine.DesiredPowerOutput accesses ValidSubstructure during
            // CompTick. When a substructure completes, UpdateSubstructureIfNeeded creates a
            // Dialog_NamePlayerGravship whose constructor uses Rand to generate a name suggestion.
            // This consumes game RNG and causes desync between host and client.
            PatchingUtilities.PatchPushPopRand("VanillaGravshipExpanded.CompPowerPlantGravEngine:get_DesiredPowerOutput");

            // Visual-only RNG fixes (prevent camera-dependent Rand from diverging game state)
            PatchingUtilities.PatchPushPopRand("VanillaGravshipExpanded.Projectile_Gauss:DrawAt");
            PatchingUtilities.PatchPushPopRand("VanillaGravshipExpanded.Projectile_JavelinRocket:EmitExhaust");
            PatchingUtilities.PatchPushPopRand("VanillaGravshipExpanded.Projectile_JavelinRocket:DrawAt");
            PatchingUtilities.PatchPushPopRand("VanillaGravshipExpanded.Verb_ShootWithSmoke:ThrowSmoke");
            PatchingUtilities.PatchPushPopRand("VanillaGravshipExpanded.GameCondition_Comet:DoCellSteadyEffects");
            PatchingUtilities.PatchPushPopRand("VanillaGravshipExpanded.GameCondition_SpaceSolarFlare:DoCellSteadyEffects");
        }

        private static void LatePatch()
        {
            #region Turret-terminal linking

            {
                var terminalType = AccessTools.TypeByName("VanillaGravshipExpanded.Building_TargetingTerminal");
                var turretType = AccessTools.TypeByName("VanillaGravshipExpanded.Building_GravshipTurret");

                MP.RegisterSyncMethod(terminalType, "LinkTo");
                MP.RegisterSyncMethod(terminalType, "Unlink");
                MP.RegisterSyncMethod(turretType, "LinkTo");
                MP.RegisterSyncMethod(turretType, "Unlink");
            }

            #endregion

            #region World artillery

            {
                var type = AccessTools.TypeByName("VanillaGravshipExpanded.CompWorldArtillery");

                MP.RegisterSyncMethod(type, "StartAttack").SetContext(SyncContext.MapSelected);

                MP.RegisterSyncMethod(type, "Reset");
            }

            // CancelInInterface for OrderAttack/ResetForcedTarget Harmony patches
            // that call comp.Reset() in UI context
            {
                PatchingUtilities.PatchCancelInInterface(
                    "VanillaGravshipExpanded.Building_TurretGun_OrderAttack_Patch:Prefix");
                PatchingUtilities.PatchCancelInInterface(
                    "VanillaGravshipExpanded.Building_TurretGun_ResetForcedTarget_Patch:Prefix");
            }

            #endregion

            #region Gizmo actions

            {
                // Building_GravshipBlackBox - convert gravdata to research
                // Lambda captures a local (currentProject), creating a display class MP can't serialize.
                // Sync via prefix + SyncMethod instead.
                var blackBoxType = AccessTools.TypeByName("VanillaGravshipExpanded.Building_GravshipBlackBox");
                var blackBoxLambda = MpMethodUtil.GetLambda(blackBoxType, "GetGizmos", lambdaOrdinal: 0);
                MpCompat.harmony.Patch(blackBoxLambda,
                    prefix: new HarmonyMethod(typeof(VanillaGravshipExpanded), nameof(PreBlackBoxConvert)));
                MP.RegisterSyncMethod(typeof(VanillaGravshipExpanded), nameof(SyncedBlackBoxConvert));

                // Building_SealantPopper - toggle autoRebuild (lambda 1, after isActive getter at 0)
                MpCompat.RegisterLambdaMethod("VanillaGravshipExpanded.Building_SealantPopper", "GetGizmos", 1);

                // Building_Agrocell - toggle SunLampOn (lambda 0)
                MpCompat.RegisterLambdaMethod("VanillaGravshipExpanded.Building_Agrocell", "GetGizmos", 0);

                // CompGravheatAbsorber - absorb gravheat (method group, not lambda)
                MP.RegisterSyncMethod(AccessTools.TypeByName("VanillaGravshipExpanded.CompGravheatAbsorber"), "AbsorbGravheat");
            }

            #endregion

            #region Gravtech research sync

            {
                // VGE patches DoBeginResearch/SetCurrentProject with prefixes that return false
                // for gravtech projects (calling SetGravshipResearch instead). This skips the method
                // body where MP's sync transpiler lives, so the sync never fires.
                // CancelInInterface lets MP's sync fire; VGE's prefixes run normally during sync execution.
                PatchingUtilities.PatchCancelInInterfaceSetResultToTrue(
                    "VanillaGravshipExpanded.MainTabWindow_Research_DoBeginResearch_Patch:Prefix");
                PatchingUtilities.PatchCancelInInterfaceSetResultToTrue(
                    "VanillaGravshipExpanded.ResearchManager_SetCurrentProject_Patch:Prefix");

                // VGE's StopProject prefix clears currentGravtechProject in UI context before
                // MP can sync the call. Cancel in interface so it only runs during sync execution.
                PatchingUtilities.PatchCancelInInterface(
                    "VanillaGravshipExpanded.ResearchManager_StopProject_Patch:Prefix");
            }

            #endregion

            #region Dialog_ConfigureVacuumRequirement

            {
                var checkpointType = AccessTools.TypeByName("VanillaGravshipExpanded.Building_VacCheckpoint");
                vacCheckpointResistanceField = AccessTools.FieldRefAccess<float>(checkpointType, "requiredResistance");
                vacCheckpointAllowDraftedField = AccessTools.FieldRefAccess<bool>(checkpointType, "allowDrafted");

                var dialogType = AccessTools.TypeByName("VanillaGravshipExpanded.Dialog_ConfigureVacuumRequirement");
                MpCompat.harmony.Patch(
                    AccessTools.DeclaredMethod(dialogType, "SetSelectedVacCheckpointsTo"),
                    prefix: new HarmonyMethod(typeof(VanillaGravshipExpanded), nameof(PreSetSelectedVacCheckpoints)));

                MP.RegisterSyncMethod(typeof(VanillaGravshipExpanded), nameof(SyncedSetVacCheckpoint));
            }

            #endregion

            #region Window_SetDesiredMaintenance

            {
                var exposePatchType = AccessTools.TypeByName("VanillaGravshipExpanded.World_ExposeData_Patch");
                maintenanceThresholdSync = MP.RegisterSyncField(exposePatchType, "maintenanceThreshold");

                var windowType = AccessTools.TypeByName("VanillaGravshipExpanded.Window_SetDesiredMaintenance");
                MpCompat.harmony.Patch(
                    AccessTools.DeclaredMethod(windowType, "DoWindowContents"),
                    prefix: new HarmonyMethod(typeof(VanillaGravshipExpanded), nameof(PreMaintenanceDoWindowContents)),
                    postfix: new HarmonyMethod(typeof(VanillaGravshipExpanded), nameof(PostMaintenanceDoWindowContents)));
            }

            #endregion

            #region Window_RenameAsteroid

            {
                var windowType = AccessTools.TypeByName("VanillaGravshipExpanded.Window_RenameAsteroid");
                renameWindowWorldObjectField = AccessTools.FieldRefAccess<object>(windowType, "worldObject");

                MpCompat.harmony.Patch(
                    AccessTools.DeclaredMethod(windowType, "DoWindowContents"),
                    prefix: new HarmonyMethod(typeof(VanillaGravshipExpanded), nameof(PreRenameDoWindowContents)),
                    postfix: new HarmonyMethod(typeof(VanillaGravshipExpanded), nameof(PostRenameDoWindowContents)));

                MP.RegisterSyncMethod(typeof(VanillaGravshipExpanded), nameof(SyncedRenameAsteroid));
            }

            #endregion

            #region Building_Gravlift (launch to orbit)

            {
                // Gizmo lambda sets IsGravliftLaunch then calls ShowRitualBeginWindow.
                // MP intercepts ShowRitualBeginWindow to create a RitualSession.
                MpCompat.RegisterLambdaMethod("VanillaGravshipExpanded.Building_Gravlift", "GetGizmos", 0);
            }

            #endregion

            #region Building_VacBarrier_Recolorable color sync

            {
                vacBarrierRecolorableType = AccessTools.TypeByName("VanillaGravshipExpanded.Building_VacBarrier_Recolorable");
                barrierColorField = AccessTools.FieldRefAccess<Color>(vacBarrierRecolorableType, "barrierColor");
                colorClipboardFieldInfo = AccessTools.DeclaredField(vacBarrierRecolorableType, "ColorClipboard");
                notifyBarrierColorChangedMethod = AccessTools.Method(vacBarrierRecolorableType, "Notify_ColorChanged");

                // Prefix on paste gizmo lambda — reads per-client ColorClipboard,
                // so we must capture the color and sync per-barrier.
                var pasteLambda = MpMethodUtil.GetLambda(vacBarrierRecolorableType, "GetGizmos", lambdaOrdinal: 2);
                MpCompat.harmony.Patch(pasteLambda,
                    prefix: new HarmonyMethod(typeof(VanillaGravshipExpanded), nameof(PrePasteBarrierColor)));

                // Prefix on color picker dialog SaveColor
                var colorPickerType = AccessTools.TypeByName("VanillaGravshipExpanded.Dialog_VacBarrierColorPicker");
                vacBarrierDialogBarriersField = AccessTools.FieldRefAccess<object>(colorPickerType, "extraVacBarriers");

                MpCompat.harmony.Patch(
                    AccessTools.DeclaredMethod(colorPickerType, "SaveColor"),
                    prefix: new HarmonyMethod(typeof(VanillaGravshipExpanded), nameof(PreVacBarrierSaveColor)));

                MP.RegisterSyncMethod(typeof(VanillaGravshipExpanded), nameof(SyncedSetBarrierColor));
            }

            #endregion

            #region VGE launch flow fix

            {
                // VGE replaces the vanilla gravship launch flow: instead of tile picker → launch,
                // it does tile picker → ritual → launch. VGE intercepts ShowRitualBeginWindow to
                // insert a tile selection step before the ritual.
                //
                // The MP mod syncs vanilla's settleAction lambda (b__5 in StartChoosingDestination_NewTemp)
                // via SyncDelegate. But VGE's CheckConfirmSettle prefix REPLACES that settleAction
                // with its own delegate (which opens ShowRitualBeginWindow instead of launching).
                // Since b__5 is never called, MP's sync never fires.
                //
                // Fix: intercept CheckConfirmSettle when VGE state is active, and use a synced method
                // to sync the tile selection. The synced method closes the tile picker, removes the
                // GravshipTravelSession, and calls ShowRitualBeginWindow (which MP handles via RitualSession).

                var showRitualPatchType = AccessTools.TypeByName(
                    "VanillaGravshipExpanded.Dialog_BeginRitual_ShowRitualBeginWindow_Patch");
                vgeLaunchStateField = AccessTools.StaticFieldRefAccess<object>(
                    AccessTools.DeclaredField(showRitualPatchType, "state"));

                // GravshipLaunchState fields (needed to call ShowRitualBeginWindow with correct args)
                var launchStateType = AccessTools.TypeByName("VanillaGravshipExpanded.GravshipLaunchState");
                vgeLaunchStateTargetTileField = AccessTools.FieldRefAccess<PlanetTile>(launchStateType, "targetTile");
                vgeLaunchStateInstanceField = AccessTools.FieldRefAccess<object>(launchStateType, "instance");
                vgeLaunchStateTargetInfoField = AccessTools.FieldRefAccess<TargetInfo>(launchStateType, "targetInfo");
                vgeLaunchStateForObligationField = AccessTools.FieldRefAccess<object>(launchStateType, "forObligation");
                vgeLaunchStateSelectedPawnField = AccessTools.FieldRefAccess<Pawn>(launchStateType, "selectedPawn");
                vgeLaunchStateForcedForRoleField = AccessTools.FieldRefAccess<object>(launchStateType, "forcedForRole");

                // VGE's CheckConfirmSettle patch stores the selected tile in a static field
                var checkConfirmPatchType = AccessTools.TypeByName(
                    "VanillaGravshipExpanded.SettlementProximityGoodwillUtility_CheckConfirmSettle_Patch");
                vgeCheckConfirmSettleTargetTileField = AccessTools.StaticFieldRefAccess<PlanetTile>(
                    AccessTools.DeclaredField(checkConfirmPatchType, "targetTile"));

                // MP's GravshipTravelUtils.CloseSessionAt (via reflection, not in public API)
                var travelUtilsType = AccessTools.TypeByName("Multiplayer.Client.Persistent.GravshipTravelUtils");
                var closeMethod = AccessTools.DeclaredMethod(travelUtilsType, "CloseSessionAt");
                if (closeMethod != null)
                    closeGravshipSession = (Action<PlanetTile>)Delegate.CreateDelegate(typeof(Action<PlanetTile>), closeMethod);

                var hasMethod = AccessTools.DeclaredMethod(travelUtilsType, "HasSessionAt");
                if (hasMethod != null)
                    hasGravshipSession = (Func<PlanetTile, bool>)Delegate.CreateDelegate(typeof(Func<PlanetTile, bool>), hasMethod);

                // TilePicker.StopTargetingInt bypasses VGE's state-clearing patch on StopTargeting
                stopTilePickerInt = AccessTools.DeclaredMethod(typeof(TilePicker), "StopTargetingInt");

                // Clear stale VGE state before ShowRitualBeginWindow runs.
                // VGE's state is a per-process static that may be left set on one client
                // but not the other (cleared by UI-driven patches like Window_PostClose_Patch
                // or TilePicker_StopTargeting_Patch). In tick context (pilot console job),
                // always clear state so both clients start fresh. In sync context
                // (our SyncedGravshipTileSelected), state is needed — don't clear.
                MpCompat.harmony.Patch(
                    AccessTools.DeclaredMethod(typeof(Precept_Ritual), "ShowRitualBeginWindow"),
                    prefix: new HarmonyMethod(typeof(VanillaGravshipExpanded), nameof(PreShowRitualClearStaleState))
                        { priority = Priority.First });

                // Patch CheckConfirmSettle to sync VGE's tile selection
                MpCompat.harmony.Patch(
                    AccessTools.DeclaredMethod(typeof(SettlementProximityGoodwillUtility), "CheckConfirmSettle"),
                    prefix: new HarmonyMethod(typeof(VanillaGravshipExpanded), nameof(PreCheckConfirmSettle)));

                MP.RegisterSyncMethod(typeof(VanillaGravshipExpanded), nameof(SyncedGravshipTileSelected));

                // Sync VGE's replacement launch action in the prelaunch confirmation dialog.
                // VGE replaces the vanilla launchAction (synced by MP as lambda 0 in Apply)
                // with its own delegate, so MP's sync never fires for VGE launches.
                // Two prefixes: high-priority captures original, low-priority detects replacement.
                var preLaunchMethod = AccessTools.DeclaredMethod(typeof(GravshipUtility), "PreLaunchConfirmation");
                MpCompat.harmony.Patch(preLaunchMethod,
                    prefix: new HarmonyMethod(typeof(VanillaGravshipExpanded), nameof(CaptureOriginalLaunchAction))
                        { priority = Priority.First });
                MpCompat.harmony.Patch(preLaunchMethod,
                    prefix: new HarmonyMethod(typeof(VanillaGravshipExpanded), nameof(WrapVgeLaunchAction))
                        { priority = Priority.Last });

                MP.RegisterSyncMethod(typeof(VanillaGravshipExpanded), nameof(SyncedVgeLaunchConfirm));

                // Reflect VGE methods used in the launch action reconstruction
                destroyTreesAroundSubstructureMethod = AccessTools.DeclaredMethod(
                    typeof(WorldComponent_GravshipController), "DestroyTreesAroundSubstructure");
                consumeFuelMethod = AccessTools.DeclaredMethod(typeof(Building_GravEngine), "ConsumeFuel");
                initiateTakeoffMethod = AccessTools.DeclaredMethod(
                    typeof(WorldComponent_GravshipController), "InitiateTakeoff");
                validSubstructureProperty = AccessTools.DeclaredProperty(typeof(Building_GravEngine), "ValidSubstructure");
            }

            #endregion

            #region Mod settings sync (orbitalObjectsMultiplier)

            {
                // orbitalObjectsMultiplier is a per-player mod setting that affects
                // WorldComponent_LocationGenerator.WorldComponentTick via transpiler.
                // If host and client have different values, world RNG state diverges.
                // Force a consistent value (default 2.0) during world tick in MP.
                var settingsType = AccessTools.TypeByName("VanillaGravshipExpanded.GravshipsMod_Settings");
                orbitalObjectsMultiplierField = AccessTools.StaticFieldRefAccess<float>(
                    AccessTools.DeclaredField(settingsType, "orbitalObjectsMultiplier"));

                MpCompat.harmony.Patch(
                    AccessTools.DeclaredMethod(typeof(WorldComponent_LocationGenerator), "WorldComponentTick"),
                    prefix: new HarmonyMethod(typeof(VanillaGravshipExpanded), nameof(PreLocationGeneratorTick)),
                    postfix: new HarmonyMethod(typeof(VanillaGravshipExpanded), nameof(PostLocationGeneratorTick)));

                MpCompat.harmony.Patch(
                    AccessTools.DeclaredMethod(typeof(WorldComponent_LocationGenerator), "GenerateUntilTarget", new[] { typeof(PlanetLayer) }),
                    prefix: new HarmonyMethod(typeof(VanillaGravshipExpanded), nameof(PreLocationGeneratorTick)),
                    postfix: new HarmonyMethod(typeof(VanillaGravshipExpanded), nameof(PostLocationGeneratorTick)));
            }

            #endregion

            #region TakeoffEnded map decision sync

            {
                // VGE's TakeoffEnded patch shows a Dialog_MessageBox asking the player to
                // settle or abandon the map after gravship launch. The dialog is per-client
                // and the button actions (settle/abandon) are not synced.
                // Patch VGE's prefix to replace the dialog actions with synced versions.
                var takeoffPatchType = AccessTools.TypeByName(
                    "VanillaGravshipExpanded.WorldComponent_GravshipController_TakeoffEnded_Patch");
                MpCompat.harmony.Patch(
                    AccessTools.DeclaredMethod(takeoffPatchType, "Prefix"),
                    prefix: new HarmonyMethod(typeof(VanillaGravshipExpanded), nameof(PreTakeoffEndedPatch)));

                canEverKeepThisMapMethod = AccessTools.Method(
                    "VanillaGravshipExpanded.WorldComponent_GravshipController_TakeoffEnded_Patch:CanEverKeepThisMap");
                shouldAlwaysKeepThisMapMethod = AccessTools.Method(
                    "VanillaGravshipExpanded.WorldComponent_GravshipController_TakeoffEnded_Patch:ShouldAlwaysKeepThisMap");
                shouldHaveKeepMapUIMethod = AccessTools.Method(
                    "VanillaGravshipExpanded.WorldComponent_GravshipController_TakeoffEnded_Patch:ShouldHaveKeepMapUI");

                MP.RegisterSyncMethod(typeof(VanillaGravshipExpanded), nameof(SyncedSettleTile));
                MP.RegisterSyncMethod(typeof(VanillaGravshipExpanded), nameof(SyncedAbandonTile));
            }

            #endregion

            #region Gravship naming dialog sync

            {
                // Dialog_NamePlayerGravship is created from UpdateSubstructureIfNeeded during tick.
                // Each client can type and submit a different name independently.
                // Sync the Named method and close the dialog on all clients.
                MpCompat.harmony.Patch(
                    AccessTools.DeclaredMethod(typeof(Dialog_NamePlayerGravship), "Named"),
                    prefix: new HarmonyMethod(typeof(VanillaGravshipExpanded), nameof(PreGravshipNamed)));

                MP.RegisterSyncMethod(typeof(VanillaGravshipExpanded), nameof(SyncedGravshipNamed));
            }

            #endregion

            #region Gizmo_OxygenProvider

            {
                var compType = AccessTools.TypeByName("VanillaGravshipExpanded.CompApparelOxygenProvider");
                oxygenRechargeAtField = MP.RegisterSyncField(compType, "rechargeAtCharges").SetBufferChanges();
                oxygenAutoRechargeField = MP.RegisterSyncField(compType, "automaticRechargeEnabled");

                oxygenGizmoType = AccessTools.TypeByName("VanillaGravshipExpanded.Gizmo_OxygenProvider");
                oxygenProviderFromGizmo = AccessTools.FieldRefAccess<object>(oxygenGizmoType, "oxygenProvider");

                // Watch both fields around GizmoOnGUI with explicit WatchBegin/WatchEnd.
                MpCompat.harmony.Patch(
                    AccessTools.DeclaredMethod(typeof(Gizmo_Slider), nameof(Gizmo_Slider.GizmoOnGUI)),
                    prefix: new HarmonyMethod(typeof(VanillaGravshipExpanded), nameof(PreOxygenGizmoOnGUI)),
                    postfix: new HarmonyMethod(typeof(VanillaGravshipExpanded), nameof(PostOxygenGizmoOnGUI)));
            }

            #endregion
        }

        #region Patches

        /// <summary>
        /// Intercept the black box convert gravdata lambda and sync it.
        /// The lambda captures a local variable (currentProject), so we
        /// replicate the logic in SyncedBlackBoxConvert instead.
        /// </summary>
        private static bool PreBlackBoxConvert(object __instance)
        {
            if (!MP.IsInMultiplayer)
                return true;

            // __instance is the display class; get the building from its fields
            var buildingField = __instance.GetType().GetField("<>4__this");
            if (buildingField?.GetValue(__instance) is Thing building)
                SyncedBlackBoxConvert(building);

            return false;
        }


        private static void SyncedBlackBoxConvert(Thing blackBox)
        {
            var currentProject = Find.ResearchManager.currentProj;
            if (currentProject == null || currentProject.IsFinished)
                return;

            var storedField = AccessTools.Field(blackBox.GetType(), "storedGravdata");
            if (storedField == null)
                return;

            var stored = (float)storedField.GetValue(blackBox);
            if (stored <= 0)
                return;

            float progressNeeded = currentProject.Cost - Find.ResearchManager.GetProgress(currentProject);
            float gravdataToConvert = Math.Min(stored, progressNeeded);
            Find.ResearchManager.AddProgress(currentProject, gravdataToConvert);
            storedField.SetValue(blackBox, stored - gravdataToConvert);
            Messages.Message("VGE_ConvertedGravdataToResearch".Translate(gravdataToConvert, currentProject.LabelCap),
                MessageTypeDefOf.TaskCompletion);
        }

        /// <summary>
        /// In MP, replace the Find.Selector iteration in SetSelectedVacCheckpointsTo
        /// with per-building synced calls.
        /// </summary>
        private static bool PreSetSelectedVacCheckpoints(float resistance, bool allowDrafted)
        {
            if (!MP.IsInMultiplayer)
                return true;

            foreach (var selected in Find.Selector.SelectedObjects)
            {
                if (selected is Thing thing && thing.GetType().Name == "Building_VacCheckpoint"
                    && thing.Faction == Faction.OfPlayer)
                {
                    SyncedSetVacCheckpoint(thing, resistance, allowDrafted);
                }
            }

            return false;
        }


        private static void SyncedSetVacCheckpoint(Thing checkpoint, float resistance, bool allowDrafted)
        {
            resistance = UnityEngine.Mathf.Clamp01(resistance);
            vacCheckpointResistanceField(checkpoint) = resistance;
            vacCheckpointAllowDraftedField(checkpoint) = allowDrafted;

            var map = checkpoint.Map;
            if (map?.Biome?.inVacuum == true)
            {
                // Clear reachability cache for player pawns on vacuum maps
                var tmpEntries = new List<ReachabilityCache.CachedEntry>();

                foreach (var entry in map.reachability.cache.cacheDict)
                {
                    var pawn = entry.Key.TraverseParms.pawn;
                    if (pawn != null && pawn.Faction == Faction.OfPlayer)
                        tmpEntries.Add(entry.Key);
                }

                for (var i = 0; i < tmpEntries.Count; i++)
                    map.reachability.cache.cacheDict.Remove(tmpEntries[i]);
            }
        }

        private static void PreMaintenanceDoWindowContents()
        {
            if (!MP.IsInMultiplayer)
                return;

            MP.WatchBegin();
            maintenanceThresholdSync.Watch();
        }

        private static void PostMaintenanceDoWindowContents()
        {
            if (!MP.IsInMultiplayer)
                return;

            MP.WatchEnd();
        }

        /// <summary>
        /// Capture the asteroid name before DoWindowContents runs.
        /// If the name changed after (user clicked OK), revert and sync.
        /// </summary>
        private static void PreRenameDoWindowContents(object __instance)
        {
            if (!MP.IsInMultiplayer)
                return;

            var worldObj = renameWindowWorldObjectField(__instance) as SpaceMapParent;
            cachedAsteroidName = worldObj?.Name;
        }

        private static void PostRenameDoWindowContents(object __instance)
        {
            if (!MP.IsInMultiplayer)
                return;

            var worldObj = renameWindowWorldObjectField(__instance) as SpaceMapParent;
            if (worldObj == null || cachedAsteroidName == worldObj.Name)
                return;

            // Name changed — revert locally and sync
            var newName = worldObj.Name;
            worldObj.Name = cachedAsteroidName;
            SyncedRenameAsteroid(worldObj, newName);
        }


        private static void SyncedRenameAsteroid(WorldObject worldObject, string name)
        {
            if (worldObject is SpaceMapParent smp)
                smp.Name = name;
        }

        /// <summary>
        /// Clear stale VGE state before ShowRitualBeginWindow.
        /// In tick context (pilot console job), state must be null so VGE starts
        /// a fresh tile picker flow. In sync context (our SyncedGravshipTileSelected),
        /// state is needed for VGE's prefix to fall through to the ritual dialog.
        /// </summary>
        private static void PreShowRitualClearStaleState()
        {
            if (MP.IsInMultiplayer && !inSyncedTileSelectedFlow)
                vgeLaunchStateField() = null;
        }

        /// <summary>
        /// Intercept CheckConfirmSettle when VGE's gravship launch state is active.
        /// VGE replaces the settleAction delegate (which MP syncs as lambda b__5)
        /// with its own delegate, so MP's SyncDelegate never fires. We sync the
        /// tile selection ourselves via SyncedGravshipTileSelected.
        /// </summary>
        private static bool PreCheckConfirmSettle(PlanetTile tile, Building_GravEngine gravEngine)
        {
            if (!MP.IsInMultiplayer)
                return true;

            // Only intercept VGE gravship flow (state is set + gravEngine present)
            if (gravEngine == null || vgeLaunchStateField() == null)
                return true;

            // Block CheckConfirmSettle entirely in VGE flow — both UI and sync contexts.
            // VGE replaces the settleAction with its own delegate that calls ShowRitualBeginWindow,
            // and CheckConfirmSettle may show a faction goodwill dialog (unsyncable in sync context).
            // We handle the tile selection ourselves via SyncedGravshipTileSelected.
            if (!MP.IsExecutingSyncCommand)
                SyncedGravshipTileSelected(gravEngine, tile);

            return false;
        }

        /// <summary>
        /// Synced tile selection for VGE's gravship launch flow.
        /// Closes the tile picker and GravshipTravelSession, stores the tile,
        /// switches to map view, and calls ShowRitualBeginWindow so MP creates
        /// a RitualSession for the launch ritual.
        /// </summary>

        private static void SyncedGravshipTileSelected(Building_GravEngine gravEngine, PlanetTile tile)
        {
            var state = vgeLaunchStateField();
            if (state == null)
                return;

            // Guard against duplicate tile selections (both players picked tiles).
            // If the GravshipTravelSession was already closed by a prior call, skip.
            if (hasGravshipSession != null && !hasGravshipSession(gravEngine.Map.Tile))
                return;

            // Store tile in VGE's state (used later by VGE's ritual outcome patches)
            vgeLaunchStateTargetTileField(state) = tile;
            vgeCheckConfirmSettleTargetTileField() = tile;

            // Save ShowRitualBeginWindow args from state before tile picker cleanup
            var ritualInstance = vgeLaunchStateInstanceField(state) as Precept_Ritual;
            var targetInfo = vgeLaunchStateTargetInfoField(state);
            var forObligation = vgeLaunchStateForObligationField(state) as RitualObligation;
            var selectedPawn = vgeLaunchStateSelectedPawnField(state);
            var forcedForRole = vgeLaunchStateForcedForRoleField(state) as Dictionary<string, Pawn>;

            // Close tile picker using StopTargetingInt to bypass VGE's
            // TilePicker_StopTargeting_Patch (which would clear state too early)
            Find.World.renderer.wantedMode = WorldRenderMode.None;
            if (stopTilePickerInt != null)
                stopTilePickerInt.Invoke(Find.TilePicker, null);

            // Close the GravshipTravelSession that MP created (it pauses the map,
            // which would prevent the ritual from running)
            closeGravshipSession?.Invoke(gravEngine.Map.Tile);

            // Switch to map view (replicates VGE's settleAction UI operations)
            CameraJumper.TryHideWorld();
            Current.Game.CurrentMap = gravEngine.Map;
            Find.CameraDriver.JumpToCurrentMapLoc(gravEngine.Position);

            // Call ShowRitualBeginWindow — VGE state is set so VGE's prefix falls
            // through to the original. In ExecutingCmds, MP's CancelDialogBeginRitual
            // intercepts the dialog creation and creates a RitualSession instead.
            // Set flag so PreShowRitualClearStaleState doesn't clear state during this call.
            inSyncedTileSelectedFlow = true;
            try
            {
                ritualInstance?.ShowRitualBeginWindow(targetInfo, forObligation,
                    selectedPawn, forcedForRole);
            }
            finally
            {
                inSyncedTileSelectedFlow = false;
            }

            // Clear VGE state now that ShowRitualBeginWindow has consumed it.
            // VGE's Window_PostClose_Patch and TilePicker_StopTargeting_Patch clear state
            // in UI context (non-deterministic between clients). By clearing here in sync
            // context, we ensure state is always null for the next launch attempt,
            // regardless of how this launch ends (complete, cancel, or interrupted).
            // The target tile is stored separately in vgeCheckConfirmSettleTargetTileField
            // and LordJob_Ritual_ExposeData_Patch.targetTile, so it's not lost.
            vgeLaunchStateField() = null;
        }

        /// <summary>
        /// High-priority prefix on PreLaunchConfirmation: captures the original
        /// launchAction before VGE's prefix can replace it.
        /// </summary>
        private static void CaptureOriginalLaunchAction(ref Action launchAction)
        {
            if (!MP.IsInMultiplayer)
                return;

            capturedOriginalLaunchAction = launchAction;
        }

        /// <summary>
        /// Low-priority prefix on PreLaunchConfirmation: if VGE replaced the
        /// launchAction (detected by comparing to captured original), wrap it
        /// with a synced call so the launch executes on all clients.
        /// </summary>
        private static void WrapVgeLaunchAction(Building_GravEngine engine, ref Action launchAction)
        {
            if (!MP.IsInMultiplayer)
                return;

            // If VGE didn't modify the action, vanilla MP sync handles it
            if (launchAction == capturedOriginalLaunchAction)
                return;

            launchAction = () =>
            {
                if (!MP.IsExecutingSyncCommand)
                    SyncedVgeLaunchConfirm(engine);
            };
        }

        /// <summary>
        /// Synced VGE launch confirmation. Replicates VGE's replacement launchAction
        /// (from GravshipUtility_PreLaunchConfirmation_Patch) on all clients.
        /// The target tile was stored in vgeCheckConfirmSettleTargetTileField during
        /// the earlier tile selection step.
        /// </summary>

        private static void SyncedVgeLaunchConfirm(Building_GravEngine engine)
        {
            var tile = vgeCheckConfirmSettleTargetTileField();
            if (tile == null)
                return;

            // Close the prelaunch confirmation dialog on all clients
            var dialogPrefix = "ConfirmGravEngineLaunch".Translate().RawText;
            foreach (var window in Find.WindowStack.Windows)
            {
                if (window is Dialog_MessageBox msgBox && msgBox.text.RawText.StartsWith(dialogPrefix))
                {
                    msgBox.Close();
                    break;
                }
            }

            // Replicate VGE's launch action:
            // DestroyTreesAroundSubstructure, ConsumeFuel, InitiateTakeoff, clear state
            if (destroyTreesAroundSubstructureMethod != null && validSubstructureProperty != null)
            {
                var substructure = validSubstructureProperty.GetValue(engine);
                destroyTreesAroundSubstructureMethod.Invoke(null, new object[] { engine.Map, substructure, 2, null });
            }

            Find.World.renderer.wantedMode = WorldRenderMode.None;
            consumeFuelMethod?.Invoke(engine, new object[] { tile });
            initiateTakeoffMethod?.Invoke(Find.GravshipController, new object[] { engine, tile });
            SoundDefOf.Gravship_Launch.PlayOneShotOnCamera();
            vgeLaunchStateField() = null;
        }

        /// <summary>
        /// Intercept VGE's TakeoffEnded patch to replace dialog button actions
        /// with synced versions. VGE creates Dialog_MessageBox with settle/abandon
        /// actions that are per-client and unsynced.
        /// </summary>
        private static bool PreTakeoffEndedPatch(WorldComponent_GravshipController __0)
        {
            if (!MP.IsInMultiplayer)
                return true;

            if (__0.mapHasGravAnchor || __0.map?.info?.parent == null)
                return false;

            var map = __0.map;
            var mapParent = map.Parent;
            if (mapParent == null)
                return false;

            if (canEverKeepThisMapMethod != null && !(bool)canEverKeepThisMapMethod.Invoke(null, new object[] { mapParent }))
                return false;

            if (shouldAlwaysKeepThisMapMethod != null && (bool)shouldAlwaysKeepThisMapMethod.Invoke(null, new object[] { mapParent }))
            {
                __0.mapHasGravAnchor = true;
                if (mapParent.CanBeSettled && !map.attackTargetsCache.TargetsHostileToColony
                    .Any(item => GenHostility.IsActiveThreatToPlayer(item)))
                {
                    Find.WindowStack.Add(new Dialog_MessageBox(
                        "VGE_MapDecisionSettleText".Translate(),
                        "VGE_DontSettle".Translate(),
                        null,
                        "VGE_SettleMap".Translate(),
                        () => SyncedSettleTile(mapParent),
                        buttonADestructive: false
                    ));
                }
            }
            else if (shouldHaveKeepMapUIMethod != null && (bool)shouldHaveKeepMapUIMethod.Invoke(null, new object[] { mapParent }))
            {
                __0.mapHasGravAnchor = true;
                Find.WindowStack.Add(new Dialog_MessageBox(
                    "VGE_MapDecisionText".Translate(),
                    "VGE_DiscardMap".Translate(),
                    () => SyncedAbandonTile(mapParent),
                    "VGE_KeepMap".Translate(),
                    () => SyncedSettleTile(mapParent),
                    buttonADestructive: true
                ));
            }
            else
            {
                __0.mapHasGravAnchor = true;
                SyncedAbandonTile(mapParent);
            }

            return false;
        }


        private static void SyncedSettleTile(MapParent mapParent)
        {
            CloseMapDecisionDialog();
            if (mapParent?.Map != null && mapParent.CanBeSettled)
                SettleInExistingMapUtility.Settle(mapParent.Map);
        }


        private static void SyncedAbandonTile(MapParent mapParent)
        {
            CloseMapDecisionDialog();
            if (mapParent?.Map == null)
                return;

            var map = mapParent.Map;
            if (mapParent is Settlement settlement && settlement.Faction == Faction.OfPlayer)
            {
                mapParent.Abandon(wasGravshipLaunch: false);
            }
            else
            {
                mapParent.ShouldRemoveMapNow(out var removeWorldObject);
                Current.Game.DeinitAndRemoveMap(map, false);
                if (!mapParent.Destroyed && (removeWorldObject || mapParent.forceRemoveWorldObjectWhenMapRemoved))
                    mapParent.Destroy();
            }
        }

        private static void CloseMapDecisionDialog()
        {
            for (var i = Find.WindowStack.Windows.Count - 1; i >= 0; i--)
            {
                if (Find.WindowStack.Windows[i] is Dialog_MessageBox msgBox)
                    msgBox.Close();
            }
        }

        /// <summary>
        /// Intercept Dialog_NamePlayerGravship.Named to sync the gravship name.
        /// Without this, each client can submit a different name independently.
        /// </summary>
        private static bool PreGravshipNamed(Window __instance, string s)
        {
            if (!MP.IsInMultiplayer)
                return true;

            // Get the engine from the dialog via reflection
            var engineField = AccessTools.Field(typeof(Dialog_NamePlayerGravship), "engine");
            if (engineField?.GetValue(__instance) is Building_GravEngine engine)
            {
                if (!MP.IsExecutingSyncCommand)
                    SyncedGravshipNamed(engine, s);
            }

            return false;
        }


        private static void SyncedGravshipNamed(Building_GravEngine engine, string name)
        {
            engine.RenamableLabel = name;
            engine.nameHidden = false;
            Messages.Message("PlayerGravshipGainsName".Translate(name), MessageTypeDefOf.TaskCompletion, false);

            // Close the naming dialog on all clients
            for (var i = Find.WindowStack.Windows.Count - 1; i >= 0; i--)
            {
                if (Find.WindowStack.Windows[i] is Dialog_NamePlayerGravship)
                {
                    Find.WindowStack.Windows[i].Close();
                    break;
                }
            }
        }

        /// <summary>
        /// Force orbitalObjectsMultiplier to its default value during
        /// WorldComponent_LocationGenerator methods in MP, so host and client
        /// produce identical results regardless of per-player mod settings.
        /// </summary>
        private static void PreLocationGeneratorTick()
        {
            if (!MP.IsInMultiplayer)
                return;

            savedMultiplier = orbitalObjectsMultiplierField();
            orbitalObjectsMultiplierField() = 2f; // orbitalObjectsMultiplierBase default
        }

        private static void PostLocationGeneratorTick()
        {
            if (!MP.IsInMultiplayer)
                return;

            orbitalObjectsMultiplierField() = savedMultiplier;
        }

        private static void PreOxygenGizmoOnGUI(Gizmo_Slider __instance)
        {
            if (!MP.IsInMultiplayer || __instance.GetType() != oxygenGizmoType)
                return;

            var comp = oxygenProviderFromGizmo(__instance);
            MP.WatchBegin();
            oxygenRechargeAtField.Watch(comp);
            oxygenAutoRechargeField.Watch(comp);
        }

        private static void PostOxygenGizmoOnGUI(Gizmo_Slider __instance)
        {
            if (!MP.IsInMultiplayer || __instance.GetType() != oxygenGizmoType)
                return;

            MP.WatchEnd();
        }

        /// <summary>
        /// Intercept the paste color gizmo action. ColorClipboard is per-client
        /// state, so we capture the color value and sync per-barrier.
        /// </summary>
        private static bool PrePasteBarrierColor()
        {
            if (!MP.IsInMultiplayer)
                return true;

            var clipboard = (Color?)colorClipboardFieldInfo.GetValue(null);
            if (clipboard == null)
            {
                Messages.Message("ClipboardInvalidColor".Translate(), MessageTypeDefOf.RejectInput, false);
                return false;
            }

            foreach (var obj in Find.Selector.SelectedObjects)
            {
                if (vacBarrierRecolorableType.IsInstanceOfType(obj))
                    SyncedSetBarrierColor((Thing)obj, clipboard.Value.r, clipboard.Value.g, clipboard.Value.b);
            }

            return false;
        }

        /// <summary>
        /// Intercept Dialog_VacBarrierColorPicker.SaveColor to sync
        /// the color change per-barrier instead of applying locally.
        /// </summary>
        private static bool PreVacBarrierSaveColor(object __instance, Color color)
        {
            if (!MP.IsInMultiplayer)
                return true;

            var barriers = vacBarrierDialogBarriersField(__instance);
            if (barriers is Array barrierArray)
            {
                foreach (object barrier in barrierArray)
                    SyncedSetBarrierColor((Thing)barrier, color.r, color.g, color.b);
            }

            return false;
        }


        private static void SyncedSetBarrierColor(Thing barrier, float r, float g, float b)
        {
            barrierColorField(barrier) = new Color(r, g, b);
            notifyBarrierColorChangedMethod?.Invoke(barrier, null);
        }

        #endregion
    }
}
