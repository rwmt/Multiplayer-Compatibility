using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
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

        // MP fields
        private static Type mpTransferableReferenceType;
        private static ISyncField syncTradeableCount;

        // Mp Compat fields
        private static bool shouldSyncInInterface = false;

        // VehiclesModSettings
        private static ISyncField showAllCargoItemsField;

        // VehiclePawn.<>c__DisplayClass250_0
        private static AccessTools.FieldRef<object, VehiclePawn> vehiclePawnInnerClassParentField;
        
        // Designator_AreaRoad
        private static Designator_AreaRoad.RoadType localRoadType = Designator_AreaRoad.RoadType.Prioritize;

        // Gizmo_RefuelableFuelTravel.refuelable field ref (cached for PreToggleFuelSwitch)
        private static readonly AccessTools.FieldRef<Gizmo_RefuelableFuelTravel, CompFueledTravel> fuelGizmoRefuelableField
            = AccessTools.FieldRefAccess<Gizmo_RefuelableFuelTravel, CompFueledTravel>("refuelable");

        #endregion

        #region Constructor

        public VehicleFramework(ModContentPack mod)
        {
            // Some stuff needs it, some doesn't. Too much effort going through everything to see what
            // needs late patch and what doesn't.
            LongEventHandler.ExecuteWhenFinished(LatePatch);
        }

        #endregion

        #region Main patch

        private static void LatePatch()
        {
            #region MP Compat

            MethodInfo method;

            static ISyncMethod TrySyncDeclaredMethod(Type targetType, string targetMethodName)
            {
                var declaredMethod = AccessTools.DeclaredMethod(targetType, targetMethodName);
                if (declaredMethod != null)
                    return MP.RegisterSyncMethod(declaredMethod);
                return null;
            }

            // Should be initialized by PatchCancelInInterface calls later on,
            // so this exists here as an extra safety in case those ever get removed later on.
            MpCompatPatchLoader.LoadPatch<VehicleFramework>();

            // Needed for overlay fix.
            PatchingUtilities.PatchLongEventMarkers();

            #endregion

            #region VehicleFramework

            #region Multithreading

            {
                // Disable threading in those specific methods
                var methods = new[]
                {
                    AccessTools.DeclaredMethod(typeof(PathingHelper), nameof(PathingHelper.RecalculatePerceivedPathCostAt)),
                    AccessTools.DeclaredMethod(typeof(PathingHelper), nameof(PathingHelper.ThingAffectingRegionsOrientationChanged)),
                    AccessTools.DeclaredMethod(typeof(PathingHelper), nameof(PathingHelper.ThingAffectingRegionsStateChange)),
                };
                var transpiler = new HarmonyMethod(typeof(VehicleFramework), nameof(ReplaceThreadAvailable));
                foreach (var m in methods.Where(m => m != null))
                    MpCompat.harmony.Patch(m, transpiler: transpiler);

                // Vehicles.WorldVehiclePathGrid:RecalculateAllPerceivedPathCosts is going to run rarely enough
                // and unless something changed on the world map (like changed tiles) then the result should end
                // up being the same. The thread should be safe (in general) to keep in a separate thread.

                // Vehicles.VehiclePathing has two methods which (likely) can be kept
                // threaded. They are used for calculating ConcurrentListThings, but seems like is
                // ever used for debug related stuff. (RegisterInVehicleRegions/DeregisterInVehicleRegions)
                // This may not be 100% true (they could be referenced by name somewhere and used bu a transpiler).
            }

            #endregion

            #region Gizmos

            {
                // Buildings

                // Switch to next verb (0), cancel target (1)
                MpCompat.RegisterLambdaMethod(typeof(Building_Artillery), nameof(Building_Artillery.GetGizmos), 0, 1);


                // Vehicle pawn

                // Ordinals verified from compiled DLL IL (release build):
                // Cancel load cargo (5), fish toggle (8),
                // force leave caravan (14), cancel forming caravan (15)
                // DisembarkAll is a method reference (no ordinal).
                // Load cargo (6) opens dialog, handled by LoadVehicleCargoSession.
                // Fish isActive (7) is a getter, doesn't need syncing.
                // Disembark all — method reference (no ordinal), sync directly
                MP.RegisterSyncMethod(typeof(VehiclePawn), nameof(VehiclePawn.DisembarkAll));
                // Disembark single (13) — synced via wrapper SyncedDisembarkPawn instead of lambda
                //   or direct method (pawns inside vehicles are despawned, MP can't serialize them)
                // Haul pawn target callback (10) — called when player selects a pawn in HaulTargeter
                MpCompat.RegisterLambdaDelegate(typeof(VehiclePawn), nameof(VehiclePawn.GetGizmos), 5, 8, 10, 14, 15);
                MpCompat.harmony.Patch(
                    MpMethodUtil.GetLambda(typeof(VehiclePawn), nameof(VehiclePawn.GetGizmos), lambdaOrdinal: 13),
                    prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(PreDisembarkSinglePawn)));
                MP.RegisterSyncMethod(typeof(VehicleFramework), nameof(SyncedDisembarkPawn));

                // Toggle drafted or (if moving) engage brakes.
                MpCompat.RegisterLambdaMethod(typeof(VehicleIgnitionController), nameof(VehicleIgnitionController.GetGizmos), 1);

                // Comps

                // Target fuel level setter, used from Gizmo_RefuelableFuelTravel
                MP.RegisterSyncMethod(typeof(CompFueledTravel), nameof(CompFueledTravel.TargetFuelPercent));
                // Refuel from inventory, used from Gizmo_RefuelableFuelTravel
                MP.RegisterSyncMethod(typeof(CompFueledTravel), nameof(CompFueledTravel.Refuel), [typeof(List<Thing>)]);
                MP.RegisterSyncMethod(typeof(CompFueledTravel), nameof(CompFueledTravel.Refuel), [typeof(float)]);
                // CompGetGizmosExtra has no lambdas now (refuelGizmo is a cached field)
                // Auto-refuel toggle, charging toggle, and refuel-from-cargo moved to Gizmo_RefuelableFuelTravel.
                // Refuel from cargo opens Dialog_Slider → calls ConsumeFuelFromInventory
                MP.RegisterSyncMethod(typeof(CompFueledTravel), nameof(CompFueledTravel.ConsumeFuelFromInventory));
                // Sync ToggleAutoRefuel and ToggleCharging via prefix → synced wrapper on the comp.
                // DrawHeader calls ToggleAutoRefuel directly (not ToggleSwitch).
                MpCompat.harmony.Patch(
                    AccessTools.DeclaredMethod(typeof(Gizmo_RefuelableFuelTravel), "ToggleAutoRefuel"),
                    prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(PreToggleFuelSwitch)));
                MpCompat.harmony.Patch(
                    AccessTools.DeclaredMethod(typeof(Gizmo_RefuelableFuelTravel), "ToggleCharging"),
                    prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(PreToggleFuelSwitch)));
                MP.RegisterSyncMethod(typeof(VehicleFramework), nameof(SyncedToggleFuelSwitch));
                // (Dev) set fuel to 0 (0), set fuel to max (1), set fuel to 99.99% (2)
                // RefuelHalfway is a method reference (not a lambda), so doesn't consume an ordinal
                MpCompat.RegisterLambdaMethod(typeof(CompFueledTravel), nameof(CompFueledTravel.DevModeGizmos), 0, 1, 2).SetDebugOnly();
                // (Dev) set fuel to 0/max
                MpCompat.RegisterLambdaMethod(typeof(CompFueledTravel), nameof(CompFueledTravel.CompCaravanGizmos), 0, 1).SetDebugOnly();

                MP.RegisterSyncMethod(typeof(CompVehicleTurrets), nameof(CompVehicleTurrets.SetQuotaLevel));
                // Deploy turret is now a cached field (deployToggle), no lambda to register
                // (Dev) full reload turret — only lambda in CompGetGizmosExtra now
                MpCompat.RegisterLambdaDelegate(typeof(CompVehicleTurrets), nameof(CompVehicleTurrets.CompGetGizmosExtra), 0).SetDebugOnly();


                // Turret syncing in separate region

                // `Vehicles.Gizmos` includes patches to add or edit gizmos in existing GetGizmos methods:
                // `AddVehicleGizmosPassthrough` opens `Dialog_FormVehicleCaravan`, which we need to handle instead.
                // `GizmosForVehicleCaravans` calls `CaravanFormingUtility.LateJoinFormingCaravan`, which is synced through MP.
            }

            #endregion

            #region Turrets

            {
                // We need to selectively pick which "FireTurret" method get synced.
                // It would be beneficial if we could sync `FireTurrets` method, as it would
                // prevent a lot of duplicated syncing of "FireTurret" if there are multiple turrets.
                // However, the issue is that not all calls to "FireTurret" are an action we want to sync,
                // for example when it opens targeter.

                // Static turrets
                MP.RegisterSyncMethod(typeof(Command_CooldownAction), nameof(Command_CooldownAction.FireTurret));
                MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(Command_CooldownAction), nameof(Command_CooldownAction.GizmoOnGUI)),
                    transpiler: new HarmonyMethod(typeof(VehicleFramework), nameof(ReplaceSetTargetCall)));

                // Targetable turrets
                method = MpMethodUtil.GetLambda(typeof(Command_TargeterCooldownAction), nameof(Command_TargeterCooldownAction.FireTurret), lambdaOrdinal: 0);
                MP.RegisterSyncDelegate(typeof(Command_TargeterCooldownAction), method.DeclaringType!.Name, method.Name);
                MpCompat.harmony.Patch(method, prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(PreSetTurretTarget)));

                // Called from Vehicles.TurretTargeter:BeginTargeting
                PatchingUtilities.PatchCancelInInterface(AccessTools.DeclaredMethod(typeof(VehicleTurret), nameof(VehicleTurret.StartTicking)));
                // Called from Vehicles.TurretTargeter:TargeterUpdate and Vehicles.TurretTargeter:StopTargeting(bool)
                PatchingUtilities.PatchCancelInInterface(AccessTools.DeclaredMethod(typeof(VehicleTurret), nameof(VehicleTurret.AlignToAngleRestricted)));
                MP.RegisterSyncMethod(typeof(VehicleTurret), nameof(VehicleTurret.CycleFireMode));
                // Future-proofing, currently only affects the base type as the subclass doesn't override those.
                foreach (var subclass in typeof(VehicleTurret).AllSubclasses().Concat(typeof(VehicleTurret)))
                {
                    method = AccessTools.DeclaredMethod(subclass, nameof(VehicleTurret.SetTarget));
                    if (method != null)
                    {
                        // Sync the call
                        MP.RegisterSyncMethod(method);
                        // But only allow it to be synced under very specific circumstances
                        MpCompat.harmony.Patch(method, prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(CancelTurretSetTargetSync)));
                    }

                    // Reload has multiple overloads — sync both parameterless and (ThingDef, bool)
                    // VVE's FueledVehicleTurret.SubGizmo_ReloadFromFuel calls Reload(null, true)
                    var reloadMethod = AccessTools.DeclaredMethod(subclass, nameof(VehicleTurret.Reload), []);
                    if (reloadMethod != null)
                        MP.RegisterSyncMethod(reloadMethod);
                    var reloadWithArgs = AccessTools.DeclaredMethod(subclass, nameof(VehicleTurret.Reload), [typeof(ThingDef), typeof(bool)]);
                    if (reloadWithArgs != null)
                        MP.RegisterSyncMethod(reloadWithArgs);
                    // TryClearChamber — used by VVE's FueledVehicleTurret.SubGizmo_AmmoToFuel
                    TrySyncDeclaredMethod(subclass, nameof(VehicleTurret.TryClearChamber));
                    TrySyncDeclaredMethod(subclass, nameof(VehicleTurret.SwitchAutoTarget));
                }

                // Stop the call from interface, called from TurretRotation getter. We update it during ticking.
                PatchingUtilities.PatchCancelInInterface(AccessTools.DeclaredMethod(typeof(VehicleTurret), nameof(VehicleTurret.UpdateRotationLock)));
            }

            #endregion

            #region Float Menus

            {
                // Enter vehicle. Can't sync through TryTakeOrderedJob, as the method does a bit more stuff.
                MpCompat.RegisterLambdaDelegate(typeof(VehiclePawn), nameof(VehiclePawn.GetFloatMenuOptions), 0);
                // MultiplePawnFloatMenuOptions now uses OrderPawns method reference, no lambda to sync
                // The boarding action is handled through the method reference directly.
            }

            #endregion

            #region RNG

            {
                // Motes
                PatchingUtilities.PatchPushPopRand(new[]
                {
                    "Vehicles.Verb_ShootRealistic:InitTurretMotes",
                    "Vehicles.VehicleTurret:InitTurretMotes",
                });
                // Launch flecks consume game RNG during tick — isolate so launch visuals don't desync
                // ThrowFleck has multiple overloads, patch both explicitly
                // ThrowFleck has 3 overloads — patch all
                foreach (var throwFleck in AccessTools.GetDeclaredMethods(typeof(LaunchProtocol))
                    .Where(m => m.Name == nameof(LaunchProtocol.ThrowFleck)))
                    PatchingUtilities.PatchPushPopRand(throwFleck);
                // Vehicles.CompFueledTravel:DrawMotes - most likely not needed, RNG calls before GenView.ShouldSpawnMotesAt
            }

            #endregion

            #region DrawAt determinism

            {
                // VehiclePawn.DrawAt(in Vector3, Rot8, float) uses `in` keyword which
                // MpCompatPrefix attribute can't match. Patch manually.
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

                // Called when accepted from change color dialog
                MpCompat.RegisterLambdaMethod("Vehicles.VehiclePawn", "ChangeColor", 0);

                #endregion

                #region Load cargo

                // Sync creation of session
                MP.RegisterSyncMethod(typeof(LoadVehicleCargoSession), nameof(LoadVehicleCargoSession.CreateLoadVehicleCargoSession));
                // Sync load cargo session methods
                MP.RegisterSyncMethod(typeof(LoadVehicleCargoSession), nameof(LoadVehicleCargoSession.Accept));
                MP.RegisterSyncMethod(typeof(LoadVehicleCargoSession), nameof(LoadVehicleCargoSession.Reset));
                MP.RegisterSyncMethod(typeof(LoadVehicleCargoSession), nameof(LoadVehicleCargoSession.PackInstantly));
                MP.RegisterSyncMethod(typeof(LoadVehicleCargoSession), nameof(LoadVehicleCargoSession.SetToSendEverything));
                MP.RegisterSyncMethod(typeof(LoadVehicleCargoSession), nameof(LoadVehicleCargoSession.Remove));

                // Setting value is changeable from load cargo dialog
                showAllCargoItemsField = MP.RegisterSyncField(typeof(VehiclesModSettings), nameof(VehiclesModSettings.showAllCargoItems))
                    .PostApply(PostShowAllCargoItemsChanged);
                // Since the ability to register a sync field with instance path like in mp (Type, string string),
                // we must as a workaround provide a sync worker the Vehicle Framework settings type.
                // Alternatively we could just call the MP method directly through reflection, but let's avoid doing that.
                MP.RegisterSyncWorker<VehiclesModSettings>(SyncVehicleSettings);

                // Capture drawing so we can tie the dialog to the session and set the correct current session with transferables.
                // Mp prefers making a subclass of the session itself for it, we're doing it by patching it to avoid making extra classes.
                MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(Dialog_LoadCargo), nameof(Dialog_LoadCargo.DoWindowContents)),
                    prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(PreDrawLoadCargo)),
                    finalizer: new HarmonyMethod(typeof(VehicleFramework), nameof(FinalizeDrawLoadCargo)));

                // Catch dev option to select everything to be sent
                MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(Dialog_LoadCargo), "SetToSendEverything"),
                    prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(PreLoadCargoSetToSendEverything)));

                // Replace the `Widgets.ButtonText` for several buttons with our own to handle MP-specific stuff.
                MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(Dialog_LoadCargo), nameof(Dialog_LoadCargo.BottomButtons)),
                    transpiler: new HarmonyMethod(typeof(VehicleFramework), nameof(ReplaceButtonsTranspiler)));

                // Catch load cargo dialog gizmo to open session dialog tied to it or create session if there's none
                // Ordinal 6 = open Dialog_LoadCargo (verified from compiled DLL IL)
                method = MpMethodUtil.GetLambda(typeof(VehiclePawn), nameof(VehiclePawn.GetGizmos), lambdaOrdinal: 6);
                vehiclePawnInnerClassParentField = AccessTools.FieldRefAccess<VehiclePawn>(method.DeclaringType, "<>4__this");
                MpCompat.harmony.Patch(method, prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(PreLoadCargoDialog)));

                // Prevent the call in MP, as it'll mess with transferables if re-opening the window.
                MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(Dialog_LoadCargo), "CalculateAndRecacheTransferables"),
                    prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(PreLoadCargoCalculateAndRecache)));

                #endregion

                #region Shared

                // Insert "Switch to map" button to the dialogs with session
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
                // Technically there's only 2 types here, with the type itself never used besides as a base class...
                // May as well futureproof this, I suppose.
                foreach (var type in typeof(ITab_Airdrop_Container).AllSubclasses().Concat(typeof(ITab_Airdrop_Container)))
                {
                    TrySyncDeclaredMethod(type, "InterfaceDrop")?.SetContext(SyncContext.MapSelected);
                    TrySyncDeclaredMethod(type, "InterfaceDropAll")?.SetContext(SyncContext.MapSelected);
                }

                // Used by Vehicles.ITab_Vehicle_Passengers and Vehicles.WITab_Vehicle_Manifest
                method = AccessTools.DeclaredMethod(typeof(VehicleTabHelper_Passenger), nameof(VehicleTabHelper_Passenger.HandleDragEvent));
                MpCompat.harmony.Patch(method, prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(PreHandleDragEvent)));
                MP.RegisterSyncMethod(typeof(VehicleFramework), nameof(SyncedHandleDragEvent));

                // WITab_AerialVehicle_Items
                // Aerial vehicle inventory tab
                var typesThing = new[] { typeof(Thing), typeof(AerialVehicleInFlight) };
                var typesTransferable = new[] { typeof(TransferableImmutable), typeof(AerialVehicleInFlight) };

                // Abandon non-pawn Thing
                method = MpMethodUtil.GetLambda(
                    typeof(AerialVehicleAbandonOrBanishHelper),
                    nameof(AerialVehicleAbandonOrBanishHelper.TryAbandonOrBanishViaInterface),
                    MethodType.Normal,
                    typesThing,
                    0);
                MP.RegisterSyncDelegate(typeof(AerialVehicleAbandonOrBanishHelper), method.DeclaringType!.Name, method.Name);
                // Abandon specific Pawn, replace the vanilla banish interaction with our synced one as
                // syncing of the pawn fails here. All the other methods redirect pawn banishing here.
                MpCompat.harmony.Patch(AccessTools.DeclaredMethod(
                        typeof(AerialVehicleAbandonOrBanishHelper),
                        nameof(AerialVehicleAbandonOrBanishHelper.TryAbandonOrBanishViaInterface),
                        typesThing),
                    transpiler: new HarmonyMethod(typeof(VehicleFramework), nameof(ReplaceVanillaBanishDialog)));
                MP.RegisterSyncMethod(typeof(VehicleFramework), nameof(SyncedBanishPawn));

                // Abandon non-pawn Transferable
                method = MpMethodUtil.GetLambda(
                    typeof(AerialVehicleAbandonOrBanishHelper),
                    nameof(AerialVehicleAbandonOrBanishHelper.TryAbandonOrBanishViaInterface),
                    MethodType.Normal,
                    typesTransferable,
                    0);
                MP.RegisterSyncDelegate(typeof(AerialVehicleAbandonOrBanishHelper), method.DeclaringType!.Name, method.Name);

                // Abandon specific count Thing
                method = MpMethodUtil.GetLambda(
                    typeof(AerialVehicleAbandonOrBanishHelper),
                    nameof(AerialVehicleAbandonOrBanishHelper.TryAbandonSpecificCountViaInterface),
                    MethodType.Normal,
                    typesThing,
                    0);
                MP.RegisterSyncDelegate(typeof(AerialVehicleAbandonOrBanishHelper), method.DeclaringType!.Name, method.Name);

                // Abandon specific count Transferable
                method = MpMethodUtil.GetLambda(
                    typeof(AerialVehicleAbandonOrBanishHelper),
                    nameof(AerialVehicleAbandonOrBanishHelper.TryAbandonSpecificCountViaInterface),
                    MethodType.Normal,
                    typesTransferable,
                    0);
                MP.RegisterSyncDelegate(typeof(AerialVehicleAbandonOrBanishHelper), method.DeclaringType!.Name, method.Name);

                // CompUpgradeTree, methods called from ITab_Vehicle_Upgrades
                // Serializer for UpgradeNode for CompUpgradeTree specifically.
                // Making a sync worker for it would likely require iterating over each UpgradeTreeDef.
                var upgradeNodeSerializer = Serializer.New(
                    (UpgradeNode upgrade, object target, object[] _) => (target: ((CompUpgradeTree)target).Props.def, key: upgrade.key),
                    tuple => tuple.target.GetNode(tuple.key)
                );
                // Start installing upgrade
                MP.RegisterSyncMethod(typeof(CompUpgradeTree), nameof(CompUpgradeTree.StartUnlock))
                    .TransformArgument(0, upgradeNodeSerializer);
                // Start removing upgrade
                MP.RegisterSyncMethod(typeof(CompUpgradeTree), nameof(CompUpgradeTree.RemoveUnlock))
                    .TransformArgument(0, upgradeNodeSerializer);
                // Cancel upgrading, doesn't use UpgradeNode
                MP.RegisterSyncMethod(typeof(CompUpgradeTree), nameof(CompUpgradeTree.ClearUpgrade));
                // Dev mode upgrade instantly
                MP.RegisterSyncMethod(typeof(CompUpgradeTree), nameof(CompUpgradeTree.FinishUnlock))
                    .TransformArgument(0, upgradeNodeSerializer)
                    .SetDebugOnly();
                // Dev mode remove upgrade
                MP.RegisterSyncMethod(typeof(CompUpgradeTree), nameof(CompUpgradeTree.ResetUnlock))
                    .TransformArgument(0, upgradeNodeSerializer)
                    .SetDebugOnly();
            }

            #endregion

            #region Flying vehicles

            {
                    MP.RegisterSyncWorker<LaunchProtocol>(SyncLaunchProtocol, isImplicit: true);
                MP.RegisterSyncWorker<FlightNode>(SyncFlightNode);
                MP.RegisterSyncWorker<VehicleArrivalAction>(SyncVehicleArrivalAction, isImplicit: true);
                // Launch parameter type is IArrivalAction (interface), not VehicleArrivalAction (base class)
                MP.RegisterSyncWorker<IArrivalAction>(SyncIArrivalAction, isImplicit: true);

                // Sync launch via prefix → static sync method to avoid ThingComp serialization.
                // MP's ThingComp reader has a bug: if the parent Thing resolves to null, it
                // returns without reading the compIndex ushort, misaligning all subsequent reads.
                // By using a static sync method with the vehicle as a plain Thing arg, we avoid
                // the ThingComp serializer entirely.
                MP.RegisterSyncMethod(typeof(VehicleFramework), nameof(SyncedLaunch));
                MpCompat.harmony.Patch(
                    AccessTools.DeclaredMethod(typeof(CompVehicleLauncher), nameof(CompVehicleLauncher.Launch)),
                    prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(PreLaunch)));

                // Sync OrderFlyToTiles via prefix → static sync method for consistency
                // and to set the arrival action vehicle reference before execution.
                MP.RegisterSyncMethod(typeof(VehicleFramework), nameof(SyncedOrderFlyToTiles));
                MpCompat.harmony.Patch(
                    AccessTools.DeclaredMethod(typeof(AerialVehicleInFlight), nameof(AerialVehicleInFlight.OrderFlyToTiles)),
                    prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(PreOrderFlyToTiles)));

                // Sync VehicleCaravan.Launch — it creates AerialVehicleInFlight AND calls
                // OrderFlyToTiles. Both must happen atomically during sync execution,
                // otherwise the world object only exists on the originating player.
                MP.RegisterSyncMethod(typeof(VehicleFramework), nameof(SyncedCaravanLaunch));
                MpCompat.harmony.Patch(
                    AccessTools.DeclaredMethod(typeof(VehicleCaravan), nameof(VehicleCaravan.Launch)),
                    prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(PreCaravanLaunch)));

                // Sync StartTargetingLocalMap for the non-spawned case (caravan on world map).
                // When selecting "land in existing map", the flow bypasses VehicleCaravan.Launch
                // and calls StartTargetingLocalMap directly, which creates AerialVehicleInFlight
                // + OrderFlyToTiles. Must be synced atomically like caravan launch.
                MP.RegisterSyncMethod(typeof(VehicleFramework), nameof(SyncedWorldVehicleFlyToMap));
                MpCompat.harmony.Patch(
                    AccessTools.DeclaredMethod(typeof(LaunchProtocol), nameof(LaunchProtocol.StartTargetingLocalMap)),
                    prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(PreStartTargetingLocalMap)));

                // VF bug: VehicleSkyfaller_Leaving.ExposeData doesn't save arrivalAction.
                // In MP, saves can happen between skyfaller creation and LeaveMap, losing the action.
                MpCompat.harmony.Patch(
                    AccessTools.DeclaredMethod(typeof(VehicleSkyfaller_Leaving), nameof(VehicleSkyfaller_Leaving.ExposeData)),
                    postfix: new HarmonyMethod(typeof(VehicleFramework), nameof(PostSkyfallerLeavingExposeData)));

                // Ensure arrival action has the vehicle reference set during sync execution.
                // These prefixes run when Launch/OrderFlyToTiles execute inside the synced wrappers.
                MpCompat.harmony.Patch(
                    AccessTools.DeclaredMethod(typeof(CompVehicleLauncher), nameof(CompVehicleLauncher.Launch)),
                    prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(PreLaunchSetArrivalVehicle)));
                MpCompat.harmony.Patch(
                    AccessTools.DeclaredMethod(typeof(AerialVehicleInFlight), nameof(AerialVehicleInFlight.OrderFlyToTiles)),
                    prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(PreOrderFlySetArrivalVehicle)));

                // Ensure arrival action's vehicle reference is valid before arrival.
                // The vehicle Scribe_Reference may fail to resolve when inside AerialVehicleInFlight.
                // Patch MoveForward to fix the vehicle ref before ConsumeNode calls Arrived.
                MpCompat.harmony.Patch(
                    AccessTools.DeclaredMethod(typeof(AerialVehicleInFlight), nameof(AerialVehicleInFlight.MoveForward)),
                    prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(PreMoveForwardFixArrivalVehicle)));

                // VF's ResumePathPostLoad calls OrderFlyToTiles which resets transition to 0.
                // In MP, save/load cycles happen during sync, resetting flight progress.
                // Preserve transition and position across the reload.
                MpCompat.harmony.Patch(
                    AccessTools.DeclaredMethod(typeof(AerialVehicleInFlight), "ResumePathPostLoad"),
                    prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(PreResumePathPostLoad)),
                    postfix: new HarmonyMethod(typeof(VehicleFramework), nameof(PostResumePathPostLoad)));

                // Deselect destroyed vehicle caravans and pick their respective aerial vehicles (if there are any).
                MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(VehicleCaravan), nameof(VehicleCaravan.GetInspectString)),
                    prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(CleanupDestroyedCaravans)));

                // Aerial vehicles have multiple arrival actions at settlements
                // and the like. This specific ones orders the vehicle to fly to
                // a settlement and land on a specific tile, despite it not even
                // being loaded in the first place. Once the vehicle arrives it
                // load the map and forces the player to target the location to
                // land on. We need to sync the land-and-pick-cell interaction.

                // Capture and stop the vehicle arrival, instead starting a session
                MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(AerialVehicleArrivalModeWorker_TargetedDrop), nameof(AerialVehicleArrivalModeWorker_TargetedDrop.VehicleArrived)),
                    prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(PreTargetedDropVehicleArrival)));

                // Prevent the map from being removed if the landing session is active
                MpCompat.harmony.Patch(AccessTools.DeclaredPropertyGetter(typeof(MapPawns), nameof(MapPawns.AnyPawnBlockingMapRemoval)),
                    postfix: new HarmonyMethod(typeof(VehicleFramework), nameof(PreventMapRemovalForLandingSessions)) { after = ["SmashPhil.VehicleFramework"] });

                MP.RegisterSyncMethod(typeof(FlyingVehicleTargetedLandingSession), nameof(FlyingVehicleTargetedLandingSession.Remove));
                MP.RegisterSyncMethod(typeof(FlyingVehicleTargetedLandingSession), nameof(FlyingVehicleTargetedLandingSession.VehicleArrivalById));
            }

            #endregion

            #region SyncWorkers

            {
                // Special sync for vehicle pawn and some of its comps in case it's currently held by flying vehicle
                MP.RegisterSyncWorker<VehiclePawn>(SyncVehiclePawn, isImplicit: true);
                MP.RegisterSyncWorker<CompFueledTravel>(SyncFueledTravelComp);
                // ITabs
                MP.RegisterSyncWorker<object>(NoSync, typeof(ITab_Vehicle_Cargo), shouldConstruct: true);
                // Turret
                MP.RegisterSyncWorker<Command_Turret>(SyncCommandTurret, typeof(Command_Turret), true, true);
                // Vehicle pawn elements
                MP.RegisterSyncWorker<VehicleComponent>(SyncVehicleComponent, isImplicit: true);
                MP.RegisterSyncWorker<VehicleTurret>(SyncVehicleTurret, isImplicit: true);
                MP.RegisterSyncWorker<VehicleIgnitionController>(SyncVehicleIgnitionController);
                MP.RegisterSyncWorker<VehicleRoleHandler>(SyncVehicleRoleHandler);
                // Caravan forming
                MP.RegisterSyncWorker<AssignedSeat>(SyncAssignedSeat);
            }

            #endregion

            #region Multiplayer

            {
                // Get the type of MpTransferableReference, as we'll have to initialize it in a few places. 
                mpTransferableReferenceType = AccessTools.TypeByName("Multiplayer.Client.Persistent.MpTransferableReference");
                // We could alternatively register our own ISyncField, but then we'd have to also make PostApply,
                // which would require referencing more stuff from MP itself. Seemed easier to just re-use the ready ISyncField.
                syncTradeableCount = (ISyncField)AccessTools.DeclaredField("Multiplayer.Client.SyncFields:SyncTradeableCount").GetValue(null);

                // Insert VehicleRoleHandler as supported thing holder for syncing.
                // The mod uses VehicleRoleHandler as IThingHolder and ends up being synced.
                // We should add support for adding more supported thing holders soon... I think the PokéWorld mod would benefit from it as well.
                const string supportedThingHoldersFieldPath = "Multiplayer.Client.RwSerialization:supportedThingHolders";
                var supportedThingHoldersField = AccessTools.DeclaredField(supportedThingHoldersFieldPath);
                if (supportedThingHoldersField == null)
                    Log.Error($"Trying to access {supportedThingHoldersFieldPath} failed, field is null.");
                else if (!supportedThingHoldersField.IsStatic)
                    Log.Error($"Trying to access {supportedThingHoldersFieldPath} failed, field is non-static.");
                else if (supportedThingHoldersField.GetValue(null) is not Type[] array)
                    Log.Error($"Trying to access {supportedThingHoldersFieldPath} failed, the value is null or not Type[]. Value={supportedThingHoldersField.GetValue(null)}");
                else
                {
                    // Increase size by 1
                    Array.Resize(ref array, array.Length + 1);
                    // Fill the last element
                    array[array.Length - 1] = typeof(VehicleRoleHandler);
                    // Set the original field to the value we set up
                    supportedThingHoldersField.SetValue(null, array);
                }
            }

            #endregion

            #endregion
        }

        #endregion

        #region Multithreading

        #region Disable multithreading

        // Stops specific threads from being created
        private static bool NoThreadInMp(VehiclePathingSystem mapping) => !MP.IsInMultiplayer && mapping.ThreadAvailable;

        private static IEnumerable<CodeInstruction> ReplaceThreadAvailable(IEnumerable<CodeInstruction> instr, MethodBase baseMethod)
        {
            var target = AccessTools.DeclaredPropertyGetter(typeof(VehiclePathingSystem), nameof(VehiclePathingSystem.ThreadAvailable));
            var replacement = AccessTools.DeclaredMethod(typeof(VehicleFramework), nameof(NoThreadInMp));
            var replacedAnything = false;

            foreach (var ci in instr)
            {
                if (ci.Calls(target))
                {
                    ci.opcode = OpCodes.Call;
                    ci.operand = replacement;
                    replacedAnything = true;
                }

                yield return ci;
            }

            if (!replacedAnything)
            {
                var name = (baseMethod.DeclaringType?.Namespace).NullOrEmpty() ? baseMethod.Name : $"{baseMethod.DeclaringType!.Name}:{baseMethod.Name}";
                Log.Warning($"Failed to patch {nameof(VehiclePathingSystem)}.{nameof(VehiclePathingSystem.ThreadAvailable)} calls for method {name}");
            }
        }

        #endregion

        #endregion

        #region Fuel toggle sync

        private static bool PreToggleFuelSwitch(Gizmo_RefuelableFuelTravel __instance)
        {
            if (!MP.IsInMultiplayer)
                return true;

            SyncedToggleFuelSwitch(fuelGizmoRefuelableField(__instance));
            return false;
        }

        private static void SyncedToggleFuelSwitch(CompFueledTravel comp)
        {
            if (comp.Props.ElectricPowered)
            {
                if (!comp.Charging)
                {
                    if (comp.TryConnectPower())
                        SoundDefOf.Tick_High.PlayOneShotOnCamera();
                    else
                        SoundDefOf.ClickReject.PlayOneShotOnCamera();
                }
                else
                {
                    comp.DisconnectPower();
                    SoundDefOf.Tick_Low.PlayOneShotOnCamera();
                }
            }
            else
            {
                comp.allowAutoRefuel = !comp.allowAutoRefuel;
                (comp.allowAutoRefuel ? SoundDefOf.Tick_High : SoundDefOf.Tick_Low).PlayOneShotOnCamera();
            }
        }

        #endregion

        #region Disembark pawn sync

        private static bool PreDisembarkSinglePawn(object __instance)
        {
            if (!MP.IsInMultiplayer)
                return true;

            // __instance is DisplayClass291_1 which has:
            //   currentPawn (Pawn) and CS$<>8__locals1 (DisplayClass291_0)
            // DisplayClass291_0 has <>4__this (VehiclePawn)
            var pawnField = __instance.GetType().GetField("currentPawn");
            var parentField = __instance.GetType().GetField("CS$<>8__locals1")
                           ?? __instance.GetType().GetField("CS$<>8__locals2");
            var parent = parentField?.GetValue(__instance);
            var vehicleField = parent?.GetType().GetField("<>4__this");

            if (vehicleField?.GetValue(parent) is VehiclePawn vehicle &&
                pawnField?.GetValue(__instance) is Pawn pawn)
            {
                SyncedDisembarkPawn(vehicle, pawn.thingIDNumber);
            }

            return false;
        }

        private static void SyncedDisembarkPawn(VehiclePawn vehicle, int pawnId)
        {
            // Find the pawn inside the vehicle's handlers
            foreach (var handler in vehicle.handlers)
            {
                foreach (var thing in handler.thingOwner.InnerListForReading)
                {
                    if (thing.thingIDNumber == pawnId && thing is Pawn pawn)
                    {
                        vehicle.DisembarkPawn(pawn);
                        return;
                    }
                }
            }
        }

        #endregion

        #region ITabs and WITabs

        private static bool PreHandleDragEvent()
        {
            if (!MP.IsInMultiplayer || MP.IsExecutingSyncCommand)
                return true;

            // If the method wasn't going to handle the event, just cancel the method execution
            if (Event.current.type != EventType.MouseUp || Event.current.button != 0)
                return false;

            // If the method was going to handle the event, we're going to sync it instead
            SyncedHandleDragEvent(VehicleTabHelper_Passenger.draggedPawn, VehicleTabHelper_Passenger.hoveringOverPawn, VehicleTabHelper_Passenger.transferToHolder);
            // If the event was handled, the dragged pawn is set to null.
            VehicleTabHelper_Passenger.draggedPawn = null;
            return false;
        }

        private static void SyncedHandleDragEvent(Pawn dragged, Pawn hovering, IThingHolder holder)
        {
            // Get current values as temporary values
            var currentDraggedPawn = VehicleTabHelper_Passenger.draggedPawn;
            var currentHoveringPawn = VehicleTabHelper_Passenger.hoveringOverPawn;
            var currentTransferToHolder = VehicleTabHelper_Passenger.transferToHolder;
            var currentEvent = Event.current;

            try
            {
                // Change fields to the synced values
                VehicleTabHelper_Passenger.draggedPawn = dragged;
                VehicleTabHelper_Passenger.hoveringOverPawn = hovering;
                VehicleTabHelper_Passenger.transferToHolder = holder;
                Event.current = new Event
                {
                    type = EventType.MouseUp,
                    button = 0,
                };
                // Run the original method with values we synced
                VehicleTabHelper_Passenger.HandleDragEvent();
            }
            finally
            {
                // Restore fields to their previous values
                VehicleTabHelper_Passenger.draggedPawn = currentDraggedPawn;
                VehicleTabHelper_Passenger.hoveringOverPawn = currentHoveringPawn;
                VehicleTabHelper_Passenger.transferToHolder = currentTransferToHolder;
                Event.current = currentEvent;
            }
        }

        private static void ReplacedShowBanishPawnConfirmationDialog(Pawn pawn, Action onConfirm, AerialVehicleInFlight aerialVehicle)
        {
            if (!MP.IsInMultiplayer)
            {
                PawnBanishUtility.ShowBanishPawnConfirmationDialog(pawn, onConfirm);
                return;
            }

            // onConfirm should be null, don't bother with it in MP
            if (onConfirm != null)
                Log.ErrorOnce($"onConfirm was not null for {nameof(PawnBanishUtility.ShowBanishPawnConfirmationDialog)}, MP Compat will likely need an update. There may be issues", -818484805);

            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                PawnBanishUtility.GetBanishPawnDialogText(pawn),
                () => SyncedBanishPawn(aerialVehicle, pawn.thingIDNumber),
                true));
        }

        private static void SyncedBanishPawn(AerialVehicleInFlight aerialVehicle, int pawnId)
        {
            if (aerialVehicle?.vehicle == null)
                return;

            var pawns = aerialVehicle.vehicle.AllPawnsAboard;
            if (pawns.NullOrEmpty())
                return;

            var pawn = pawns.Find(p => p.thingIDNumber == pawnId);
            if (pawn != null)
                PawnBanishUtility.Banish(pawn);
        }

        private static IEnumerable<CodeInstruction> ReplaceVanillaBanishDialog(IEnumerable<CodeInstruction> instr, MethodBase baseMethod)
        {
            var target = AccessTools.DeclaredMethod(typeof(PawnBanishUtility), nameof(PawnBanishUtility.ShowBanishPawnConfirmationDialog));
            var replacement = AccessTools.DeclaredMethod(typeof(VehicleFramework), nameof(ReplacedShowBanishPawnConfirmationDialog));
            var replacedCount = 0;

            foreach (var ci in instr)
            {
                if (ci.Calls(target))
                {
                    ci.opcode = OpCodes.Call;
                    ci.operand = replacement;

                    replacedCount++;

                    // Load the first arg (AerialVehicleInFlight) to the stack so our replacement method can access it
                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                }

                yield return ci;
            }

            const int expected = 1;
            if (replacedCount != expected)
            {
                var name = (baseMethod.DeclaringType?.Namespace).NullOrEmpty() ? baseMethod.Name : $"{baseMethod.DeclaringType!.Name}:{baseMethod.Name}";
                Log.Warning($"Patched incorrect number of PawnBanishUtility.ShowBanishPawnConfirmationDialog calls (patched {replacedCount}, expected {expected}) for method {name}");
            }
#if DEBUG
            else
            {
                var name = (baseMethod.DeclaringType?.Namespace).NullOrEmpty() ? baseMethod.Name : $"{baseMethod.DeclaringType!.Name}:{baseMethod.Name}";
                Log.Message($"Patched PawnBanishUtility.ShowBanishPawnConfirmationDialog calls (patched {replacedCount}, expected {expected}) for method {name}");
            }
#endif
        }

        #endregion

        #region Turrets

        // In almost every situation that `SetTarget` is called, we want to cancel it in interface.
        // This is due to the `SetTarget` being called with intent to make the turret start following
        // the current mouse position, which we don't want, and it's a feature we've disabled in MP.
        // There's however only 1 situation that it's not the case, this will handle it.
        // The situation is pressing the gizmo's cancel button to stop targetting att all.
        private static bool CancelTurretSetTargetSync() => shouldSyncInInterface || !MP.InInterface;

        private static void SyncSetTarget(VehicleTurret turret, LocalTargetInfo target)
        {
            try
            {
                shouldSyncInInterface = true;
                turret.SetTarget(target);
            }
            finally
            {
                shouldSyncInInterface = false;
            }
        }

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

        // Normally, a turret would start warmup once it starts pointing at the target.
        // In MP, as we cancel the set target earlier when starting targetting, it
        // doesn't properly reset the target and the vehicle thinks it's already targetting
        // and starts the warmup instantly. This should fix that issue by forcing the turret
        // to recalculate the position it should be aiming at before it starts aiming.
        private static void PreSetTurretTarget(VehicleTurret ___turret)
        {
            if (!MP.IsInMultiplayer)
                return;

            // Only needed for the first shot. All the other ones would be fine without it.
            ___turret.SetTarget(LocalTargetInfo.Invalid);
            // Actually force the turret to recalculate stuff.
            ___turret.AlignToAngleRestricted(0f);
        }

        #endregion

        #region Flying Vehicles

        private static bool PreLaunch(CompVehicleLauncher __instance,
            SmashTools.Targeting.TargetData<GlobalTargetInfo> targetData, IArrivalAction arrivalAction)
        {
            if (!MP.IsInMultiplayer || MP.IsExecutingSyncCommand)
                return true;

            ExtractTargetData(targetData.targets, out var tileIds, out var layerIds, out var worldObjectIds);
            SyncedLaunch(__instance.Vehicle, tileIds, layerIds, worldObjectIds, (VehicleArrivalAction)arrivalAction);
            return false;
        }

        private static void SyncedLaunch(VehiclePawn vehicle, List<int> tileIds, List<int> layerIds, List<int> worldObjectIds, VehicleArrivalAction arrivalAction)
        {
            if (arrivalAction != null)
                arrivalActionVehicleField(arrivalAction) = vehicle;
            vehicle.CompVehicleLauncher.Launch(ReconstructTargetData(tileIds, layerIds, worldObjectIds), arrivalAction);
        }

        private static bool PreCaravanLaunch(VehicleCaravan __instance,
            SmashTools.Targeting.TargetData<GlobalTargetInfo> targetData, IArrivalAction arrivalAction)
        {
            if (!MP.IsInMultiplayer || MP.IsExecutingSyncCommand)
                return true;

            ExtractTargetData(targetData.targets, out var tileIds, out var layerIds, out var worldObjectIds);
            SyncedCaravanLaunch(__instance, tileIds, layerIds, worldObjectIds, (VehicleArrivalAction)arrivalAction);
            return false;
        }

        private static void SyncedCaravanLaunch(VehicleCaravan caravan, List<int> tileIds, List<int> layerIds, List<int> worldObjectIds, VehicleArrivalAction arrivalAction)
        {
            if (arrivalAction != null)
                arrivalActionVehicleField(arrivalAction) = caravan.LeadVehicle;
            caravan.Launch(ReconstructTargetData(tileIds, layerIds, worldObjectIds), arrivalAction);
        }

        private static bool PreStartTargetingLocalMap(VehiclePawn vehicle,
            SmashTools.Targeting.TargetData<GlobalTargetInfo> targetData,
            MapParent mapParent, LocalTargetInfo landingCell, Rot4 rot)
        {
            if (!MP.IsInMultiplayer || MP.IsExecutingSyncCommand)
                return true;

            // Sync the entire StartTargetingLocalMap call atomically for both spawned
            // and non-spawned cases. The arrival action (ArrivalAction_LandToCell) needs
            // mapParent/landingCell/rot which SyncVehicleArrivalAction can't serialize.
            ExtractTargetData(targetData.targets, out var tileIds, out var layerIds, out var worldObjectIds);
            SyncedWorldVehicleFlyToMap(vehicle, tileIds, layerIds, worldObjectIds, mapParent, landingCell.Cell, rot);
            return false;
        }

        private static void SyncedWorldVehicleFlyToMap(VehiclePawn vehicle, List<int> tileIds, List<int> layerIds, List<int> worldObjectIds, MapParent mapParent, IntVec3 landingCell, Rot4 rot)
        {
            LaunchProtocol.StartTargetingLocalMap(vehicle, ReconstructTargetData(tileIds, layerIds, worldObjectIds), mapParent, landingCell, rot);
        }

        private static void PostSkyfallerLeavingExposeData(VehicleSkyfaller_Leaving __instance)
        {
            // VF doesn't save arrivalAction in ExposeData. In MP, the game may save
            // between skyfaller creation and LeaveMap, losing the arrival action.
            Scribe_Deep.Look(ref __instance.arrivalAction, "arrivalAction");
        }

        private static void PreLaunchSetArrivalVehicle(CompVehicleLauncher __instance, IArrivalAction arrivalAction)
        {
            if (MP.IsInMultiplayer && arrivalAction is VehicleArrivalAction vehicleAction)
                arrivalActionVehicleField(vehicleAction) = __instance.Vehicle;
        }

        private static void PreOrderFlySetArrivalVehicle(AerialVehicleInFlight __instance, IArrivalAction arrivalAction)
        {
            if (MP.IsInMultiplayer && arrivalAction is VehicleArrivalAction vehicleAction)
                arrivalActionVehicleField(vehicleAction) = __instance.vehicle;
        }

        private static bool PreOrderFlyToTiles(AerialVehicleInFlight __instance,
            List<FlightNode> flightPath, IArrivalAction arrivalAction)
        {
            // Only intercept when called from UI (InInterface). OrderFlyToTiles is also
            // called by VehicleSkyfaller_Leaving.LeaveMap during normal ticks — that must
            // not be intercepted or the aerial vehicle never gets its flight path.
            if (!MP.IsInMultiplayer || !MP.InInterface)
                return true;

            SyncedOrderFlyToTiles(__instance, flightPath, (VehicleArrivalAction)arrivalAction);
            return false;
        }

        private static void SyncedOrderFlyToTiles(AerialVehicleInFlight aerialVehicle,
            List<FlightNode> flightPath, VehicleArrivalAction arrivalAction)
        {
            if (arrivalAction != null)
                arrivalActionVehicleField(arrivalAction) = aerialVehicle.vehicle;
            aerialVehicle.OrderFlyToTiles(flightPath, arrivalAction);
        }

        private static void SyncIArrivalAction(SyncWorker sync, ref IArrivalAction action)
        {
            var vehicleAction = action as VehicleArrivalAction;
            SyncVehicleArrivalAction(sync, ref vehicleAction);
            action = vehicleAction;
        }

        private static readonly AccessTools.FieldRef<VehicleArrivalAction, VehiclePawn> arrivalActionVehicleField
            = AccessTools.FieldRefAccess<VehicleArrivalAction, VehiclePawn>("vehicle");

        /// <summary>
        /// Extract GlobalTargetInfo targets as plain int lists to avoid MP's broken
        /// GlobalTargetInfo serializer (writes PlanetTile as 8 bytes, reads as 4).
        /// </summary>
        private static void ExtractTargetData(IEnumerable<GlobalTargetInfo> targets,
            out List<int> tileIds, out List<int> layerIds, out List<int> worldObjectIds)
        {
            tileIds = [];
            layerIds = [];
            worldObjectIds = [];
            if (targets == null) return;
            foreach (var target in targets)
            {
                tileIds.Add(target.Tile.tileId);
                layerIds.Add(target.Tile.Layer.LayerID);
                worldObjectIds.Add(target.HasWorldObject ? target.WorldObject.ID : -1);
            }
        }

        /// <summary>Reconstruct TargetData from serialized int lists.</summary>
        private static SmashTools.Targeting.TargetData<GlobalTargetInfo> ReconstructTargetData(
            List<int> tileIds, List<int> layerIds, List<int> worldObjectIds)
        {
            var targetData = new SmashTools.Targeting.TargetData<GlobalTargetInfo>();
            for (var i = 0; i < tileIds.Count; i++)
            {
                var woId = worldObjectIds[i];
                if (woId >= 0)
                {
                    var wo = Find.WorldObjects.AllWorldObjects.FirstOrDefault(o => o.ID == woId);
                    if (wo != null)
                    {
                        targetData.targets.Add(new GlobalTargetInfo(wo));
                        continue;
                    }
                }
                targetData.targets.Add(new GlobalTargetInfo(new PlanetTile(tileIds[i], layerIds[i])));
            }
            return targetData;
        }

        private static void SyncVehicleArrivalAction(SyncWorker sync, ref VehicleArrivalAction action)
        {
            if (sync.isWriting)
            {
                var isNull = action == null;
                sync.Write(isNull);
                if (!isNull)
                {
                    sync.Write(action.GetType().FullName);
                    // Write vehicle thingIDNumber — can't use sync.Write<VehiclePawn> because
                    // the vehicle may be in a transitional state (inside skyfaller).
                    // We'll resolve it from the comp's vehicle during Launch execution.
                    var vehicle = arrivalActionVehicleField(action);
                    sync.Write(vehicle?.thingIDNumber ?? -1);
                }
            }
            else
            {
                var isNull = sync.Read<bool>();
                if (isNull)
                    return;

                var typeName = sync.Read<string>();
                var vehicleId = sync.Read<int>();

                if (string.IsNullOrEmpty(typeName))
                    return;

                var type = AccessTools.TypeByName(typeName);
                if (type == null)
                    return;

                action = (VehicleArrivalAction)Activator.CreateInstance(type);
                // Vehicle will be resolved when Launch executes — the CompVehicleLauncher
                // target is synced separately and has the correct vehicle reference.
                // Store the ID for now; the Launch method sets it via the comp's Vehicle.
            }
        }

        private static void PreMoveForwardFixArrivalVehicle(AerialVehicleInFlight __instance)
        {
            if (!MP.IsInMultiplayer)
                return;

            // Ensure the arrival action's vehicle reference is set before ConsumeNode
            // calls Arrived. The vehicle Scribe_Reference may fail to resolve after
            // MP save/load because the vehicle is inside the aerial vehicle's container.
            if (__instance.flightPath?.ArrivalAction is VehicleArrivalAction action
                && arrivalActionVehicleField(action) == null
                && __instance.vehicle != null)
            {
                arrivalActionVehicleField(action) = __instance.vehicle;
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

        private static void CleanupDestroyedCaravans(VehicleCaravan __instance)
        {
            // If we launched a VehicleCaravan, it won't deselect it (as the sync context was world selected).
            // We need to do it manually. Specifically from GetInspectString, as it seems like the safest
            // place to do so.
            if (__instance.Destroyed)
            {
                foreach (var vehicle in __instance.Vehicles)
                {
                    var aerialVehicle = vehicle.GetAerialVehicle();
                    if (aerialVehicle != null)
                        Find.WorldSelector.Select(aerialVehicle);
                }

                Find.WorldSelector.Deselect(__instance);
            }
        }

        #endregion

        #region SyncWorkers

        private static void NoSync(SyncWorker sync, ref object target)
        {
        }

        private static void SyncVehiclePawn(SyncWorker sync, ref VehiclePawn vehicle)
        {
            if (sync.isWriting)
            {
                if (vehicle == null)
                {
                    sync.Write(byte.MaxValue);
                    return;
                }

                var aerialVehicle = vehicle.GetAerialVehicle();
                if (aerialVehicle == null)
                {
                    // The first possible scenario when syncing - the vehicle exists as normal, and sync it as such.
                    sync.Write((byte)0);
                    sync.Write<Pawn>(vehicle);
                }
                else
                {
                    // The second possible scenario when syncing - the vehicle exists as a world object, which needs
                    // to be synced instead of the vehicle itself.
                    // The vehicle apparently specifies the world object as its parent holder, but the world object
                    // only returns the list of vehicle's cargo (instead of the vehicle itself), which causes MP to
                    // fail syncing the vehicle in a situation like that.
                    // Also write the thingId as fallback — the AerialVehicleInFlight may be destroyed
                    // by arrival before this command executes.
                    sync.Write((byte)1);
                    sync.Write(aerialVehicle);
                    sync.Write(vehicle.thingIDNumber);
                }
            }
            else
            {
                var type = sync.Read<byte>();
                switch (type)
                {
                    case 0:
                    {
                        vehicle = sync.Read<Pawn>() as VehiclePawn;
                        break;
                    }
                    case 1:
                    {
                        var vehicleInFlight = sync.Read<AerialVehicleInFlight>();
                        var thingId = sync.Read<int>();
                        vehicle = vehicleInFlight?.vehicle;

                        // Fallback: if the AerialVehicleInFlight was destroyed (vehicle arrived),
                        // try to find the vehicle by thingId — it may now be spawned on a map.
                        if (vehicle == null && MP.TryGetThingById(thingId, out var thing))
                            vehicle = thing as VehiclePawn;

                        break;
                    }
                    case byte.MaxValue:
                        break;
                    default:
                        throw new Exception($"Trying to read {nameof(VehiclePawn)}, but received an unsupported holder type ({type})");
                }
            }
        }

        private static void SyncFueledTravelComp(SyncWorker sync, ref CompFueledTravel fueledTravel)
        {
            if (sync.isWriting)
                sync.Write(fueledTravel.parent as VehiclePawn);
            else
                fueledTravel = sync.Read<VehiclePawn>()?.CompFueledTravel;
        }

        private static void SyncVehicleComponent(SyncWorker sync, ref VehicleComponent comp)
        {
            if (sync.isWriting)
            {
                var vehicle = comp.vehicle;
                var comps = vehicle.statHandler.components;

                var compIndex = comps.IndexOf(comp);
                sync.Write(compIndex);
                if (compIndex >= 0)
                    sync.Write(vehicle);
                else
                    Log.Error($"Trying to write a VehicleComponent, but the vehicle this component belongs to does not contain it. Vehicle={vehicle}, compCount={comps.Count}");
            }
            else
            {
                var compIndex = sync.Read<int>();

                if (compIndex >= 0)
                {
                    var vehicle = sync.Read<VehiclePawn>();
                    var compList = vehicle.statHandler.components;

                    if (compIndex < compList.Count)
                        comp = compList[compIndex];
                    else
                        Log.Error($"Trying to read VehicleComponent, but we've received component with index out of range. Vehicle={vehicle}, index={compIndex}, compCount={compList.Count}");
                }
            }
        }

        private static void SyncVehicleTurret(SyncWorker sync, ref VehicleTurret turret)
        {
            if (sync.isWriting)
            {
                sync.Write(turret.uniqueID);
                sync.Write(turret.vehicle);
            }
            else
            {
                var id = sync.Read<int>();
                var vehiclePawn = sync.Read<VehiclePawn>();

                switch (id)
                {
                    case -1:
                        Log.Error($"Trying to read VehicleTurret, received an uninitialized turret. Vehicle={vehiclePawn}, id=-1");
                        return;
                    case < -1:
                        Log.Warning($"Trying to read VehicleTurret, received a turret with local ID. This shouldn't happen. Vehicle={vehiclePawn}, id={id}");
                        return;
                }

                if (vehiclePawn == null)
                {
                    Log.Error($"Trying to read VehicleTurret, received a null parent vehicle. id={id}");
                    return;
                }

                var comp = vehiclePawn.CompVehicleTurrets;
                if (vehiclePawn.CompVehicleTurrets == null)
                {
                    Log.Error($"Trying to read VehicleTurret, the vehicle doesn't contain CompVehicleTurrets. Vehicle={vehiclePawn}, id={id}");
                    return;
                }

                if (comp.turrets == null)
                {
                    Log.Error($"Trying to read VehicleTurret, but the CompVehicleTurrets contains a null list of turrets. Vehicle={vehiclePawn}, id={id}, comp={comp}");
                    return;
                }

                turret = comp.turrets.FirstOrDefault(t => t.uniqueID == id);
                if (turret == null)
                    Log.Error($"Trying to read VehicleTurret, but the list of turrets does not contain the turret we're trying to read. Vehicle={vehiclePawn}, id={id}, comp={comp}");
            }
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
                if (controller == null)
                    Log.Error($"Trying to read VehicleIgnitionController, but the vehicle is missing it. Vehicle={vehiclePawn}");
            }
        }

        private static void SyncVehicleRoleHandler(SyncWorker sync, ref VehicleRoleHandler handler)
        {
            if (sync.isWriting)
            {
                sync.Write(handler.uniqueID);
                sync.Write(handler.vehicle);
            }
            else
            {
                var id = sync.Read<int>();
                var vehiclePawn = sync.Read<VehiclePawn>();

                switch (id)
                {
                    case -1:
                        Log.Error($"Trying to read VehicleRoleHandler, received an uninitialized handler. Vehicle={vehiclePawn}, id=-1");
                        return;
                    case < -1:
                        Log.Warning($"Trying to read VehicleRoleHandler, received handler with local ID. This shouldn't happen. Vehicle={vehiclePawn}, id={id}");
                        return;
                }

                if (vehiclePawn == null)
                {
                    Log.Error($"Trying to read VehicleRoleHandler, received a null parent vehicle. id={id}");
                    return;
                }

                if (vehiclePawn.handlers == null)
                {
                    Log.Error($"Trying to read VehicleRoleHandler, but the vehicle contains a null list of handlers. Vehicle={vehiclePawn}, id={id}");
                    return;
                }

                handler = vehiclePawn.handlers.FirstOrDefault(t => t.uniqueID == id);
                if (handler == null)
                    Log.Error($"Trying to read VehicleRoleHandler, but the list of handlers does not contain the handler we're trying to read. Vehicle={vehiclePawn}, id={id}");
            }
        }

        private static void SyncCommandTurret(SyncWorker sync, ref Command_Turret command)
        {
            if (sync.isWriting)
            {
                // We could technically just sync the turret and use its vehicle field.
                // Syncing it anyway in case some mods do weird stuff with this.
                sync.Write(command.vehicle);

                // Not syncing other fields, as it doesn't seem they're needed.
            }
            else
            {
                // isImplicit: true
                // If any subclass ever introduces a constructor we'll need to replace it with call to `FormatterServices.GetUninitializedObject(type)`

                command.vehicle = sync.Read<VehiclePawn>();
            }

            SyncVehicleTurret(sync, ref command.turret);
        }

        // Used by caravan forming session
        private static void SyncAssignedSeat(SyncWorker sync, ref AssignedSeat seat)
        {
            if (sync.isWriting)
            {
                sync.Write(seat.handler);
            }
            else
            {
                var handler = sync.Read<VehicleRoleHandler>();
                seat = new AssignedSeat
                {
                    handler = handler,
                };
            }
        }

        // Needed by the load cargo dialog/session sync field
        private static void SyncVehicleSettings(SyncWorker sync, ref VehiclesModSettings settings)
        {
            if (!sync.isWriting)
                settings = VehicleMod.settings;
        }

        // Needed for flying vehicle syncing
        private static void SyncLaunchProtocol(SyncWorker sync, ref LaunchProtocol launchProtocol)
        {
            if (sync.isWriting)
                sync.Write(launchProtocol?.vehicle);
            else
                launchProtocol = sync.Read<VehiclePawn>()?.CompVehicleLauncher?.launchProtocol;
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
            // We need to sync the road type (prioritize/avoid) to properly sync the designator.
            // Sync the local player's road type, as the normal one may become overwritten by
            // this specific sync worker delegate.
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
                if (vehicle == null)
                {
                    Log.Error($"Trying to create {nameof(LoadVehicleCargoSession)} for a null vehicle.");
                    return;
                }

                if (vehicle.Map == null)
                {
                    Log.Error($"Trying to create {nameof(LoadVehicleCargoSession)} for a vehicle with null map. Vehicle={vehicle}");
                    return;
                }

                var manager = MP.GetLocalSessionManager(vehicle.Map);
                var session = manager.GetFirstOfType<LoadVehicleCargoSession>();
                if (session == null)
                {
                    session = new LoadVehicleCargoSession(vehicle.Map, vehicle);
                    if (!manager.AddSession(session))
                        session = null;
                }

                if (session == null)
                    Log.Error($"Couldn't get or create {nameof(LoadVehicleCargoSession)}");
                else if (MP.IsExecutingSyncCommandIssuedBySelf)
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
                // Initialize UI
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
            // If the setting to see all was selected, it resets the dialog transferables.
            // We need to make sure it's done to all dialogs, as it's a global setting.
            foreach (var map in Find.Maps)
                MP.GetLocalSessionManager(map).GetFirstOfType<LoadVehicleCargoSession>()?.AddItems();
        }

        private static bool ReplacedLoadCargoCancelButton(Rect rect, string label, bool drawBackground, bool doMouseoverSound, bool active, TextAnchor? overrideTextAnchor)
        {
            bool DoButton() => Widgets.ButtonText(rect, label, drawBackground, doMouseoverSound, active, overrideTextAnchor);

            if (LoadVehicleCargoSession.drawingSession == null)
                return DoButton();

            var color = GUI.color;
            try
            {
                // Red button like in MP
                GUI.color = new Color(1f, 0.3f, 0.35f);

                // If the button was pressed sync removing the dialog
                if (DoButton())
                    LoadVehicleCargoSession.drawingSession.Remove();
            }
            finally
            {
                GUI.color = color;
            }

            return false;
        }

        private static bool ReplacedLoadCargoResetButton(Rect rect, string label, bool drawBackground, bool doMouseoverSound, bool active, TextAnchor? overrideTextAnchor)
        {
            var result = Widgets.ButtonText(rect, label, drawBackground, doMouseoverSound, active, overrideTextAnchor);

            if (!result || LoadVehicleCargoSession.drawingSession == null)
                return result;

            LoadVehicleCargoSession.drawingSession.Reset();
            return false;
        }

        private static bool ReplacedLoadCargoAcceptButton(Rect rect, string label, bool drawBackground, bool doMouseoverSound, bool active, TextAnchor? overrideTextAnchor)
        {
            var result = Widgets.ButtonText(rect, label, drawBackground, doMouseoverSound, active, overrideTextAnchor);

            if (!result || LoadVehicleCargoSession.drawingSession == null)
                return result;

            LoadVehicleCargoSession.drawingSession.Accept();
            return false;
        }

        private static bool ReplacedLoadCargoPackInstantlyButton(Rect rect, string label, bool drawBackground, bool doMouseoverSound, bool active, TextAnchor? overrideTextAnchor)
        {
            var result = Widgets.ButtonText(rect, label, drawBackground, doMouseoverSound, active, overrideTextAnchor);

            if (!result || LoadVehicleCargoSession.drawingSession == null)
                return result;

            LoadVehicleCargoSession.drawingSession.PackInstantly();
            return false;
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

                        // If one vehicle, just start targeter for it
                        if (vehicles.Count == 1)
                            StartVehicleLandingTargeter(vehicles[0]);
                        // If multiple vehicles, open list of the ones waiting to land
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

            // The vehicle is not spawned, and it doesn't have a holder.
            // And even if we make this session class a IThingHolder, MP
            // doesn't include session classes as valid implementations.
            public void VehicleArrivalById(int vehicleId, LocalTargetInfo target, Rot4 rot)
            {
                var vehicle = vehicles.Find(v => v.thingIDNumber == vehicleId);
                if (vehicle == null)
                    return;

                if (vehicle.Spawned)
                {
                    vehicles.Remove(vehicle);
                    Log.Error($"{nameof(FlyingVehicleTargetedLandingSession)} contained a vehicle that it should no longer contain, sessionID={SessionId}, vehicle={vehicle}");
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

            // Should only ever be called during ticking, no need for sync methods here.
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
            // The map would get removed due to no active pawns. The mod would prevent the map removal
            // if the LandingTargeter was active, which in our patch - it isn't. It also checks active
            // pawns in skyfallers, which again - likely none are active at this point.
            if (MP.IsInMultiplayer && !__result)
                __result = MP.GetLocalSessionManager(___map).GetFirstOfType<FlyingVehicleTargetedLandingSession>() != null;
        }

        private static bool PreTargetedDropVehicleArrival(VehiclePawn vehicle, Map map)
        {
            if (!MP.IsInMultiplayer)
                return true;

            // Prevent the targeter from being started in MP, as we'll instead handle
            // this ourselves with the session we've made.
            FlyingVehicleTargetedLandingSession.HandleTargetedVehicleArrival(vehicle, map);
            return false;
        }

        #endregion

        #endregion

        #region Shared

        private static void CreateAndSyncMpTransferableReference(ISessionWithTransferables session, Transferable transferable)
            => syncTradeableCount.Watch(Activator.CreateInstance(mpTransferableReferenceType, session, transferable));

        // MP approach is to intercept `Widgets.ButtonTextWorker` call with a prefix/postfix call.
        // Our approach here is to replace the `Widgets.ButtonText` call itself with our intercepted one.
        private static IEnumerable<CodeInstruction> ReplaceButtonsTranspiler(IEnumerable<CodeInstruction> instr, MethodBase baseMethod)
        {
            var target = AccessTools.DeclaredMethod(typeof(Widgets), nameof(Widgets.ButtonText),
                [typeof(Rect), typeof(string), typeof(bool), typeof(bool), typeof(bool), typeof(TextAnchor?)]);
            var buttonReplacements = new Dictionary<string, MethodInfo>();
            int expected;

            if (baseMethod.DeclaringType == typeof(Dialog_LoadCargo))
            {
                expected = 4;
                buttonReplacements.Add("CancelButton", AccessTools.DeclaredMethod(typeof(VehicleFramework), nameof(ReplacedLoadCargoCancelButton)));
                buttonReplacements.Add("ResetButton", AccessTools.DeclaredMethod(typeof(VehicleFramework), nameof(ReplacedLoadCargoResetButton)));
                buttonReplacements.Add("AcceptButton", AccessTools.DeclaredMethod(typeof(VehicleFramework), nameof(ReplacedLoadCargoAcceptButton)));
                buttonReplacements.Add("Dev: Pack Instantly", AccessTools.DeclaredMethod(typeof(VehicleFramework), nameof(ReplacedLoadCargoPackInstantlyButton)));
            }
            else
                throw new Exception($"Trying to patch a method for unsupported type: {baseMethod.DeclaringType}");

            var replacedCount = 0;
            MethodInfo currentButtonReplacement = null;

            foreach (var ci in instr)
            {
                if (currentButtonReplacement != null)
                {
                    if (ci.Calls(target))
                    {
                        ci.operand = currentButtonReplacement;
                        currentButtonReplacement = null;
                        replacedCount++;
                    }
                }
                else if (ci.opcode == OpCodes.Ldstr && ci.operand is string text)
                {
                    buttonReplacements.TryGetValue(text, out currentButtonReplacement);
                }

                yield return ci;
            }

            if (replacedCount != expected)
            {
                var name = (baseMethod.DeclaringType?.Namespace).NullOrEmpty() ? baseMethod.Name : $"{baseMethod.DeclaringType!.Name}:{baseMethod.Name}";
                Log.Warning($"Patched incorrect number of Widgets.ButtonText calls (patched {replacedCount}, expected {expected}) for method {name}");
            }
#if DEBUG
            else
            {
                var name = (baseMethod.DeclaringType?.Namespace).NullOrEmpty() ? baseMethod.Name : $"{baseMethod.DeclaringType!.Name}:{baseMethod.Name}";
                Log.Message($"Patched Widgets.ButtonText calls (patched {replacedCount}, expected {expected}) for method {name}");
            }
#endif
        }

        private static void InsertSwitchToMap(Window __instance, Rect __0)
        {
            if (!MP.IsInMultiplayer)
                return;

            using (new TextBlock(GameFont.Tiny))
            {
                // TODO: Switch to the MP translation once it's included in the mod
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
            // Ignore out of MP, drawing will update angles and stuff
            if (!MP.IsInMultiplayer)
                return;

            // Likely not needed if the vehicle is not spawned
            if (!__instance.Spawned)
                return;

            // Make sure that the vehicle has turrets at all
            var turretsComp = __instance.CompVehicleTurrets;
            if (turretsComp == null)
                return;

            // This would normally be done during ticking or drawing for each turret inside
            // TurretRotation getter. However, calling it during drawing will cause issues,
            // and the turrets don't always tick, so we need to ensure this is updated when
            // the turret is not ticking, and it's done in a deterministic manner.
            foreach (var turret in turretsComp.turrets)
            {
                if (turret.IsTargetable || turret.attachedTo != null)
                {
                    turret.UpdateRotationLock();
                    turret.TurretRotation = Mathf.Repeat(turret.TurretRotation, 360f);
                }
            }
        }

        // DISABLED: TurretRotation getter prefix/postfix causes native crash on turret vehicle spawn.
        // The old approach saved/restored rotation around the getter to prevent drawing from
        // affecting game state. With the new VF API (transform.rotation), this causes issues —
        // likely recursive getter calls or uninitialized transform during spawn.
        // Need a different approach for turret rotation determinism.
        //
        // [MpCompatPrefix(typeof(VehicleTurret), nameof(VehicleTurret.TurretRotation), methodType: MethodType.Getter)]
        private static void PreTurretRotation(VehicleTurret __instance, ref float? __state)
        {
            if (MP.InInterface)
                __state = __instance.TurretRotation;
        }

        // [MpCompatPostfix(typeof(VehicleTurret), nameof(VehicleTurret.TurretRotation), methodType: MethodType.Getter)]
        private static void PostTurretRotation(VehicleTurret __instance, float? __state)
        {
            if (__state.HasValue)
                __instance.TurretRotation = __state.Value;
        }

        [MpCompatPrefix(typeof(TurretTargeter), nameof(TurretTargeter.Turret), methodType: MethodType.Getter)]
        private static bool PreTurretTargeterCurrentTurretGetter()
        {
            return !MP.IsInMultiplayer || MP.InInterface;
        }

        [MpCompatPrefix(typeof(VehicleTweener), nameof(VehicleTweener.TweenedPos), methodType: MethodType.Getter)]
        private static bool PreTweenedPosGetter(VehicleTweener __instance, ref Vector3 __result)
        {
            // Out of MP or in interface, return the tweened pos
            if (!MP.IsInMultiplayer || MP.InInterface)
                return true;

            // Give the root position during ticking, same as MP does with pawns.
            __result = __instance.TweenedPosRoot();
            return false;
        }

        // VehiclePawn.DrawAt uses `in Vector3` which MpCompatPrefix can't match by type array.
        // Patched manually in LatePatch instead.
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
            // If in MP and not executing synced commands, restore the current player's road type.
            // It may become overwritten by a different value when syncing.
            if (MP.IsInMultiplayer && !MP.IsExecutingSyncCommand)
                Designator_AreaRoad.roadType = localRoadType;
        }

        #endregion

        #region Upgrade Fixes

        private static bool ShouldExecuteWhenFinished()
        {
            // If not on main thread we always need to
            // execute when finished, both in MP and in SP.
            if (!UnityData.IsInMainThread)
                return false;
            // If main thread and not in MP, leave current behavior.
            if (!MP.IsInMultiplayer)
                return true;

            // If in MP and on main thread, ensure that we call it in
            // ExecuteWhenFinished during game loading as it's
            // unsafe there and will cause errors for the host.
            // AllowedToRunLongEvents is false only for host during
            // loading
            return PatchingUtilities.AllowedToRunLongEvents;
        }

        [MpCompatTranspiler(typeof(UpgradeNode), nameof(UpgradeNode.AddOverlays))]
        private static IEnumerable<CodeInstruction> FixHostOverlayInit(IEnumerable<CodeInstruction> instr, MethodBase baseMethod)
        {
            // The mod fails to initialize the overlays for the host when (re)loading the game.
            // It fails to initialize the second time as the initialization method is not called
            // inside "LongEventHandler.ExecuteWhenFinished" call (this only happens when not on
            // the main thread, the code checks UnityData.IsInMainThread). The issue is that likely
            // due to the way MP handles loading some of the data required for overlay initialization
            // is not yet initialized. We need to ensure that the execution is delayed until it's
            // safe to initialize them.

            var target = AccessTools.DeclaredPropertyGetter(typeof(UnityData), nameof(UnityData.IsInMainThread));
            var replacement = MpMethodUtil.MethodOf(ShouldExecuteWhenFinished);

            return instr.ReplaceMethod(target, replacement, baseMethod, expectedReplacements: 1);
        }

        #endregion
    }
}