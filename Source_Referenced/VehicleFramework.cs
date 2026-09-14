using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using JetBrains.Annotations;
using Multiplayer.API;
using RimWorld;
using RimWorld.Planet;
using SmashTools;
using UnityEngine;
using Vehicles;
using Vehicles.Rendering;
using Vehicles.World;
using Verse;
using Verse.AI.Group;
using Verse.Sound;

namespace Multiplayer.Compat
{
    /// <summary>Vehicle Framework by Smash Phil</summary>
    /// <see href="https://github.com/SmashPhil/Vehicle-Framework"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3014915404"/>
    [MpCompatFor("SmashPhil.VehicleFramework")]
    public class VehicleFramework
    {
        #region Fields

        private static Type caravanFormingSessionType;
        private static Type caravanFormingProxyType;
        private static MethodInfo caravanFormingChooseRouteMethod;
        private static MethodInfo caravanFormingTrySendMethod;
        private static MethodInfo vehicleTryFormAndSendMethod;
        private static PlanetTile synchronizedVehicleStartingTile = PlanetTile.Invalid;

        private static ISyncField showAllCargoItemsField;
        private static ISyncField targetFuelPercentField;

        private static AccessTools.FieldRef<object, VehiclePawn> vehiclePawnInnerClassParentField;
        
        private static Designator_AreaRoad.RoadType localRoadType = Designator_AreaRoad.RoadType.Prioritize;

        #endregion

        #region Constructor

        public VehicleFramework(ModContentPack mod)
        {
            LongEventHandler.ExecuteWhenFinished(LatePatch);
        }

        #endregion

        #region Main patch

        private static void LatePatch()
        {
            // MP reloads the world on rejoin without disposing Game.
            GameEvent.OnWorldUnloading += ClearVehicleTargetersForReload;
            #region MP Compat

            MethodInfo method;

            // Only declared overrides; inherited methods are already registered.
            static ISyncMethod TrySyncDeclaredMethod(Type targetType, string targetMethodName)
            {
                var declaredMethod = AccessTools.DeclaredMethod(targetType, targetMethodName);
                if (declaredMethod != null)
                    return MP.RegisterSyncMethod(declaredMethod);
                return null;
            }

            MpCompatPatchLoader.LoadPatch<VehicleFramework>();

            PatchingUtilities.PatchLongEventMarkers();

            #endregion

            #region VehicleFramework

            #region Multithreading

            {
                MpCompat.harmony.Patch(
                    AccessTools.DeclaredPropertyGetter(typeof(VehiclePathingSystem), nameof(VehiclePathingSystem.ThreadAvailable)),
                    postfix: new HarmonyMethod(typeof(VehicleFramework), nameof(DisableVehiclePathingThreadsInMultiplayer)));
            }

            #endregion

            #region Gizmos

            {

                MpCompat.RegisterLambdaMethod(typeof(Building_Artillery), nameof(Building_Artillery.GetGizmos), 0, 1);

                MP.RegisterSyncMethod(typeof(VehiclePawn), nameof(VehiclePawn.DisembarkAll));
                // Cancel cargo (5), fish (8), haul pawn (10), cancel caravan (15).
                MpCompat.RegisterLambdaDelegate(typeof(VehiclePawn), nameof(VehiclePawn.GetGizmos), 5, 8, 10, 15);

                // MP cannot resolve pawns held by VehicleRoleHandler.
                MP.RegisterSyncMethod(typeof(VehiclePawn), nameof(VehiclePawn.DisembarkPawn))
                    .TransformArgument(0, Serializer.New(
                        (Pawn pawn, object target, object[] _) =>
                            (vehicle: (VehiclePawn)target, pawnId: pawn.thingIDNumber),
                        tuple => tuple.vehicle?.AllPawnsAboard
                            .FirstOrDefault(pawn => pawn.thingIDNumber == tuple.pawnId)))
                    .CancelIfAnyArgNull();

                MP.RegisterSyncMethod(
                        typeof(LordJob_FormAndSendVehicles),
                        nameof(LordJob_FormAndSendVehicles.ForceCaravanLeave))
                    .TransformTarget(Serializer.New(
                        (LordJob_FormAndSendVehicles job) => job.lord,
                        (Lord lord) => lord?.LordJob as LordJob_FormAndSendVehicles));
                MpCompat.harmony.Patch(
                    AccessTools.DeclaredMethod(
                        typeof(CaravanFormingUtility),
                        nameof(CaravanFormingUtility.ForceCaravanDepart)),
                    prefix: new HarmonyMethod(
                        typeof(VehicleFramework),
                        nameof(RedirectVehicleCaravanForceDeparture)));

                MpCompat.RegisterLambdaDelegate(typeof(VehiclePawn), nameof(VehiclePawn.GetGizmos), 17, 19, 22, 24, 25, 26, 28)
                    .SetDebugOnly();

                MpCompat.RegisterLambdaMethod(typeof(VehicleIgnitionController), nameof(VehicleIgnitionController.GetGizmos), 1);

                // The slider assigns Target every draw.
                targetFuelPercentField = MP.RegisterSyncField(typeof(CompFueledTravel), "targetFuelPercent").SetBufferChanges();
                MpCompat.harmony.Patch(
                    AccessTools.DeclaredMethod(typeof(Gizmo_RefuelableFuelTravel), nameof(Gizmo_RefuelableFuelTravel.GizmoOnGUI)),
                    prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(PreFuelGizmo)),
                    finalizer: new HarmonyMethod(typeof(VehicleFramework), nameof(FinalizeFuelGizmo)));
                MP.RegisterSyncMethod(typeof(CompFueledTravel), nameof(CompFueledTravel.Refuel), [typeof(List<Thing>)]);
                MP.RegisterSyncMethod(typeof(CompFueledTravel), nameof(CompFueledTravel.Refuel), [typeof(float)]);
                MP.RegisterSyncMethod(typeof(CompFueledTravel), nameof(CompFueledTravel.ConsumeFuelFromInventory));
                MP.RegisterSyncMethod(typeof(Gizmo_RefuelableFuelTravel), nameof(Gizmo_RefuelableFuelTravel.ToggleAutoRefuel));
                MP.RegisterSyncMethod(typeof(Gizmo_RefuelableFuelTravel), nameof(Gizmo_RefuelableFuelTravel.ToggleCharging));
                MP.RegisterSyncMethod(typeof(CompFueledTravel), nameof(CompFueledTravel.ConsumeFuel), [typeof(float)])
                    .SetDebugOnly();
                // Set fuel to 99.99%.
                MpCompat.RegisterLambdaMethod(typeof(CompFueledTravel), nameof(CompFueledTravel.DevModeGizmos), 2).SetDebugOnly();

                MP.RegisterSyncMethod(typeof(CompVehicleTurrets), nameof(CompVehicleTurrets.SetQuotaLevel));
                MP.RegisterSyncMethod(typeof(CompVehicleTurrets), "DevModeReloadTurret").SetDebugOnly();

                // Pause (1), repairs (3).
                MpCompat.RegisterLambdaMethod(typeof(VehicleCaravan), nameof(VehicleCaravan.GetGizmos), 1, 3);
                // Down (5), kill (7), teleport (9), repair all (10).
                MpCompat.RegisterLambdaMethod(typeof(VehicleCaravan), nameof(VehicleCaravan.GetGizmos), 5, 7, 9, 10)
                    .SetDebugOnly();
                MP.RegisterSyncMethod(typeof(Patch_Debug), nameof(Patch_Debug.DebugLandAerialVehicle))
                    .SetDebugOnly();
                MP.RegisterSyncMethod(typeof(AerialVehicleInFlight), nameof(AerialVehicleInFlight.InitiateCrashEvent))
                    .SetDebugOnly();

                caravanFormingSessionType = AccessTools.TypeByName("Multiplayer.Client.CaravanFormingSession");
                caravanFormingProxyType = AccessTools.TypeByName("Multiplayer.Client.CaravanFormingProxy");
                caravanFormingChooseRouteMethod = caravanFormingSessionType == null
                    ? null
                    : AccessTools.DeclaredMethod(caravanFormingSessionType, "ChooseRoute");
                caravanFormingTrySendMethod = caravanFormingSessionType == null
                    ? null
                    : AccessTools.DeclaredMethod(caravanFormingSessionType, "TryFormAndSendCaravan");

                var vehicleFormCaravanPatchType = typeof(Patch_FormCaravanDialog);
                var createTabListPostOpen = AccessTools.DeclaredMethod(
                    vehicleFormCaravanPatchType,
                    "CreateTabListPostOpen");
                if (createTabListPostOpen != null && caravanFormingProxyType != null)
                {
                    MpCompat.harmony.Patch(
                        createTabListPostOpen,
                        prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(PrepareVehicleTabsForCaravanFormingProxy)));
                }

