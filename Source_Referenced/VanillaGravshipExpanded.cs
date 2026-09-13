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

        // Gizmo_OxygenProvider refill threshold
        private static ISyncField oxygenRechargeThresholdField;

        // VGE launch flow — MP internals (not publicized, reached via reflection)
        private static Action<PlanetTile> closeGravshipSession;
        private static Func<PlanetTile, bool> hasGravshipSession;

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
                MpCompat.harmony.Patch(
                    AccessTools.DeclaredMethod(typeof(Command_Ritual), nameof(Command_Ritual.ProcessInput)),
                    prefix: new HarmonyMethod(typeof(VanillaGravshipExpanded), nameof(PreCommandRitualProcessInput_SyncIfGravshipLaunch)));

                MP.RegisterSyncMethod(typeof(VanillaGravshipExpanded), nameof(SyncedShowRitualBeginWindow));

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

                // VGE PR #6 exposed ExecuteGravshipLaunch as a public static method that VGE's
                // PreLaunchConfirmation prefix invokes via lambda. Sync it directly.
                MP.RegisterSyncMethod(typeof(GravshipUtility_PreLaunchConfirmation_Patch),
                    nameof(GravshipUtility_PreLaunchConfirmation_Patch.ExecuteGravshipLaunch));

                MpCompat.harmony.Patch(
                    AccessTools.DeclaredMethod(typeof(GravshipUtility_PreLaunchConfirmation_Patch),
                        nameof(GravshipUtility_PreLaunchConfirmation_Patch.ExecuteGravshipLaunch)),
                    prefix: new HarmonyMethod(typeof(VanillaGravshipExpanded), nameof(PreExecuteGravshipLaunchCloseDialog)));
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
                MP.RegisterSyncMethod(typeof(CompApparelOxygenProvider), nameof(CompApparelOxygenProvider.AutomaticRechargeEnabled));

                // The released mod recreates this gizmo; sync its persistent comp instead.
                oxygenRechargeThresholdField = MP.RegisterSyncField(typeof(CompApparelOxygenProvider), nameof(CompApparelOxygenProvider.rechargeAtCharges)).SetBufferChanges();

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
                Dialog_BeginRitual_ShowRitualBeginWindow_Patch.ClearLaunchState();
        }

        private static bool PreCommandRitualProcessInput_SyncIfGravshipLaunch(Command_Ritual __instance)
        {
            if (!MP.IsInMultiplayer)
                return true;
            if (MP.IsExecutingSyncCommand)
                return true;
            if (!__instance.ritual.def.IsGravshipLaunch())
                return true;

            SyncedShowRitualBeginWindow(__instance.ritual, __instance.targetInfo);
            return false;
        }

        private static void SyncedShowRitualBeginWindow(Precept_Ritual ritual, TargetInfo targetInfo)
        {
            ritual.ShowRitualBeginWindow(targetInfo);
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

        /// <summary>Synced tile selection — MP cleanup (picker, session) then delegate to VGE's named entry (PR #6).</summary>
        private static void SyncedGravshipTileSelected(Building_GravEngine gravEngine, PlanetTile tile)
        {
            var state = Dialog_BeginRitual_ShowRitualBeginWindow_Patch.state;
            if (state == null)
                return;

            // Dup-selection guard: session already closed by a prior synced call → skip.
            if (hasGravshipSession != null && !hasGravshipSession(gravEngine.Map.Tile))
                return;

            // MP-specific cleanup (not in VGE's OnGravshipTileSelected):
            //   StopTargetingInt — bypass VGE's TilePicker prefix which would clear state too early
            //   closeGravshipSession — close MP's GravshipTravelSession (it pauses the map)
            Find.World.renderer.wantedMode = WorldRenderMode.None;
            Find.TilePicker.StopTargetingInt();
            closeGravshipSession?.Invoke(gravEngine.Map.Tile);

            // Delegate camera ops + ShowRitualBeginWindow + targetTile writes to VGE.
            // inSyncedTileSelectedFlow guards PreShowRitualClearStaleState during the call.
            inSyncedTileSelectedFlow = true;
            try
            {
                SettlementProximityGoodwillUtility_CheckConfirmSettle_Patch.OnGravshipTileSelected(gravEngine, tile, state);
            }
            finally
            {
                inSyncedTileSelectedFlow = false;
            }

            Dialog_BeginRitual_ShowRitualBeginWindow_Patch.ClearLaunchState();
        }

        /// <summary>Close the prelaunch confirm Dialog_MessageBox on non-clicking clients before sync replay.</summary>
        private static void PreExecuteGravshipLaunchCloseDialog()
        {
            if (!MP.IsInMultiplayer || !MP.IsExecutingSyncCommand)
                return;

            var dialogPrefix = "ConfirmGravEngineLaunch".Translate().RawText;
            for (var i = Find.WindowStack.Windows.Count - 1; i >= 0; i--)
            {
                if (Find.WindowStack.Windows[i] is Dialog_MessageBox msgBox &&
                    msgBox.text.RawText.StartsWith(dialogPrefix))
                {
                    msgBox.Close();
                    break;
                }
            }
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
            oxygenRechargeThresholdField.Watch(((Gizmo_OxygenProvider)__instance).oxygenProvider);
            // Refresh the vanilla slider cache after Watch restores any pending local value.
            __instance.targetValuePct = ((Gizmo_OxygenProvider)__instance).Target;
        }

        private static void PostOxygenGizmoOnGUI(Gizmo_Slider __instance)
        {
            if (!MP.IsInMultiplayer || __instance.GetType() != typeof(Gizmo_OxygenProvider))
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
