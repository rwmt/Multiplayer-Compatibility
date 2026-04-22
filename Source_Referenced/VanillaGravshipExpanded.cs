using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using VanillaGravshipExpanded;
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
        // Window_SetDesiredMaintenance
        private static ISyncField maintenanceThresholdSync;

        // Window_RenameAsteroid
        private static string cachedAsteroidName;

        // Flag to preserve VGE state during our SyncedGravshipTileSelected flow
        private static bool inSyncedTileSelectedFlow;

        // Gizmo_OxygenProvider sync field (#880 workaround)
        private static ISyncField oxygenTargetValuePctField;

        // VGE launch flow — MP internals (not publicized, reached via reflection)
        private static Action<PlanetTile> closeGravshipSession;
        private static Func<PlanetTile, bool> hasGravshipSession;

        // VGE PreLaunchConfirmation sync — capture original before VGE's prefix replaces it
        private static Action capturedOriginalLaunchAction;

        // MP internals reflection for MultiFaction area sync.
        // MP.Client types are deliberately NOT publicized — direct reflection required.
        private static MethodInfo mpCompMethod;
        private static FieldInfo factionDataField;
        private static FieldInfo areaManagerField;

        public VanillaGravshipExpanded(ModContentPack mod)
        {
            LongEventHandler.ExecuteWhenFinished(LatePatch);

            // RNG fixes — rendering context only (frame-rate-dependent Rand consumption)
            PatchingUtilities.PatchPushPopRand("VanillaGravshipExpanded.Projectile_Gauss:DrawAt");
            PatchingUtilities.PatchPushPopRand("VanillaGravshipExpanded.Projectile_JavelinRocket:DrawAt");
        }

        private static void LatePatch()
        {
            #region Turret-terminal linking

            {
                MP.RegisterSyncMethod(typeof(Building_TargetingTerminal), "LinkTo");
                MP.RegisterSyncMethod(typeof(Building_TargetingTerminal), "Unlink");
                MP.RegisterSyncMethod(typeof(Building_GravshipTurret), "LinkTo");
                MP.RegisterSyncMethod(typeof(Building_GravshipTurret), "Unlink");
            }

            #endregion

            #region World artillery

            {
                MP.RegisterSyncMethod(typeof(CompWorldArtillery), nameof(CompWorldArtillery.StartAttack));
                MP.RegisterSyncMethod(typeof(CompWorldArtillery), nameof(CompWorldArtillery.Reset));
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
                // Building_GravshipBlackBox — convert gravdata to research (lambda 0, captures this + currentProject)
                MpCompat.RegisterLambdaDelegate(typeof(Building_GravshipBlackBox), "GetGizmos", 0);

                // Building_SealantPopper — toggle autoRebuild (lambda 1, after isActive getter at 0)
                MpCompat.RegisterLambdaMethod(typeof(Building_SealantPopper), "GetGizmos", 1);

                // Building_Agrocell — toggle SunLampOn (lambda 0)
                MpCompat.RegisterLambdaMethod(typeof(Building_Agrocell), "GetGizmos", 0);

                // CompGravheatAbsorber — absorb gravheat (method group, not lambda)
                MP.RegisterSyncMethod(typeof(CompGravheatAbsorber), "AbsorbGravheat");
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
                MpCompat.harmony.Patch(
                    AccessTools.DeclaredMethod(typeof(Dialog_ConfigureVacuumRequirement), "SetSelectedVacCheckpointsTo"),
                    prefix: new HarmonyMethod(typeof(VanillaGravshipExpanded), nameof(PreSetSelectedVacCheckpoints)));

                MP.RegisterSyncMethod(typeof(VanillaGravshipExpanded), nameof(SyncedSetVacCheckpoints))
                    .SetContext(SyncContext.MapSelected);
            }

            #endregion

            #region Window_SetDesiredMaintenance

            {
                maintenanceThresholdSync = MP.RegisterSyncField(typeof(World_ExposeData_Patch), nameof(World_ExposeData_Patch.maintenanceThreshold));

                MpCompat.harmony.Patch(
                    AccessTools.DeclaredMethod(typeof(Window_SetDesiredMaintenance), "DoWindowContents"),
                    prefix: new HarmonyMethod(typeof(VanillaGravshipExpanded), nameof(PreMaintenanceDoWindowContents)),
                    postfix: new HarmonyMethod(typeof(VanillaGravshipExpanded), nameof(PostMaintenanceDoWindowContents)));
            }

            #endregion

            #region Window_RenameAsteroid

            {
                MpCompat.harmony.Patch(
                    AccessTools.DeclaredMethod(typeof(Window_RenameAsteroid), "DoWindowContents"),
                    prefix: new HarmonyMethod(typeof(VanillaGravshipExpanded), nameof(PreRenameDoWindowContents)),
                    postfix: new HarmonyMethod(typeof(VanillaGravshipExpanded), nameof(PostRenameDoWindowContents)));

                MP.RegisterSyncMethod(typeof(VanillaGravshipExpanded), nameof(SyncedRenameAsteroid));
            }

            #endregion

            #region Building_Gravlift (launch to orbit)

            {
                // Ordinal 2: gizmo action — sets IsGravliftLaunch, calls ShowLaunchRitual.
                // Captures locals (isInOrbit, comp) via display class, so must use Delegate not Method.
                // (0: LINQ Select projection, 1: LINQ FirstOrDefault predicate — both non-capturing in <>c)
                MpCompat.RegisterLambdaDelegate(typeof(Building_Gravlift), "GetGizmos", 2);
            }

            #endregion

            #region Building_VacBarrier_Recolorable color sync

            {
                // Prefix on paste gizmo lambda — reads per-client ColorClipboard,
                // so we must capture the color and sync per-barrier.
                var pasteLambda = MpMethodUtil.GetLambda(typeof(Building_VacBarrier_Recolorable), "GetGizmos", lambdaOrdinal: 2);
                MpCompat.harmony.Patch(pasteLambda,
                    prefix: new HarmonyMethod(typeof(VanillaGravshipExpanded), nameof(PrePasteBarrierColor)));

                MpCompat.harmony.Patch(
                    AccessTools.DeclaredMethod(typeof(Dialog_VacBarrierColorPicker), "SaveColor"),
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

                // MP's GravshipTravelUtils.CloseSessionAt / HasSessionAt (not in public API).
                // MP internals are NOT publicized — reached via reflection.
                var travelUtilsType = AccessTools.TypeByName("Multiplayer.Client.Persistent.GravshipTravelUtils");
                var closeMethod = AccessTools.DeclaredMethod(travelUtilsType, "CloseSessionAt");
                if (closeMethod != null)
                    closeGravshipSession = (Action<PlanetTile>)Delegate.CreateDelegate(typeof(Action<PlanetTile>), closeMethod);

                var hasMethod = AccessTools.DeclaredMethod(travelUtilsType, "HasSessionAt");
                if (hasMethod != null)
                    hasGravshipSession = (Func<PlanetTile, bool>)Delegate.CreateDelegate(typeof(Func<PlanetTile, bool>), hasMethod);

                // Clear stale VGE state before ShowRitualBeginWindow runs.
                // VGE's state is a per-process static that may be left set on one client
                // but not the other (cleared by UI-driven patches like Window_PostClose_Patch
                // or TilePicker_StopTargeting_Patch). In tick context (pilot console job),
                // always clear state so both clients start fresh. In sync context
                // (our SyncedGravshipTileSelected), state is needed — don't clear.
                MpCompat.harmony.Patch(
                    AccessTools.DeclaredMethod(typeof(Precept_Ritual), nameof(Precept_Ritual.ShowRitualBeginWindow)),
                    prefix: new HarmonyMethod(typeof(VanillaGravshipExpanded), nameof(PreShowRitualClearStaleState))
                    { priority = Priority.First });

                // Patch CheckConfirmSettle to sync VGE's tile selection
                MpCompat.harmony.Patch(
                    AccessTools.DeclaredMethod(typeof(SettlementProximityGoodwillUtility), nameof(SettlementProximityGoodwillUtility.CheckConfirmSettle)),
                    prefix: new HarmonyMethod(typeof(VanillaGravshipExpanded), nameof(PreCheckConfirmSettle)));

                MP.RegisterSyncMethod(typeof(VanillaGravshipExpanded), nameof(SyncedGravshipTileSelected));

                // Sync VGE's replacement launch action in the prelaunch confirmation dialog.
                // VGE replaces the vanilla launchAction (synced by MP as lambda 0 in Apply)
                // with its own delegate, so MP's sync never fires for VGE launches.
                // Two prefixes: high-priority captures original, low-priority detects replacement.
                var preLaunchMethod = AccessTools.DeclaredMethod(typeof(GravshipUtility), nameof(GravshipUtility.PreLaunchConfirmation));
                MpCompat.harmony.Patch(preLaunchMethod,
                    prefix: new HarmonyMethod(typeof(VanillaGravshipExpanded), nameof(CaptureOriginalLaunchAction))
                    { priority = Priority.First });
                MpCompat.harmony.Patch(preLaunchMethod,
                    prefix: new HarmonyMethod(typeof(VanillaGravshipExpanded), nameof(WrapVgeLaunchAction))
                    { priority = Priority.Last });

                MP.RegisterSyncMethod(typeof(VanillaGravshipExpanded), nameof(SyncedVgeLaunchConfirm));
            }

            #endregion

            #region TakeoffEnded map decision sync

            {
                // VGE's TakeoffEnded patch shows a Dialog_MessageBox asking the player to
                // settle or abandon the map after gravship launch. The button actions
                // (settle/abandon) are local functions — per-client and unsynced.
                // Let VGE create the dialog natively, then swap the button actions with
                // synced versions in a postfix. No VGE condition logic is copied.
                MpCompat.harmony.Patch(
                    AccessTools.DeclaredMethod(typeof(WorldComponent_GravshipController_TakeoffEnded_Patch), "Prefix"),
                    postfix: new HarmonyMethod(typeof(VanillaGravshipExpanded), nameof(PostTakeoffEndedPatch)));

                MP.RegisterSyncMethod(typeof(VanillaGravshipExpanded), nameof(SyncedSettleTile));
                MP.RegisterSyncMethod(typeof(VanillaGravshipExpanded), nameof(SyncedAbandonTile));
            }

            #endregion

            #region Gravship naming dialog sync

            {
                // Dialog_NamePlayerGravship is created from UpdateSubstructureIfNeeded during tick.
                // Each client can type and submit a different name independently.
                // Sync the Named method — the prefix lets the original run during sync execution
                // so we call vanilla's Named on the dialog instance (open on all clients).
                MpCompat.harmony.Patch(
                    AccessTools.DeclaredMethod(typeof(Dialog_NamePlayerGravship), "Named"),
                    prefix: new HarmonyMethod(typeof(VanillaGravshipExpanded), nameof(PreGravshipNamed)));

                MP.RegisterSyncMethod(typeof(VanillaGravshipExpanded), nameof(SyncedGravshipNamed));
            }

            #endregion

            #region Gizmo_OxygenProvider

            {
                MP.RegisterSyncMethod(AccessTools.PropertySetter(typeof(CompApparelOxygenProvider), nameof(CompApparelOxygenProvider.AutomaticRechargeEnabled)));

                oxygenTargetValuePctField = MP.RegisterSyncField(typeof(Gizmo_OxygenProvider), "targetValuePct").SetBufferChanges();

                // MP issue #880 workaround: RegisterSyncField resolves targetType via ReflectedType,
                // which returns Gizmo_Slider (the base declaring "targetValuePct"), not Gizmo_OxygenProvider.
                // Rewrite the private targetType field so Watch() matches our subclass instance.
                AccessTools.Field(oxygenTargetValuePctField.GetType(), "targetType")
                    .SetValue(oxygenTargetValuePctField, typeof(Gizmo_OxygenProvider));

                MP.RegisterSyncWorker<Gizmo_Slider>(SyncOxygenGizmo, typeof(Gizmo_OxygenProvider));

                MpCompat.harmony.Patch(
                    AccessTools.DeclaredMethod(typeof(Gizmo_Slider), nameof(Gizmo_Slider.GizmoOnGUI)),
                    prefix: new HarmonyMethod(typeof(VanillaGravshipExpanded), nameof(PreOxygenGizmoOnGUI)),
                    postfix: new HarmonyMethod(typeof(VanillaGravshipExpanded), nameof(PostOxygenGizmoOnGUI)));
            }

            #endregion

            #region MultiFaction sync

            {
                // MP internals (Multiplayer.Client.*) are NOT publicized — reached via reflection.
                mpCompMethod = AccessTools.Method(AccessTools.TypeByName("Multiplayer.Client.Extensions"), "MpComp");
                factionDataField = AccessTools.Field(AccessTools.TypeByName("Multiplayer.Client.MultiplayerMapComp"), "factionData");
                areaManagerField = AccessTools.Field(AccessTools.TypeByName("Multiplayer.Client.FactionMapData"), "areaManager");

                MpCompat.harmony.Patch(
                    AccessTools.Method(AccessTools.TypeByName("Multiplayer.Client.MapSetup"), "InitNewFactionData"),
                    postfix: new HarmonyMethod(typeof(VanillaGravshipExpanded), nameof(PostMapSetupInitNewFactionData)));
            }
            #endregion
        }

        #region Patches

        /// <summary>
        /// In MP, redirect SetSelectedVacCheckpointsTo through a synced call.
        /// SyncContext.MapSelected restores the initiating player's selection on
        /// all clients, so the original method sees the correct checkpoints.
        /// </summary>
        private static bool PreSetSelectedVacCheckpoints(float resistance, bool allowDrafted)
        {
            if (!MP.IsInMultiplayer)
                return true;

            // During sync execution, let the original run — SyncContext.MapSelected
            // has already restored the correct selection on all clients.
            if (MP.IsExecutingSyncCommand)
                return true;

            SyncedSetVacCheckpoints(resistance, allowDrafted);
            return false;
        }

        private static void SyncedSetVacCheckpoints(float resistance, bool allowDrafted)
        {
            Dialog_ConfigureVacuumRequirement.SetSelectedVacCheckpointsTo(resistance, allowDrafted);
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
        private static void PreRenameDoWindowContents(Window_RenameAsteroid __instance)
        {
            if (!MP.IsInMultiplayer)
                return;

            cachedAsteroidName = (__instance.worldObject as SpaceMapParent)?.Name;
        }

        private static void PostRenameDoWindowContents(Window_RenameAsteroid __instance)
        {
            if (!MP.IsInMultiplayer)
                return;

            var worldObj = __instance.worldObject as SpaceMapParent;
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
                Dialog_BeginRitual_ShowRitualBeginWindow_Patch.state = null;
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
            if (gravEngine == null || Dialog_BeginRitual_ShowRitualBeginWindow_Patch.state == null)
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
            var state = Dialog_BeginRitual_ShowRitualBeginWindow_Patch.state;
            if (state == null)
                return;

            // Guard against duplicate tile selections (both players picked tiles).
            // If the GravshipTravelSession was already closed by a prior call, skip.
            if (hasGravshipSession != null && !hasGravshipSession(gravEngine.Map.Tile))
                return;

            // Store tile in VGE's state (used later by VGE's ritual outcome patches)
            state.targetTile = tile;
            SettlementProximityGoodwillUtility_CheckConfirmSettle_Patch.targetTile = tile;

            // Save ShowRitualBeginWindow args from state before tile picker cleanup
            var ritualInstance = state.instance;
            var targetInfo = state.targetInfo;
            var forObligation = state.forObligation;
            var selectedPawn = state.selectedPawn;
            var forcedForRole = state.forcedForRole;

            // Close tile picker using StopTargetingInt to bypass VGE's
            // TilePicker_StopTargeting_Patch (which would clear state too early)
            Find.World.renderer.wantedMode = WorldRenderMode.None;
            Find.TilePicker.StopTargetingInt();

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
                ritualInstance?.ShowRitualBeginWindow(targetInfo, forObligation, selectedPawn, forcedForRole);
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
            // The target tile is stored separately in CheckConfirmSettle_Patch.targetTile
            // and LordJob_Ritual_ExposeData_Patch.targetTile, so it's not lost.
            Dialog_BeginRitual_ShowRitualBeginWindow_Patch.state = null;
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
        /// Synced VGE launch confirmation. Mirrors VGE's replacement launch delegate
        /// (LaunchSequenceSwap.GravshipUtility_PreLaunchConfirmation_Patch). The tile
        /// was stored in CheckConfirmSettle_Patch.targetTile during the earlier tile
        /// selection step. We cannot invoke VGE's prefix directly because it looks up
        /// the ritual lordJob, which may have completed by sync execution time.
        /// </summary>
        private static void SyncedVgeLaunchConfirm(Building_GravEngine engine)
        {
            var tile = SettlementProximityGoodwillUtility_CheckConfirmSettle_Patch.targetTile;
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

            WorldComponent_GravshipController.DestroyTreesAroundSubstructure(engine.Map, engine.ValidSubstructure);
            Find.World.renderer.wantedMode = WorldRenderMode.None;
            engine.ConsumeFuel(tile);
            Find.GravshipController.InitiateTakeoff(engine, tile);
            SoundDefOf.Gravship_Launch.PlayOneShotOnCamera();
            Dialog_BeginRitual_ShowRitualBeginWindow_Patch.state = null;
        }

        /// <summary>
        /// Postfix on VGE's TakeoffEnded prefix. VGE creates Dialog_MessageBox with
        /// local-function button actions that are per-client and unsynced. We let VGE
        /// handle all condition logic and dialog creation natively, then swap the
        /// button actions with synced versions.
        /// </summary>
        private static void PostTakeoffEndedPatch(WorldComponent_GravshipController __0)
        {
            if (!MP.IsInMultiplayer)
                return;

            // Check if VGE added a Dialog_MessageBox
            if (Find.WindowStack.Count == 0)
                return;
            if (Find.WindowStack.Windows[Find.WindowStack.Count - 1] is not Dialog_MessageBox dialog)
                return;

            var mapParent = __0.map?.Parent;
            if (mapParent == null)
                return;

            // buttonB is SettleTile in both dialog variants
            if (dialog.buttonBAction != null)
                dialog.buttonBAction = () => SyncedSettleTile(mapParent);

            // buttonA is AbandonTile in the keep/abandon dialog, null in the settle-only dialog
            if (dialog.buttonAAction != null)
                dialog.buttonAAction = () => SyncedAbandonTile(mapParent);
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
        /// The dialog is opened from tick context (UpdateSubstructureIfNeeded),
        /// so it exists on all clients — we call the original Named during sync.
        /// </summary>
        private static bool PreGravshipNamed(Dialog_NamePlayerGravship __instance, string s)
        {
            if (!MP.IsInMultiplayer)
                return true;

            // During sync execution, let the original Named run on the dialog
            if (MP.IsExecutingSyncCommand)
                return true;

            if (__instance.engine is Building_GravEngine engine)
                SyncedGravshipNamed(engine, s);

            return false;
        }

        private static void SyncedGravshipNamed(Building_GravEngine engine, string name)
        {
            // Dialog is open on all clients (opened from tick context).
            // Find it and call Named — the prefix lets it through because
            // IsExecutingSyncCommand is true.
            for (var i = Find.WindowStack.Windows.Count - 1; i >= 0; i--)
            {
                if (Find.WindowStack.Windows[i] is Dialog_NamePlayerGravship dialog)
                {
                    dialog.Named(name);
                    return;
                }
            }
        }

        private static void PreOxygenGizmoOnGUI(Gizmo_Slider __instance)
        {
            if (!MP.IsInMultiplayer || __instance.GetType() != typeof(Gizmo_OxygenProvider))
                return;

            MP.WatchBegin();
            oxygenTargetValuePctField.Watch(__instance);
        }

        private static void PostOxygenGizmoOnGUI(Gizmo_Slider __instance)
        {
            if (!MP.IsInMultiplayer || __instance.GetType() != typeof(Gizmo_OxygenProvider))
                return;

            MP.WatchEnd();
        }

        private static void SyncOxygenGizmo(SyncWorker sync, ref Gizmo_Slider gizmo)
        {
            if (sync.isWriting)
            {
                sync.Write<ThingComp>(((Gizmo_OxygenProvider)gizmo).oxygenProvider);
            }
            else
            {
                var comp = (CompApparelOxygenProvider)sync.Read<ThingComp>();
                gizmo = comp.oxygenConfigurationGizmo;
            }
        }

        /// <summary>
        /// Intercept the paste color gizmo action. ColorClipboard is per-client
        /// state, so we capture the color value and sync per-barrier.
        /// </summary>
        private static bool PrePasteBarrierColor()
        {
            if (!MP.IsInMultiplayer)
                return true;

            var clipboard = Building_VacBarrier_Recolorable.ColorClipboard;
            if (clipboard == null)
            {
                Messages.Message("ClipboardInvalidColor".Translate(), MessageTypeDefOf.RejectInput, false);
                return false;
            }

            foreach (var obj in Find.Selector.SelectedObjects)
            {
                if (obj is Building_VacBarrier_Recolorable barrier)
                    SyncedSetBarrierColor(barrier, clipboard.Value.r, clipboard.Value.g, clipboard.Value.b);
            }

            return false;
        }

        /// <summary>
        /// Intercept Dialog_VacBarrierColorPicker.SaveColor to sync
        /// the color change per-barrier instead of applying locally.
        /// </summary>
        private static bool PreVacBarrierSaveColor(Dialog_VacBarrierColorPicker __instance, Color color)
        {
            if (!MP.IsInMultiplayer)
                return true;

            var barriers = __instance.extraVacBarriers;
            if (barriers != null)
            {
                foreach (var barrier in barriers)
                    SyncedSetBarrierColor(barrier, color.r, color.g, color.b);
            }

            return false;
        }

        private static void SyncedSetBarrierColor(Thing barrier, float r, float g, float b)
        {
            if (barrier is Building_VacBarrier_Recolorable recolorable)
            {
                recolorable.barrierColor = new Color(r, g, b);
                recolorable.Notify_ColorChanged();
            }
        }

        /// <summary>
        /// VGE creates VacBarrierRoofArea only for the map owner's faction in their
        /// mapcomponent finalizer. Patch so that every time a new faction data on
        /// a map is created, we create the same area for them if they don't have one.
        /// </summary>
        private static void PostMapSetupInitNewFactionData(Map map, Faction f)
        {
            var mpComp = mpCompMethod?.Invoke(null, new object[] { map });
            if (mpComp == null)
                return;
            var factionDataDict = factionDataField?.GetValue(mpComp) as System.Collections.IDictionary;
            if (factionDataDict == null)
                return;
            var factionData = factionDataDict[f.loadID];
            if (factionData == null)
                return;
            var manager = (AreaManager)areaManagerField?.GetValue(factionData);
            if (manager == null)
                return;
            if (manager.Get<Area_BuildVacBarrierRoof>() == null)
                manager.areas.Add(new Area_BuildVacBarrierRoof(manager));
        }
        #endregion
    }
}