                var vehicleChoseRouteMethod = MpMethodUtil.GetLocalFunc(
                    vehicleFormCaravanPatchType,
                    "WorldRoutePannerReroute",
                    localFunc: "ChoseVehicleRoute");
                if (vehicleChoseRouteMethod != null &&
                    caravanFormingProxyType != null &&
                    caravanFormingChooseRouteMethod != null)
                {
                    MP.RegisterSyncMethod(typeof(VehicleFramework), nameof(SyncedChooseVehicleCaravanRoute));
                    MpCompat.harmony.Patch(
                        vehicleChoseRouteMethod,
                        prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(SyncVehicleCaravanRoute)));
                    MpCompat.harmony.Patch(
                        AccessTools.DeclaredMethod(typeof(Dialog_FormCaravan), nameof(Dialog_FormCaravan.Notify_ChoseRoute)),
                        transpiler: new HarmonyMethod(typeof(VehicleFramework), nameof(UseSynchronizedVehicleStartingTile)));

                    // Reset the process-local shuffled cache before MP use.
                    MpCompat.harmony.Patch(
                        AccessTools.DeclaredMethod(typeof(CellFinderExtended), "CacheAndShuffleMapEdgeCells"),
                        prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(ResetVehicleEdgeCellCache)));
                }

                vehicleTryFormAndSendMethod = MpMethodUtil.GetLocalFunc(
                    typeof(CaravanFormation),
                    nameof(CaravanFormation.TrySendVehicleCaravan),
                    localFunc: "TryFormAndSendCaravan");
                if (vehicleTryFormAndSendMethod != null &&
                    caravanFormingTrySendMethod != null)
                {
                    MpCompat.harmony.Patch(
                        vehicleTryFormAndSendMethod,
                        prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(RedirectVehicleCaravanSendToSession)));
                    MpCompat.harmony.Patch(
                        AccessTools.DeclaredMethod(typeof(Dialog_FormCaravan), nameof(Dialog_FormCaravan.TryFormAndSendCaravan)),
                        prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(FormVehicleCaravanFromSessionDummy)));
                }

            }

            #endregion

            #region Turrets

            {

                MP.RegisterSyncMethod(typeof(Command_CooldownAction), nameof(Command_CooldownAction.FireTurret));
                MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(Command_CooldownAction), nameof(Command_CooldownAction.GizmoOnGUI)),
                    transpiler: new HarmonyMethod(typeof(VehicleFramework), nameof(ReplaceSetTargetCall)));

                MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(Command_TargeterCooldownAction), nameof(Command_TargeterCooldownAction.FireTurret)),
                    transpiler: new HarmonyMethod(typeof(VehicleFramework), nameof(ReplaceSetTargetCall)));
                MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(TurretTargeter), nameof(TurretTargeter.BeginTargeting)),
                    transpiler: new HarmonyMethod(typeof(VehicleFramework), nameof(ReplaceSetTargetCall)));
                MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(TurretTargeter), nameof(TurretTargeter.StopTargeting), [typeof(bool)]),
                    transpiler: new HarmonyMethod(typeof(VehicleFramework), nameof(ReplaceSetTargetCall)));
                method = MpMethodUtil.GetLambda(typeof(Command_TargeterCooldownAction), nameof(Command_TargeterCooldownAction.FireTurret), lambdaOrdinal: 0);
                var targetableTurretField = AccessTools.FieldRefAccess<VehicleTurret>(method.DeclaringType, "turret");
                MP.RegisterSyncDelegateLambda(typeof(Command_TargeterCooldownAction), nameof(Command_TargeterCooldownAction.FireTurret), 0)
                    .SetPreInvoke((target, _) => ResetTurretTarget(targetableTurretField(target)));

                PatchingUtilities.PatchCancelInInterface(AccessTools.DeclaredMethod(typeof(VehicleTurret), nameof(VehicleTurret.StartTicking)));
                PatchingUtilities.PatchCancelInInterface(AccessTools.DeclaredMethod(typeof(VehicleTurret), nameof(VehicleTurret.AlignToAngleRestricted)));
                MP.RegisterSyncMethod(typeof(VehicleTurret), nameof(VehicleTurret.CycleFireMode));
                MP.RegisterSyncMethod(typeof(VehicleFramework), nameof(SyncSetTarget));
                foreach (var subclass in typeof(VehicleTurret).AllSubclasses().Concat(typeof(VehicleTurret)))
                {
                    var reloadMethod = AccessTools.DeclaredMethod(subclass, nameof(VehicleTurret.Reload), []);
                    if (reloadMethod != null)
                        MP.RegisterSyncMethod(reloadMethod);
                    var reloadWithArgs = AccessTools.DeclaredMethod(subclass, nameof(VehicleTurret.Reload), [typeof(ThingDef), typeof(bool)]);
                    if (reloadWithArgs != null)
                        MP.RegisterSyncMethod(reloadWithArgs);
                    TrySyncDeclaredMethod(subclass, nameof(VehicleTurret.TryClearChamber));
                    TrySyncDeclaredMethod(subclass, nameof(VehicleTurret.SwitchAutoTarget));
                }

                PatchingUtilities.PatchCancelInInterface(AccessTools.DeclaredMethod(typeof(VehicleTurret), nameof(VehicleTurret.UpdateRotationLock)));
            }

            #endregion

            #region Float Menus

            {
                MpCompat.RegisterLambdaDelegate(typeof(VehiclePawn), nameof(VehiclePawn.GetFloatMenuOptions), 0);

                MP.RegisterSyncMethod(typeof(Command_TransferToVehicle_Order), nameof(Command_TransferToVehicle_Order.Action))
                    .SetContext(SyncContext.MapSelected);
                MP.RegisterSyncMethod(typeof(Command_TransferToVehicle_Cancel), nameof(Command_TransferToVehicle_Cancel.Action))
                    .SetContext(SyncContext.MapSelected);
            }

            #endregion

            #region RNG

            {
                PatchingUtilities.PatchPushPopRand(new[]
                {
                    "Vehicles.Verb_ShootRealistic:InitTurretMotes",
                    "Vehicles.VehicleTurret:InitTurretMotes",
                    "Vehicles.CompFueledTravel:DrawMotes",
                });
                foreach (var throwFleck in AccessTools.GetDeclaredMethods(typeof(LaunchProtocol))
                    .Where(m => m.Name == nameof(LaunchProtocol.ThrowFleck) && !m.IsStatic))
                    PatchingUtilities.PatchPushPopRand(throwFleck);
            }

            #endregion

            #region DrawAt determinism

            {
                // The attribute cannot match the in Vector3 parameter.
                var drawAtMethod = AccessTools.DeclaredMethod(typeof(VehiclePawn), nameof(VehiclePawn.DrawAt),
                    [typeof(Vector3).MakeByRefType(), typeof(Rot8), typeof(float)]);
                if (drawAtMethod != null)
                {
                    MpCompat.harmony.Patch(drawAtMethod,
                        prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(PreRenderPawnInternal)),
                        finalizer: new HarmonyMethod(typeof(VehicleFramework), nameof(PostRenderPawnInternal)));
                }
            }

            #endregion

            #region Dialogs

            {
                #region Other

                MpCompat.RegisterLambdaMethod("Vehicles.VehiclePawn", "ChangeColor", 0);

                MP.RegisterSyncMethod(typeof(VehicleFramework), nameof(SyncedSetSeatAssignments))
                    .TransformArgument(1, Serializer.New(
                        (List<Pawn> pawns) => pawns.Select(GetVehiclePawnReference).ToList(),
                        references => references.Select(ResolveVehiclePawnReference).ToList()));
                MP.RegisterSyncMethod(typeof(VehicleFramework), nameof(SyncedRemoveSeatAssignments));
                MP.RegisterSyncMethod(typeof(VehicleFramework), nameof(SyncedClearSeatAssignments));
                MpCompat.harmony.Patch(
                    AccessTools.DeclaredMethod(typeof(VehicleAssignment), nameof(VehicleAssignment.SetAssignments)),
                    prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(PreSetSeatAssignments)));
                MpCompat.harmony.Patch(
                    AccessTools.DeclaredMethod(typeof(VehicleAssignment), nameof(VehicleAssignment.RemoveAssignments)),
                    prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(PreRemoveSeatAssignments)));
                MpCompat.harmony.Patch(
                    AccessTools.DeclaredMethod(typeof(VehicleAssignment), nameof(VehicleAssignment.Clear)),
                    prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(PreClearSeatAssignments)));

                MP.RegisterSyncMethod(typeof(VehicleFramework), nameof(SyncedStashVehicles));
                MpCompat.harmony.Patch(
                    AccessTools.DeclaredMethod(typeof(Dialog_StashVehicle), "TransferPawns"),
                    prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(PreStashVehicles)));

                #endregion

                #region Load cargo

                MP.RegisterSyncMethod(typeof(LoadVehicleCargoSession), nameof(LoadVehicleCargoSession.CreateLoadVehicleCargoSession));
                MP.RegisterSyncMethod(typeof(LoadVehicleCargoSession), nameof(LoadVehicleCargoSession.Accept));
                MP.RegisterSyncMethod(typeof(LoadVehicleCargoSession), nameof(LoadVehicleCargoSession.Reset));
                MP.RegisterSyncMethod(typeof(LoadVehicleCargoSession), nameof(LoadVehicleCargoSession.PackInstantly));
                MP.RegisterSyncMethod(typeof(LoadVehicleCargoSession), nameof(LoadVehicleCargoSession.SetToSendEverything));
                MP.RegisterSyncMethod(typeof(LoadVehicleCargoSession), nameof(LoadVehicleCargoSession.Remove));

                showAllCargoItemsField = MP.RegisterSyncField(typeof(VehiclesModSettings), nameof(VehiclesModSettings.showAllCargoItems))
                    .PostApply(PostShowAllCargoItemsChanged);
                MP.RegisterSyncWorker<VehiclesModSettings>(SyncVehicleSettings);

                MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(Dialog_LoadCargo), nameof(Dialog_LoadCargo.DoWindowContents)),
                    prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(PreDrawLoadCargo)),
                    finalizer: new HarmonyMethod(typeof(VehicleFramework), nameof(FinalizeDrawLoadCargo)));

                MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(Dialog_LoadCargo), "SetToSendEverything"),
                    prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(PreLoadCargoSetToSendEverything)));

                MpCompat.harmony.Patch(
                    AccessTools.DeclaredMethod(typeof(Widgets), nameof(Widgets.ButtonText),
                        [typeof(Rect), typeof(string), typeof(bool), typeof(bool), typeof(bool), typeof(TextAnchor?)]),
                    prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(PreLoadCargoButtonText)),
                    postfix: new HarmonyMethod(typeof(VehicleFramework), nameof(PostLoadCargoButtonText)));
                MpCompat.harmony.Patch(
                    AccessTools.DeclaredMethod(typeof(Widgets), nameof(Widgets.ButtonTextWorker)),
                    postfix: new HarmonyMethod(typeof(VehicleFramework), nameof(PostLoadCargoButtonTextWorker)));

                method = MpMethodUtil.GetLambda(typeof(VehiclePawn), nameof(VehiclePawn.GetGizmos), lambdaOrdinal: 6);
                vehiclePawnInnerClassParentField = AccessTools.FieldRefAccess<VehiclePawn>(method.DeclaringType, "<>4__this");
                MpCompat.harmony.Patch(method, prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(PreLoadCargoDialog)));

                // Recaching on reopen would replace the session transferables.
                MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(Dialog_LoadCargo), "CalculateAndRecacheTransferables"),
                    prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(PreLoadCargoCalculateAndRecache)));

                #endregion

                #region Shared

                var types = new[]
                {
                    typeof(Dialog_LoadCargo),
                };

                foreach (var type in types)
                {
                    MpCompat.harmony.Patch(
                        AccessTools.DeclaredMethod(type, nameof(Window.DoWindowContents), [typeof(Rect)]),
                        postfix: new HarmonyMethod(typeof(VehicleFramework), nameof(InsertSwitchToMap)));
                }

                #endregion
            }

            #endregion

            #region ITabs and WITabs

            {
                foreach (var type in typeof(ITab_Airdrop_Container).AllSubclasses().Concat(typeof(ITab_Airdrop_Container)))
                {
                    TrySyncDeclaredMethod(type, "InterfaceDrop")?.SetContext(SyncContext.MapSelected);
                    TrySyncDeclaredMethod(type, "InterfaceDropAll")?.SetContext(SyncContext.MapSelected);
                }

                method = AccessTools.DeclaredMethod(typeof(VehicleTabHelper_Passenger), nameof(VehicleTabHelper_Passenger.HandleDragEvent));
                MpCompat.harmony.Patch(method, prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(PreHandleDragEvent)));
                MP.RegisterSyncMethod(typeof(VehicleFramework), nameof(SyncedHandleDragEvent))
                    .TransformArgument(0, Serializer.New<Pawn, (Pawn pawn, VehiclePawn vehicle, int id)>(GetVehiclePawnReference, ResolveVehiclePawnReference))
                    .TransformArgument(1, Serializer.New<Pawn, (Pawn pawn, VehiclePawn vehicle, int id)>(GetVehiclePawnReference, ResolveVehiclePawnReference));

                var typesThing = new[] { typeof(Thing), typeof(AerialVehicleInFlight) };
                var typesTransferable = new[] { typeof(TransferableImmutable), typeof(AerialVehicleInFlight) };

                MP.RegisterSyncDelegateLambda(
                    typeof(AerialVehicleAbandonOrBanishHelper),
                    nameof(AerialVehicleAbandonOrBanishHelper.TryAbandonOrBanishViaInterface),
                    1,
                    typesThing);

                MP.RegisterSyncDelegateLambda(
                    typeof(AerialVehicleAbandonOrBanishHelper),
                    nameof(AerialVehicleAbandonOrBanishHelper.TryAbandonOrBanishViaInterface),
                    0,
                    typesTransferable);

                MP.RegisterSyncDelegateLambda(
                    typeof(AerialVehicleAbandonOrBanishHelper),
                    nameof(AerialVehicleAbandonOrBanishHelper.TryAbandonSpecificCountViaInterface),
                    0,
                    typesThing);

                MP.RegisterSyncDelegateLambda(
                    typeof(AerialVehicleAbandonOrBanishHelper),
                    nameof(AerialVehicleAbandonOrBanishHelper.TryAbandonSpecificCountViaInterface),
                    0,
                    typesTransferable);

                var upgradeNodeSerializer = Serializer.New(
                    (UpgradeNode upgrade, object target, object[] _) => (target: ((CompUpgradeTree)target).Props.def, key: upgrade.key),
                    tuple => tuple.target.GetNode(tuple.key)
                );
                MP.RegisterSyncMethod(typeof(CompUpgradeTree), nameof(CompUpgradeTree.StartUnlock))
                    .TransformArgument(0, upgradeNodeSerializer);
                MP.RegisterSyncMethod(typeof(CompUpgradeTree), nameof(CompUpgradeTree.RemoveUnlock))
                    .TransformArgument(0, upgradeNodeSerializer);
                MP.RegisterSyncMethod(typeof(CompUpgradeTree), nameof(CompUpgradeTree.ClearUpgrade));
                MP.RegisterSyncMethod(typeof(CompUpgradeTree), nameof(CompUpgradeTree.FinishUnlock))
                    .TransformArgument(0, upgradeNodeSerializer)
                    .SetDebugOnly();
                MP.RegisterSyncMethod(typeof(CompUpgradeTree), nameof(CompUpgradeTree.ResetUnlock))
                    .TransformArgument(0, upgradeNodeSerializer)
                    .SetDebugOnly();

                MP.RegisterSyncMethod(typeof(AutoLoadConfig), nameof(AutoLoadConfig.SetEnabled));
                MP.RegisterSyncMethod(typeof(AutoLoadConfig), nameof(AutoLoadConfig.Set));
            }

            #endregion

            #region Flying vehicles

            {
                MP.RegisterSyncWorker<LaunchProtocol>(SyncLaunchProtocol, isImplicit: true);
                MP.RegisterSyncWorker<FlightNode>(SyncFlightNode);
                MP.RegisterSyncWorker<SmashTools.Targeting.TargetData<GlobalTargetInfo>>(SyncGlobalTargetData);

                MP.RegisterSyncMethod(typeof(CompVehicleLauncher), nameof(CompVehicleLauncher.Launch))
                    .ExposeParameter(1)
                    .SetPreInvoke(SetLaunchArrivalVehicle);
                MP.RegisterSyncMethod(typeof(AerialVehicleInFlight), nameof(AerialVehicleInFlight.OrderFlyToTiles))
                    .ExposeParameter(1)
                    .SetPreInvoke(SetOrderArrivalVehicle);
                MP.RegisterSyncMethod(typeof(VehicleCaravan), nameof(VehicleCaravan.Launch))
                    .ExposeParameter(1)
                    .SetPreInvoke(SetCaravanLaunchArrivalVehicle)
                    .SetPostInvoke(CleanupDestroyedCaravanAfterLaunch);
                MP.RegisterSyncMethod(typeof(LaunchProtocol), nameof(LaunchProtocol.StartTargetingLocalMap));

                MpCompat.harmony.Patch(
                    AccessTools.DeclaredMethod(typeof(VehicleSkyfaller_Leaving), nameof(VehicleSkyfaller_Leaving.ExposeData)),
                    postfix: new HarmonyMethod(typeof(VehicleFramework), nameof(PostSkyfallerLeavingExposeData)));

                MpCompat.harmony.Patch(
                    AccessTools.DeclaredMethod(typeof(CompVehicleLauncher), nameof(CompVehicleLauncher.Launch)),
                    prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(PreLaunchSetArrivalVehicle)));
                MpCompat.harmony.Patch(
                    AccessTools.DeclaredMethod(typeof(AerialVehicleInFlight), nameof(AerialVehicleInFlight.OrderFlyToTiles)),
                    prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(PreOrderFlySetArrivalVehicle)));

                MpCompat.harmony.Patch(
                    AccessTools.DeclaredMethod(typeof(AerialVehicleInFlight), nameof(AerialVehicleInFlight.MoveForward)),
                    prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(PreMoveForwardFixArrivalVehicle)));

                // ResumePathPostLoad resets flight progress.
                MpCompat.harmony.Patch(
                    AccessTools.DeclaredMethod(typeof(AerialVehicleInFlight), "ResumePathPostLoad"),
                    prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(PreResumePathPostLoad)),
                    postfix: new HarmonyMethod(typeof(VehicleFramework), nameof(PostResumePathPostLoad)));

                MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(AerialVehicleArrivalModeWorker_TargetedDrop), nameof(AerialVehicleArrivalModeWorker_TargetedDrop.VehicleArrived)),
                    prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(PreTargetedDropVehicleArrival)));

                MpCompat.harmony.Patch(AccessTools.DeclaredPropertyGetter(typeof(MapPawns), nameof(MapPawns.AnyPawnBlockingMapRemoval)),
                    postfix: new HarmonyMethod(typeof(VehicleFramework), nameof(PreventMapRemovalForLandingSessions)) { after = ["SmashPhil.VehicleFramework"] });

                MP.RegisterSyncMethod(typeof(FlyingVehicleTargetedLandingSession), nameof(FlyingVehicleTargetedLandingSession.Remove));
                MP.RegisterSyncMethod(typeof(FlyingVehicleTargetedLandingSession), nameof(FlyingVehicleTargetedLandingSession.VehicleArrivalById));
            }

            #endregion

            #region SyncWorkers

            {
                MP.RegisterSyncWorker<Gizmo_RefuelableFuelTravel>(SyncFuelGizmo);
                MP.RegisterSyncWorker<ITab_Vehicle_Cargo>(SyncCargoTab);
                MP.RegisterSyncWorker<Command_Turret>(SyncCommandTurret, typeof(Command_Turret), true, true);
                MP.RegisterSyncWorker<VehicleComponent>(SyncVehicleComponent, isImplicit: true);
                MP.RegisterSyncWorker<VehicleTurret>(SyncVehicleTurret, isImplicit: true);
                MP.RegisterSyncWorker<AutoLoadConfig>(SyncAutoLoadConfig, isImplicit: true);
                MP.RegisterSyncWorker<VehicleIgnitionController>(SyncVehicleIgnitionController);
                MP.RegisterSyncWorker<VehicleRoleHandler>(SyncVehicleRoleHandler);
            }

            #endregion

            #endregion
        }

        #endregion

        #region Multithreading

        #region Disable multithreading

        private static void DisableVehiclePathingThreadsInMultiplayer(ref bool __result)
            => __result &= !MP.IsInMultiplayer;

        [MpCompatPrefix(typeof(VehiclePathingSystem), nameof(VehiclePathingSystem.MapComponentTick))]
        private static void StopExistingVehiclePathingThread(VehiclePathingSystem __instance)
        {
            if (MP.IsInMultiplayer && __instance.ThreadAlive)
                __instance.ReleaseThread();
        }

        [MpCompatTranspiler(typeof(VehiclePathFollower), nameof(VehiclePathFollower.RequestNewPath))]
        [MpCompatTranspiler(typeof(WorldVehiclePathGrid), "RecalculateAllPathCostsAsync")]
        [MpCompatTranspiler(typeof(WorldVehiclePathGrid), "RecalculateReachabilityGrid")]
        private static IEnumerable<CodeInstruction> RunVehiclePathfindingSynchronously(
            IEnumerable<CodeInstruction> instr,
            MethodBase baseMethod)
        {
            var target = AccessTools.DeclaredMethod(
                typeof(TaskManager),
                nameof(TaskManager.Run),
                [typeof(Action), typeof(CancellationToken)]);
            var replacement = MpMethodUtil.MethodOf(RunVehiclePathAction);

            return instr.ReplaceMethod(target, replacement, baseMethod, expectedReplacements: 1);
        }

        private static Task RunVehiclePathAction(Action action, CancellationToken token)
        {
            if (!MP.IsInMultiplayer)
                return TaskManager.Run(action, token);

            if (!token.IsCancellationRequested)
                action();

            return Task.CompletedTask;
        }

        #endregion

        #endregion

        #region Vehicle jobs

        [MpCompatPrefix(typeof(JobDriver_IdleVehicle), "MakeNewToils")]
        private static void RestoreMissingIdleVehicleTarget(JobDriver_IdleVehicle __instance)
        {
            if (MP.IsInMultiplayer && !__instance.job.targetA.IsValid)
                __instance.job.targetA = __instance.pawn;
        }

        #endregion

        #region Caravan seat assignment sync

        private static bool RedirectVehicleCaravanForceDeparture(Lord lord)
        {
            if (!MP.IsInMultiplayer || lord?.LordJob is not LordJob_FormAndSendVehicles vehicleJob)
                return true;

            vehicleJob.ForceCaravanLeave();
            return false;
        }

        private static bool PreSetSeatAssignments(VehicleAssignment __instance, VehiclePawn vehicle, List<AssignedSeat> assignments)
        {
            if (!ShouldSyncCaravanSeatAssignment(__instance))
                return true;

            SyncedSetSeatAssignments(
                vehicle,
                assignments.Select(assignment => assignment.pawn).ToList(),
                assignments.Select(assignment => assignment.handler).ToList());
            return false;
        }

        private static (Pawn pawn, VehiclePawn vehicle, int id) GetVehiclePawnReference(Pawn pawn)
            => pawn?.ParentHolder is VehicleRoleHandler handler
                ? (null, handler.vehicle, pawn.thingIDNumber)
                : (pawn, null, -1);

        private static Pawn ResolveVehiclePawnReference((Pawn pawn, VehiclePawn vehicle, int id) reference)
            => reference.vehicle == null
                ? reference.pawn
                : reference.vehicle.AllPawnsAboard.FirstOrDefault(pawn => pawn.thingIDNumber == reference.id);

        private static void SyncedSetSeatAssignments(
            VehiclePawn vehicle,
            List<Pawn> pawns,
            List<VehicleRoleHandler> handlers)
        {
            if (vehicle == null || pawns == null || handlers == null || pawns.Count != handlers.Count)
                return;

            var previouslyAssignedPawns = CaravanHelper.assignedSeats.GetAssignments(vehicle)
                .Select(assignment => assignment.pawn)
                .ToList();
            var assignments = new List<AssignedSeat>();
            for (var i = 0; i < pawns.Count; i++)
            {
                var pawn = pawns[i];
                var handler = handlers[i];
                if (pawn != null && handler?.vehicle == vehicle)
                    assignments.Add(new AssignedSeat(pawn, handler));
            }

            var session = GetCaravanFormingSession(vehicle.Map);
            foreach (var pawn in previouslyAssignedPawns)
                SetCaravanFormingTransferCount(session, pawn, 0);
            foreach (var pawn in assignments.Select(assignment => assignment.pawn))
                SetCaravanFormingTransferCount(session, pawn, 1);

            CaravanHelper.assignedSeats.SetAssignments(vehicle, assignments);

            var vehicleTransferable = session?.GetTransferableByThingId(vehicle.thingIDNumber);
            if (vehicleTransferable != null)
            {
                vehicleTransferable.AdjustTo(assignments.Count > 0 ? vehicleTransferable.GetMaximumToTransfer() : 0);
                session.Notify_CountChanged(vehicleTransferable);
            }
        }

        private static bool PreRemoveSeatAssignments(VehicleAssignment __instance, VehiclePawn vehicle)
        {
            if (!ShouldSyncCaravanSeatAssignment(__instance))
                return true;

            SyncedRemoveSeatAssignments(vehicle);
            return false;
        }

        private static void SyncedRemoveSeatAssignments(VehiclePawn vehicle)
        {
            if (vehicle == null)
                return;

            var previouslyAssignedPawns = CaravanHelper.assignedSeats.GetAssignments(vehicle)
                .Select(assignment => assignment.pawn)
                .ToList();

            var session = GetCaravanFormingSession(vehicle.Map);
            SetCaravanFormingTransferCount(session, vehicle, 0);
            foreach (var pawn in previouslyAssignedPawns.Where(pawn => pawn != null && !pawn.InVehicle()))
                SetCaravanFormingTransferCount(session, pawn, 0);
            foreach (var pawn in vehicle.AllPawnsAboard)
                SetCaravanFormingTransferCount(session, pawn, 0);

            CaravanHelper.assignedSeats.RemoveAssignments(vehicle);
        }

        private static bool PreClearSeatAssignments(VehicleAssignment __instance)
        {
            if (!ShouldSyncCaravanSeatAssignment(__instance))
                return true;

            SyncedClearSeatAssignments();
            return false;
        }

        private static void SyncedClearSeatAssignments()
            => CaravanHelper.assignedSeats.Clear();

        private static ISessionWithTransferables GetCaravanFormingSession(Map map)
            => map == null || caravanFormingSessionType == null
                ? null
                : MP.GetLocalSessionManager(map).AllSessions
                    .FirstOrDefault(caravanFormingSessionType.IsInstanceOfType) as ISessionWithTransferables;

        private static void SetCaravanFormingTransferCount(ISessionWithTransferables session, Thing thing, int count)
        {
            var transferable = thing == null ? null : session?.GetTransferableByThingId(thing.thingIDNumber);
            if (transferable == null)
                return;

            transferable.ForceTo(count);
            session.Notify_CountChanged(transferable);
        }

        private static bool ShouldSyncCaravanSeatAssignment(VehicleAssignment assignment)
            => MP.IsInMultiplayer
               && MP.InInterface
               && !MP.IsExecutingSyncCommand
               && ReferenceEquals(assignment, CaravanHelper.assignedSeats);

        #endregion

        #region Stash vehicle sync

        private static bool PreStashVehicles(Dialog_StashVehicle __instance, ref bool __result)
        {
            if (!MP.IsInMultiplayer || MP.IsExecutingSyncCommand)
                return true;

            var things = new List<Thing>();
            var groupSizes = new List<int>();
            var counts = new List<int>();

            foreach (var transferable in __instance.transferables)
            {
                if (transferable.CountToTransfer <= 0)
                    continue;

                groupSizes.Add(transferable.things.Count);
                counts.Add(transferable.CountToTransfer);
                things.AddRange(transferable.things);
            }

            SyncedStashVehicles(__instance.caravan, things, groupSizes, counts);
            __result = true;
            return false;
        }

        private static void SyncedStashVehicles(
            VehicleCaravan caravan,
            List<Thing> things,
            List<int> groupSizes,
            List<int> counts)
        {
            if (caravan == null || things == null || groupSizes == null || counts == null ||
                groupSizes.Count != counts.Count || groupSizes.Sum() != things.Count)
                return;

            var transferables = new List<TransferableOneWay>();
            var thingIndex = 0;

            for (var groupIndex = 0; groupIndex < groupSizes.Count; groupIndex++)
            {
                var transferable = new TransferableOneWay();
                for (var i = 0; i < groupSizes[groupIndex]; i++, thingIndex++)
                {
                    var thing = things[thingIndex];
                    if (thing != null)
                        transferable.things.Add(thing);
                }

                if (transferable.things.Count > 0)
                {
                    transferable.AdjustTo(counts[groupIndex]);
                    transferables.Add(transferable);
                }
            }

            StashedVehicle.Create(caravan, out _, transferables);
        }

        #endregion

        #region ITabs and WITabs

        private static bool PreHandleDragEvent()
        {
            if (!MP.IsInMultiplayer || MP.IsExecutingSyncCommand)
                return true;

            if (Event.current.type != EventType.MouseUp || Event.current.button != 0)
                return false;

            SyncedHandleDragEvent(VehicleTabHelper_Passenger.draggedPawn, VehicleTabHelper_Passenger.hoveringOverPawn, VehicleTabHelper_Passenger.transferToHolder);
            VehicleTabHelper_Passenger.draggedPawn = null;
            return false;
        }

        private static void SyncedHandleDragEvent(Pawn dragged, Pawn hovering, IThingHolder holder)
        {
            var currentDraggedPawn = VehicleTabHelper_Passenger.draggedPawn;
            var currentHoveringPawn = VehicleTabHelper_Passenger.hoveringOverPawn;
            var currentTransferToHolder = VehicleTabHelper_Passenger.transferToHolder;
            var currentEvent = Event.current;

            try
            {
                VehicleTabHelper_Passenger.draggedPawn = dragged;
                VehicleTabHelper_Passenger.hoveringOverPawn = hovering;
                VehicleTabHelper_Passenger.transferToHolder = holder;
                Event.current = new Event
                {
                    type = EventType.MouseUp,
                    button = 0,
                };
                VehicleTabHelper_Passenger.HandleDragEvent();
            }
            finally
            {
                VehicleTabHelper_Passenger.draggedPawn = currentDraggedPawn;
                VehicleTabHelper_Passenger.hoveringOverPawn = currentHoveringPawn;
                VehicleTabHelper_Passenger.transferToHolder = currentTransferToHolder;
                Event.current = currentEvent;
            }
        }

        #endregion

        private static void PrepareVehicleTabsForCaravanFormingProxy(
            Dialog_FormCaravan formCaravan,
            List<TabRecord> tabsList,
            ref bool thisWindowInstanceEverOpened)
        {
            if (MP.IsInMultiplayer &&
                caravanFormingProxyType.IsInstanceOfType(formCaravan) &&
                tabsList.Count == 0)
                thisWindowInstanceEverOpened = false;
        }

        [MpCompatPostfix(typeof(VehicleRoutePlanner), nameof(VehicleRoutePlanner.ShouldStop), methodType: MethodType.Getter)]
        private static void KeepVehicleRoutePlannerOpen(VehicleRoutePlanner __instance, ref bool __result)
        {
            if (MP.IsInMultiplayer && __result && __instance.IsActive && WorldRendererUtility.WorldSelected)
                __result = false;
        }

        private static bool SyncVehicleCaravanRoute(PlanetTile tile)
        {
            if (!MP.IsInMultiplayer || !MP.InInterface)
                return true;

            var formation = CaravanFormation.formation;
            if (formation == null ||
                !caravanFormingProxyType.IsInstanceOfType(formation.Dialog) ||
                !tile.Valid)
                return true;

            var vehicles = TransferableUtility.GetPawnsFromTransferables(formation.Dialog.transferables)
                .OfType<VehiclePawn>()
                .ToList();
            if (vehicles.Count == 0)
                return true;

            PlanetTile startingTile;
            Rand.PushState();
            try
            {
                startingTile = CaravanHelper.BestExitTileToGoTo(
                    vehicles.Select(vehicle => vehicle.VehicleDef).Distinct().ToList(),
                    tile,
                    formation.Map);
            }
            finally
            {
                Rand.PopState();
            }

            SyncedChooseVehicleCaravanRoute(formation.Map, tile, startingTile);
            return false;
        }

        private static void SyncedChooseVehicleCaravanRoute(
            Map map,
            PlanetTile destinationTile,
            PlanetTile startingTile)
        {
            var session = GetCaravanFormingSession(map);
            if (session == null)
                return;

            synchronizedVehicleStartingTile = startingTile;
            try
            {
                caravanFormingChooseRouteMethod.Invoke(session, new object[] { destinationTile });
            }
            finally
            {
                synchronizedVehicleStartingTile = PlanetTile.Invalid;
            }
        }

        private static IEnumerable<CodeInstruction> UseSynchronizedVehicleStartingTile(
            IEnumerable<CodeInstruction> instr,
            MethodBase baseMethod)
        {
            var target = AccessTools.DeclaredMethod(
                typeof(CaravanExitMapUtility),
                nameof(CaravanExitMapUtility.BestExitTileToGoTo),
                [typeof(PlanetTile), typeof(Map)]);
            var replacement = MpMethodUtil.MethodOf(GetSynchronizedVehicleStartingTile);

            return instr.ReplaceMethod(target, replacement, baseMethod, expectedReplacements: 1);
        }

        private static PlanetTile GetSynchronizedVehicleStartingTile(
            PlanetTile destinationTile,
            Map map)
            => MP.IsInMultiplayer &&
               MP.IsExecutingSyncCommand &&
               synchronizedVehicleStartingTile.Valid
                ? synchronizedVehicleStartingTile
                : CaravanExitMapUtility.BestExitTileToGoTo(destinationTile, map);

        private static void ResetVehicleEdgeCellCache(ref List<IntVec3> ___mapEdgeCells)
        {
            if (MP.IsInMultiplayer)
                ___mapEdgeCells = null;
        }

        private static bool RedirectVehicleCaravanSendToSession(ref bool __result)
        {
            if (!MP.IsInMultiplayer || !MP.InInterface || MP.IsExecutingSyncCommand)
                return true;

            var session = GetCaravanFormingSession(CaravanFormation.formation?.Map);
            if (session == null)
                return true;

            caravanFormingTrySendMethod.Invoke(session, null);
            __result = false;
            return false;
        }

        private static bool FormVehicleCaravanFromSessionDummy(Dialog_FormCaravan __instance, ref bool __result)
        {
            if (!MP.IsInMultiplayer || !MP.IsExecutingSyncCommand)
                return true;

            var vehicle = __instance.transferables?
                .FirstOrDefault(transferable => transferable.CountToTransfer > 0 && transferable.AnyThing is VehiclePawn)?
                .AnyThing as VehiclePawn;
            if (vehicle?.Map == null)
                return true;

            var previousFormation = CaravanFormation.formation;
            try
            {
                CaravanFormation.formation = new FormationInfo(__instance, vehicle.Map);
                // The skipped UI path normally recaches these lists.
                CaravanFormation.formation.RecacheTransferables();
                __result = (bool)vehicleTryFormAndSendMethod.Invoke(null, null);
                return false;
            }
            finally
            {
                CaravanFormation.formation = previousFormation;
            }
        }

        #region Turrets

        private static void SyncSetTarget(VehicleTurret turret, LocalTargetInfo target)
            => turret.SetTarget(target);

        private static IEnumerable<CodeInstruction> ReplaceSetTargetCall(IEnumerable<CodeInstruction> instr, MethodBase baseMethod)
        {
            var target = AccessTools.DeclaredMethod(typeof(VehicleTurret), nameof(VehicleTurret.SetTarget));
            var replacement = AccessTools.DeclaredMethod(typeof(VehicleFramework), nameof(SyncSetTarget));

            var replacedCount = 0;

            foreach (var ci in instr)
            {
                if ((ci.opcode == OpCodes.Call || ci.opcode == OpCodes.Callvirt) && ci.operand is MethodInfo method && method == target)
                {
                    ci.opcode = OpCodes.Call;
                    ci.operand = replacement;
                    replacedCount++;
                }

                yield return ci;
            }

            const int expected = 1;
            if (replacedCount != expected)
            {
                var name = (baseMethod.DeclaringType?.Namespace).NullOrEmpty() ? baseMethod.Name : $"{baseMethod.DeclaringType!.Name}:{baseMethod.Name}";
                Log.Warning($"Patched incorrect number of SetTarget calls (patched {replacedCount}, expected {expected}) for method {name}");
            }
        }

        // Realign before target assignment to avoid early warmup.
        private static void ResetTurretTarget(VehicleTurret turret)
        {
            if (turret == null)
                return;

            turret.SetTarget(LocalTargetInfo.Invalid);
            turret.AlignToAngleRestricted(0f);
        }

        #endregion

        #region Flying Vehicles

        private static void SetLaunchArrivalVehicle(object target, object[] args)
        {
            if (target is CompVehicleLauncher launcher && args.Length > 1)
                SetArrivalActionVehicle(args[1] as IArrivalAction, launcher.Vehicle);
        }

        private static void SetOrderArrivalVehicle(object target, object[] args)
        {
            if (target is AerialVehicleInFlight aerialVehicle && args.Length > 1)
                SetArrivalActionVehicle(args[1] as IArrivalAction, aerialVehicle.vehicle);
        }

        private static void SetCaravanLaunchArrivalVehicle(object target, object[] args)
        {
            if (target is VehicleCaravan caravan && args.Length > 1)
                SetArrivalActionVehicle(args[1] as IArrivalAction, caravan.LeadVehicle);
        }

        private static void SetArrivalActionVehicle(IArrivalAction arrivalAction, VehiclePawn vehicle)
        {
            if (arrivalAction is VehicleArrivalAction vehicleAction)
                vehicleAction.vehicle = vehicle;
        }

        private static void CleanupDestroyedCaravanAfterLaunch(object target, object[] _)
        {
            if (MP.IsExecutingSyncCommandIssuedBySelf && target is VehicleCaravan { Destroyed: true } caravan)
                Find.WorldSelector.Deselect(caravan);
        }

        private static void PostSkyfallerLeavingExposeData(VehicleSkyfaller_Leaving __instance)
        {
            // VF omits arrivalAction from saves.
            Scribe_Deep.Look(ref __instance.arrivalAction, "arrivalAction");
        }

        private static void PreLaunchSetArrivalVehicle(CompVehicleLauncher __instance, IArrivalAction arrivalAction)
        {
            if (MP.IsInMultiplayer)
                SetArrivalActionVehicle(arrivalAction, __instance.Vehicle);
        }

        private static void PreOrderFlySetArrivalVehicle(AerialVehicleInFlight __instance, IArrivalAction arrivalAction)
        {
            if (MP.IsInMultiplayer)
                SetArrivalActionVehicle(arrivalAction, __instance.vehicle);
        }

        private static void SyncGlobalTargetData(SyncWorker sync,
            ref SmashTools.Targeting.TargetData<GlobalTargetInfo> targetData)
        {
            if (sync.isWriting)
            {
                sync.Write(targetData.targets);
                return;
            }

            var targets = sync.Read<List<GlobalTargetInfo>>();
            targetData = new SmashTools.Targeting.TargetData<GlobalTargetInfo>();
            if (targets != null)
                targetData.targets.AddRange(targets);
        }

        private static void PreMoveForwardFixArrivalVehicle(AerialVehicleInFlight __instance)
        {
            if (!MP.IsInMultiplayer)
                return;

            if (__instance.flightPath?.ArrivalAction is VehicleArrivalAction action
                && action.vehicle == null
                && __instance.vehicle != null)
            {
                action.vehicle = __instance.vehicle;
            }
        }

        private static void PreResumePathPostLoad(AerialVehicleInFlight __instance, ref (float transition, Vector3 position)? __state)
        {
            if (MP.IsInMultiplayer)
                __state = (__instance.transition, __instance.position);
        }

        private static void PostResumePathPostLoad(AerialVehicleInFlight __instance, (float transition, Vector3 position)? __state)
        {
            if (__state.HasValue)
            {
                __instance.transition = __state.Value.transition;
                __instance.position = __state.Value.position;
            }
        }

        #endregion

        #region Fuel gizmo

        private static void PreFuelGizmo(Gizmo_RefuelableFuelTravel __instance, out bool __state)
        {
            __state = MP.IsInMultiplayer;
            if (!__state)
                return;

            MP.WatchBegin();
            targetFuelPercentField.Watch(__instance.refuelable);
            __instance.targetValuePct = __instance.refuelable.TargetFuelPercent;
        }

        private static void FinalizeFuelGizmo(bool __state)
        {
            if (__state)
                MP.WatchEnd();
        }

        private static void ClearVehicleTargetersForReload()
        {
            if (MP.IsInMultiplayer)
                Targeters.ClearAllTargeters();
        }

        #endregion

        #region SyncWorkers

        private static void SyncCargoTab(SyncWorker sync, ref ITab_Vehicle_Cargo tab)
        {
            if (!sync.isWriting)
                tab = new ITab_Vehicle_Cargo();
        }

        private static void SyncFuelGizmo(SyncWorker sync, ref Gizmo_RefuelableFuelTravel gizmo)
        {
            if (sync.isWriting)
                sync.Write(gizmo.refuelable);
            else
                gizmo = new Gizmo_RefuelableFuelTravel(sync.Read<CompFueledTravel>(), false);
        }

        private static void SyncVehicleComponent(SyncWorker sync, ref VehicleComponent comp)
        {
            if (sync.isWriting)
            {
                sync.Write(comp?.props?.key);
                sync.Write(comp?.vehicle);
            }
            else
            {
                var key = sync.Read<string>();
                var vehicle = sync.Read<VehiclePawn>();
                comp = key == null ? null : vehicle?.statHandler.GetComponent(key);
            }
        }

        private static void SyncVehicleTurret(SyncWorker sync, ref VehicleTurret turret)
        {
            if (sync.isWriting)
            {
                sync.Write(turret?.key);
                sync.Write(turret?.vehicle);
            }
            else
            {
                var key = sync.Read<string>();
                var vehicle = sync.Read<VehiclePawn>();
                turret = key == null ? null : vehicle?.CompVehicleTurrets?.GetTurret(key);
            }
        }

        private static void SyncAutoLoadConfig(SyncWorker sync, ref AutoLoadConfig config)
        {
            if (sync.isWriting)
                sync.Write(config.turret);
            else
                config = sync.Read<VehicleTurret>()?.loadConfig;
        }

        private static void SyncVehicleIgnitionController(SyncWorker sync, ref VehicleIgnitionController controller)
        {
            if (sync.isWriting)
            {
                sync.Write(controller.vehicle);
            }
            else
            {
                var vehiclePawn = sync.Read<VehiclePawn>();
                controller = vehiclePawn?.ignition;
            }
        }

        private static void SyncVehicleRoleHandler(SyncWorker sync, ref VehicleRoleHandler handler)
        {
            if (sync.isWriting)
            {
                sync.Write(handler?.role?.key);
                sync.Write(handler?.vehicle);
            }
            else
            {
                var roleKey = sync.Read<string>();
                var vehicle = sync.Read<VehiclePawn>();
                handler = roleKey == null ? null : vehicle?.GetHandler(roleKey);
            }
        }

        private static void SyncCommandTurret(SyncWorker sync, ref Command_Turret command)
        {
            SyncVehicleTurret(sync, ref command.turret);
            if (!sync.isWriting)
                command.vehicle = command.turret?.vehicle;
        }

        private static void SyncVehicleSettings(SyncWorker sync, ref VehiclesModSettings settings)
        {
            if (!sync.isWriting)
                settings = VehicleMod.settings;
        }

        private static void SyncLaunchProtocol(SyncWorker sync, ref LaunchProtocol launchProtocol)
        {
            if (sync.isWriting)
                sync.Write(launchProtocol?.vehicle?.CompVehicleLauncher);
            else
                launchProtocol = sync.Read<CompVehicleLauncher>()?.launchProtocol;
        }

        private static void SyncFlightNode(SyncWorker sync, ref FlightNode node)
        {
            SyncType type = typeof(FlightNode);
            type.expose = true;

            if (sync.isWriting)
                sync.Write(node, type);
            else
                node = sync.Read<FlightNode>(type);
        }

        [MpCompatSyncWorker(typeof(Designator_AreaRoadExpand), shouldConstruct = true)]
        private static void SyncAreaRoadDesignator(SyncWorker sync, ref Designator_AreaRoadExpand designator)
        {
            if (sync.isWriting)
                sync.Write(localRoadType);
            else
                Designator_AreaRoad.roadType = sync.Read<Designator_AreaRoad.RoadType>();
        }

        #endregion

        #region Sessions

        #region Load cargo session

        #region Session class

        [MpCompatRequireMod("SmashPhil.VehicleFramework")]
        private class LoadVehicleCargoSession : ExposableSession, ISessionWithTransferables, ISessionWithCreationRestrictions
        {
            public static LoadVehicleCargoSession drawingSession;
            public static bool allowedToRecacheTransferables = false;

            public override Map Map => vehicle.Map;

            private VehiclePawn vehicle;
            public List<TransferableOneWay> transferables = [];

            public bool uiDirty;
            public bool widgetDirty;

            [UsedImplicitly]
            public LoadVehicleCargoSession(Map map) : base(map)
            {
            }

            public LoadVehicleCargoSession(Map map, VehiclePawn vehicle) : base(map)
            {
                this.vehicle = vehicle;

                AddItems();
            }

            public void AddItems()
            {
                var dialog = new Dialog_LoadCargo(vehicle);
                try
                {
                    allowedToRecacheTransferables = true;
                    uiDirty = true;
                    widgetDirty = true;
                    dialog.CalculateAndRecacheTransferables();
                    transferables = dialog.transferables;
                }
                finally
                {
                    allowedToRecacheTransferables = false;
                }
            }

            public override void ExposeData()
            {
                base.ExposeData();

                Scribe_References.Look(ref vehicle, "vehicle");
                Scribe_Collections.Look(ref transferables, "transferables", LookMode.Deep);
            }

            public override bool IsCurrentlyPausing(Map map) => map == Map;

            private void OpenWindow(bool sound = true)
            {
                var dialog = PrepareDummyDialog();
                if (!sound)
                    dialog.soundAppear = null;

                Find.WindowStack.Add(dialog);
                uiDirty = true;
                widgetDirty = true;
            }

            private Dialog_LoadCargo PrepareDummyDialog()
            {
                return new Dialog_LoadCargo(vehicle)
                {
                    transferables = transferables,
                };
            }

            public void Accept()
            {
                vehicle.cargoToLoad = transferables.Where(t => t.CountToTransfer > 0).ToList();
                vehicle.Map.GetCachedMapComponent<VehicleReservationManager>().RegisterLister(vehicle, "LoadVehicle");
                Remove();
            }

            public void Reset()
            {
                SoundDefOf.Tick_Low.PlayOneShotOnCamera();
                transferables.ForEach(t => t.CountToTransfer = 0);
                uiDirty = true;
            }

            public void PackInstantly()
            {
                SoundDefOf.Tick_High.PlayOneShotOnCamera();

                foreach (var transferable in transferables)
                {
                    var things = transferable.things;
                    var count = transferable.CountToTransfer;

                    TransferableUtility.Transfer(things, count, (t, _) => vehicle.AddOrTransfer(t));
                }

                Remove();
            }

            public void SetToSendEverything()
            {
                PrepareDummyDialog().SetToSendEverything();
                uiDirty = true;
            }

            public void Remove()
            {
                MP.GetLocalSessionManager(Map).RemoveSession(this);
            }

            public static bool TryOpenLoadVehicleCargoDialog(VehiclePawn vehicle)
            {
                if (vehicle?.Map == null)
                    return false;

                var session = MP.GetLocalSessionManager(vehicle.Map).GetFirstOfType<LoadVehicleCargoSession>();
                if (session == null)
                    return false;

                session.OpenWindow();
                return true;
            }

            public static void CreateLoadVehicleCargoSession(VehiclePawn vehicle)
            {
                if (vehicle?.Map == null)
                    return;

                var manager = MP.GetLocalSessionManager(vehicle.Map);
                var session = manager.GetFirstOfType<LoadVehicleCargoSession>();
                if (session == null)
                {
                    session = new LoadVehicleCargoSession(vehicle.Map, vehicle);
                    if (!manager.AddSession(session))
                        session = null;
                }

                if (session != null && MP.IsExecutingSyncCommandIssuedBySelf)
                    session.OpenWindow();
            }

            public override FloatMenuOption GetBlockingWindowOptions(ColonistBar.Entry entry)
            {
                return new FloatMenuOption("MpVehicleCargoLoadingSession".Translate(), () =>
                {
                    SwitchToMapOrWorld(Map);
                    OpenWindow();
                });
            }

            public Transferable GetTransferableByThingId(int thingId)
                => transferables.Find(tr => tr.things.Any(t => t.thingIDNumber == thingId));

            public void Notify_CountChanged(Transferable tr) => uiDirty = true;

            public bool CanExistWith(Session other) => other is not LoadVehicleCargoSession;
        }

        #endregion

        #region Dialog Patches

        private static void SetCurrentLoadCargoSessionState(LoadVehicleCargoSession session)
        {
            LoadVehicleCargoSession.drawingSession = session;
            MP.SetCurrentSessionWithTransferables(session);
        }

        private static void PreDrawLoadCargo(Dialog_LoadCargo __instance)
        {
            if (!MP.IsInMultiplayer)
                return;

            var session = MP.GetLocalSessionManager(__instance.vehicle.Map).GetFirstOfType<LoadVehicleCargoSession>();
            if (session == null)
            {
                __instance.Close();
                return;
            }

            SetCurrentLoadCargoSessionState(session);
            MP.WatchBegin();
            showAllCargoItemsField.Watch(VehicleMod.settings);

            if (session.uiDirty)
            {
                __instance.CountToTransferChanged();
                session.uiDirty = false;
            }

            if (session.widgetDirty)
            {
                __instance.transferables = session.transferables;
                __instance.itemsTransfer = new TransferableOneWayWidget(
                    session.transferables,
                    null,
                    null,
                    null,
                    true,
                    IgnorePawnsInventoryMode.IgnoreIfAssignedToUnload,
                    false,
                    () => __instance.MassCapacity - __instance.MassUsage);

                session.widgetDirty = false;
            }
        }

        private static void FinalizeDrawLoadCargo()
        {
            if (LoadVehicleCargoSession.drawingSession != null)
            {
                MP.WatchEnd();
                SetCurrentLoadCargoSessionState(null);
            }
        }

        private static void PostShowAllCargoItemsChanged(object instances, object value)
        {
            foreach (var map in Find.Maps)
                MP.GetLocalSessionManager(map).GetFirstOfType<LoadVehicleCargoSession>()?.AddItems();
        }

        private static void PreLoadCargoButtonText(string label, ref bool __state)
        {
            if (LoadVehicleCargoSession.drawingSession != null && label == "CancelButton".Translate())
            {
                GUI.color = new Color(1f, 0.3f, 0.35f);
                __state = true;
            }
        }

        private static void PostLoadCargoButtonText(bool __state)
        {
            if (__state)
                GUI.color = Color.white;
        }

        private static void PostLoadCargoButtonTextWorker(string label, ref Widgets.DraggableResult __result)
        {
            var session = LoadVehicleCargoSession.drawingSession;
            if (session == null || !__result.AnyPressed())
                return;

            if (label == "AcceptButton".Translate())
                session.Accept();
            else if (label == "ResetButton".Translate())
                session.Reset();
            else if (label == "CancelButton".Translate())
                session.Remove();
            else if (label == "Dev: Pack Instantly")
                session.PackInstantly();
            else
                return;

            __result = Widgets.DraggableResult.Idle;
        }

        private static bool PreLoadCargoSetToSendEverything()
        {
            if (LoadVehicleCargoSession.drawingSession == null)
                return true;

            LoadVehicleCargoSession.drawingSession.SetToSendEverything();
            return false;
        }

        private static bool PreLoadCargoCalculateAndRecache()
            => !MP.IsInMultiplayer || LoadVehicleCargoSession.allowedToRecacheTransferables;

        #endregion

        #region Gizmo patches

        private static bool PreLoadCargoDialog(object __instance)
        {
            if (!MP.IsInMultiplayer)
                return true;

            var vehicle = vehiclePawnInnerClassParentField(__instance);
            if (!LoadVehicleCargoSession.TryOpenLoadVehicleCargoDialog(vehicle))
                LoadVehicleCargoSession.CreateLoadVehicleCargoSession(vehicle);

            return false;
        }

        #endregion

        #endregion

        #region Flying vehicle landing session

        #region Session class

        [MpCompatRequireMod("SmashPhil.VehicleFramework")]
        private class FlyingVehicleTargetedLandingSession : ExposableSession, ISessionWithCreationRestrictions
        {
            private List<VehiclePawn> vehicles = [];
            public override Map Map { get; }
            public override bool IsSessionValid => !vehicles.NullOrEmpty();

            private FlyingVehicleTargetedLandingSession(Map map) : base(map)
                => Map = map;

            public override bool IsCurrentlyPausing(Map map)
                => map == Map;

            public override FloatMenuOption GetBlockingWindowOptions(ColonistBar.Entry entry)
            {
                if (entry.map != Map)
                    return null;

                return new FloatMenuOption("MpVehicleAerialLandingSession".Translate(), () =>
                {
                    if (!IsSessionValid)
                    {
                        Remove();
                    }
                    else
                    {
                        SwitchToMapOrWorld(Map);

                        if (vehicles.Count == 1)
                            StartVehicleLandingTargeter(vehicles[0]);
                        else
                            SetupVehicleListFloatMenu();
                    }
                });
            }

            public override void ExposeData()
            {
                base.ExposeData();

                Scribe_Deep.Look(ref vehicles, "vehicles", this);
            }

            public bool CanExistWith(Session other)
                => other is not FlyingVehicleTargetedLandingSession;

            public void VehicleArrival(VehiclePawn vehicle, LocalTargetInfo target, Rot4 rot)
                => VehicleArrivalById(vehicle.thingIDNumber, target, rot);

            // MP cannot serialize vehicles held only by this session.
            public void VehicleArrivalById(int vehicleId, LocalTargetInfo target, Rot4 rot)
            {
                var vehicle = vehicles.Find(v => v.thingIDNumber == vehicleId);
                if (vehicle == null)
                    return;

                if (vehicle.Spawned)
                {
                    vehicles.Remove(vehicle);
                    return;
                }

                var vehicleSkyfaller = (VehicleSkyfaller_Arriving)ThingMaker.MakeThing(vehicle.CompVehicleLauncher.Props.skyfallerIncoming);
                vehicleSkyfaller.vehicle = vehicle;
                GenSpawn.Spawn(vehicleSkyfaller, target.Cell, Map, rot);

                vehicles.Remove(vehicle);
                if (LandingTargeter.Instance.vehicle == vehicle)
                    LandingTargeter.Instance.StopTargeting();

                if (!IsSessionValid)
                    Remove();
            }

            public void Remove() => MP.GetLocalSessionManager(Map).RemoveSession(this);

            public static void HandleTargetedVehicleArrival(VehiclePawn vehicle, Map map)
            {
                MP.GetLocalSessionManager(map)
                    .GetOrAddSession(new FlyingVehicleTargetedLandingSession(map))
                    .vehicles
                    .AddDistinct(vehicle);
            }

            private void SetupVehicleListFloatMenu()
            {
                var list = new List<FloatMenuOption>();

                foreach (var vehicle in vehicles)
                {
                    string name;
                    if (vehicle.Nameable && vehicle.Name != null)
                        name = $"{vehicle.VehicleDef.LabelCap} - {vehicle.Name}";
                    else
                        name = vehicle.VehicleDef.LabelCap;

                    list.Add(new FloatMenuOption(name, () => StartVehicleLandingTargeter(vehicle)));
                }

                Find.WindowStack.Add(new FloatMenu(list, "MpVehiclesWaitingToLand"));
            }

            private void StartVehicleLandingTargeter(VehiclePawn vehicle)
            {
                var allowRotating = false;
                if (vehicle.VehicleDef.rotatable)
                    allowRotating = vehicle.CompVehicleLauncher.launchProtocol.LandingProperties?.forcedRotation == null;

                LandingTargeter.Instance.BeginTargeting(
                    vehicle,
                    Map,
                    (target, rot) => VehicleArrival(vehicle, target, rot),
                    allowRotating: allowRotating);
            }
        }

        #endregion

        #region Map patches

        private static void PreventMapRemovalForLandingSessions(ref bool __result, Map ___map)
        {
            // Keep the destination map alive while choosing a landing cell.
            if (MP.IsInMultiplayer && !__result)
                __result = MP.GetLocalSessionManager(___map).GetFirstOfType<FlyingVehicleTargetedLandingSession>() != null;
        }

        private static bool PreTargetedDropVehicleArrival(VehiclePawn vehicle, Map map)
        {
            if (!MP.IsInMultiplayer)
                return true;

            FlyingVehicleTargetedLandingSession.HandleTargetedVehicleArrival(vehicle, map);
            return false;
        }

        #endregion

        #endregion

        #region Shared

        private static void InsertSwitchToMap(Window __instance, Rect __0)
        {
            if (!MP.IsInMultiplayer)
                return;

            using (new TextBlock(GameFont.Tiny))
            {
                var switchToMapText = "MpCompatSwitchToMap".Translate();
                var width = switchToMapText.GetWidthCached() + 25;

                if (Widgets.ButtonText(new Rect(__0.xMax - width, 5, width, 24), switchToMapText))
                    __instance.Close();
            }
        }

        #endregion

        #endregion

        #region Determinism

        [MpCompatPostfix(typeof(VehiclePawn), nameof(VehiclePawn.Tick))]
        private static void PostVehicleTick(VehiclePawn __instance)
        {
            if (!MP.IsInMultiplayer)
                return;

            if (!__instance.Spawned)
                return;

            var turretsComp = __instance.CompVehicleTurrets;
            if (turretsComp == null)
                return;

            // Turrets may not tick; update aim here instead of during drawing.
            foreach (var turret in turretsComp.turrets)
            {
                if (turret.IsTargetable || turret.attachedTo != null)
                {
                    turret.UpdateRotationLock();
                    turret.TurretRotation = Mathf.Repeat(turret.TurretRotation, 360f);
                }
            }
        }

        [MpCompatPrefix(typeof(TurretTargeter), nameof(TurretTargeter.Turret), methodType: MethodType.Getter)]
        private static bool PreTurretTargeterCurrentTurretGetter()
        {
            return !MP.IsInMultiplayer || MP.InInterface;
        }

        [MpCompatPrefix(typeof(VehicleTweener), nameof(VehicleTweener.TweenedPos), methodType: MethodType.Getter)]
        private static bool PreTweenedPosGetter(VehicleTweener __instance, ref Vector3 __result)
        {
            if (!MP.IsInMultiplayer || MP.InInterface)
                return true;

            __result = __instance.TweenedPosRoot();
            return false;
        }

        private static void PreRenderPawnInternal(VehiclePawn __instance, ref (Rot4 rotation, float angle)? __state)
        {
            if (MP.InInterface)
                __state = (__instance.Rotation, __instance.angle);
        }

        private static void PostRenderPawnInternal(VehiclePawn __instance, ref (Rot4 rotation, float angle)? __state)
        {
            if (__state is {} state)
                (__instance.Rotation, __instance.angle) = (state.rotation, state.angle);
        }

        #endregion

        #region Designator

        [MpCompatPrefix(typeof(Designator_AreaRoad), nameof(Designator_AreaRoad.ProcessInput), 1)]
        private static void StoreNewLocalRoadType(Designator_AreaRoad.RoadType ___roadType)
            => localRoadType = ___roadType;

        [MpCompatPrefix(typeof(Designator_AreaRoad), nameof(Designator_AreaRoad.DesignateSingleCell))]
        [MpCompatPrefix(typeof(Designator_AreaRoad), nameof(Designator_AreaRoad.CanDesignateCell))]
        private static void RestoreLocalRoadType()
        {
            if (MP.IsInMultiplayer && !MP.IsExecutingSyncCommand)
                Designator_AreaRoad.roadType = localRoadType;
        }

        #endregion

        #region Upgrade Fixes

        private static bool ShouldExecuteWhenFinished()
        {
            if (!UnityData.IsInMainThread)
                return false;
            if (!MP.IsInMultiplayer)
                return true;

            return PatchingUtilities.AllowedToRunLongEvents;
        }

        [MpCompatTranspiler(typeof(UpgradeNode), nameof(UpgradeNode.AddOverlays))]
        private static IEnumerable<CodeInstruction> FixHostOverlayInit(IEnumerable<CodeInstruction> instr, MethodBase baseMethod)
        {
            // Defer host overlay initialization until play data loads.

            var target = AccessTools.DeclaredPropertyGetter(typeof(UnityData), nameof(UnityData.IsInMainThread));
            var replacement = MpMethodUtil.MethodOf(ShouldExecuteWhenFinished);

            return instr.ReplaceMethod(target, replacement, baseMethod, expectedReplacements: 1);
        }

        #endregion
    }
}
