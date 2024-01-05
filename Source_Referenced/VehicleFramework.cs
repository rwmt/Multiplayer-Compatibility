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
        private static List<FlightNode> tempFlightPath = null;

        // VehiclesModSettings
        private static ISyncField showAllCargoItemsField;

        // VehiclePawn.<>c__DisplayClass250_0
        private static AccessTools.FieldRef<object, VehiclePawn> vehiclePawnInnerClassParentField;

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
                    AccessTools.DeclaredMethod(typeof(Vehicle_PathFollower), nameof(Vehicle_PathFollower.TrySetNewPath_Threaded)),
                };
                var transpiler = new HarmonyMethod(typeof(VehicleFramework), nameof(ReplaceThreadAvailable));
                foreach (var m in methods.Where(m => m != null))
                    MpCompat.harmony.Patch(m, transpiler: transpiler);

                // Disable an issue where a lot of red regions pop-up for 50 ticks when hosting,
                // or when the `PathingHelper.ThingAffectingRegionsStateChange` method is called for things that spawned.
                MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(VehicleRegionDirtyer), nameof(VehicleRegionDirtyer.Notify_ThingAffectingRegionsSpawned)),
                    transpiler: new HarmonyMethod(typeof(VehicleFramework), nameof(DisableDebugFlashing)));

                // // Slightly replace how pathfinding is handled by the mod.
                // // Currently, the vehicle will wait before moving for as long as the path is being calculated.
                // // We need to make sure it's ready on the same tick for all players, as otherwise a desync will happen.
                // MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(Vehicle_PathFollower), nameof(Vehicle_PathFollower.PatherTick)),
                //     prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(PrePathTicker)));
                // MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(Vehicle_PathFollower), nameof(Vehicle_PathFollower.TrySetNewPath_Delayed)),
                //     prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(PreThreadedTrySetNewPath)));

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

                // Cancel designator (0), toggle fishing (3), haul pawn to vehicle (5), disembark all pawns (9),
                // disembark singe pawn (10), force leave caravan (11), cancel forming caravan (12)
                // Load cargo (1) opens a dialog, which we need to handle instead.
                MpCompat.RegisterLambdaDelegate(typeof(VehiclePawn), nameof(VehiclePawn.GetGizmos), 0, 3, 5, 9, 10, 11, 12);
                // Dev mode: Destroy component (14), damage component (16), explode component(18), heal all components (19),
                // recache all stats (21), give random pawn mental state (22), kill random pawn (23)
                // Flash OccupiedRect (24) - most likely pointless syncing it.
                MpCompat.RegisterLambdaDelegate(typeof(VehiclePawn), nameof(VehiclePawn.GetGizmos), 14, 16, 18, 19, 21, 22, 23).SetDebugOnly();

                // Toggle drafted or (if moving) engage brakes.
                // Alternative approach - sync both Vehicle_IgnitionController:set_Drafted and Vehicle_PathFollower:EngageBrakes,
                // but it would require making a sync worker for Vehicle_PathFollower.
                MpCompat.RegisterLambdaMethod(typeof(Vehicle_IgnitionController), nameof(Vehicle_IgnitionController.GetGizmos), 1);

                // Comps

                // Target fuel level setter, used from Gizmo_RefuelableFuelTravel
                MP.RegisterSyncMethod(typeof(CompFueledTravel), nameof(CompFueledTravel.TargetFuelLevel));
                // Refuel from inventory, used from Gizmo_RefuelableFuelTravel
                MP.RegisterSyncMethod(typeof(CompFueledTravel), nameof(CompFueledTravel.Refuel), new SyncType[] { typeof(List<Thing>) });
                MP.RegisterSyncMethod(typeof(CompFueledTravel), nameof(CompFueledTravel.Refuel), new SyncType[] { typeof(float) });
                // Toggle connect/disconnect from power for electric vehicles
                MpCompat.RegisterLambdaMethod(typeof(CompFueledTravel), nameof(CompFueledTravel.CompGetGizmosExtra), 1);
                // (Dev) set fuel to 0/0.1/half/max
                MpCompat.RegisterLambdaMethod(typeof(CompFueledTravel), nameof(CompFueledTravel.DevModeGizmos), 0, 1, 2, 3).SetDebugOnly();
                // (Dev) set fuel to 0/max
                MpCompat.RegisterLambdaMethod(typeof(CompFueledTravel), nameof(CompFueledTravel.CompCaravanGizmos), 0, 1).SetDebugOnly();

                MP.RegisterSyncMethod(typeof(CompVehicleTurrets), nameof(CompVehicleTurrets.SetQuotaLevel));
                // Deploy turret
                MpCompat.RegisterLambdaMethod(typeof(CompVehicleTurrets), nameof(CompVehicleTurrets.CompGetGizmosExtra), 0);
                // (Dev) full reload turret/cannon (4/6)
                MpCompat.RegisterLambdaDelegate(typeof(CompVehicleTurrets), nameof(CompVehicleTurrets.CompGetGizmosExtra), 4, 6).SetDebugOnly();


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

                // Targetale turrets
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

                    TrySyncDeclaredMethod(subclass, nameof(VehicleTurret.TryRemoveShell));
                    TrySyncDeclaredMethod(subclass, nameof(VehicleTurret.ReloadCannon));
                    TrySyncDeclaredMethod(subclass, nameof(VehicleTurret.SwitchAutoTarget));
                }

                // Stop the call from interface, called from TurretRotation getter. We update it during ticking.
                PatchingUtilities.PatchCancelInInterface(AccessTools.DeclaredMethod(typeof(VehicleTurret), nameof(VehicleTurret.ValidateLockStatus)));
            }

            #endregion

            #region Float Menus

            {
                // Enter vehicle. Can't sync through TryTakeOrderedJob, as the method does a bit more stuff.
                MpCompat.RegisterLambdaDelegate(typeof(VehiclePawn), nameof(VehiclePawn.GetFloatMenuOptions), 0);
                MpCompat.RegisterLambdaDelegate(typeof(VehiclePawn), nameof(VehiclePawn.MultiplePawnFloatMenuOptions), 0);
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
                // Vehicles.CompFueledTravel:DrawMotes - most likely not needed, RNG calls before GenView.ShouldSpawnMotesAt
            }

            #endregion

            #region Dialogs

            {
                #region Other

                // Called when accepted from change color dialog
                MpCompat.RegisterLambdaMethod("Vehicles.VehiclePawn", "ChangeColor", 0);

                #endregion

                #region Form vehicle caravan

                // Sync creation of session
                MP.RegisterSyncMethod(typeof(FormVehicleCaravanSession), nameof(FormVehicleCaravanSession.CreateFormVehicleCaravanSession));
                // Sync caravan forming session methods
                MP.RegisterSyncMethod(typeof(FormVehicleCaravanSession), nameof(FormVehicleCaravanSession.ChooseRoute));
                MP.RegisterSyncMethod(typeof(FormVehicleCaravanSession), nameof(FormVehicleCaravanSession.TryReformCaravan));
                MP.RegisterSyncMethod(typeof(FormVehicleCaravanSession), nameof(FormVehicleCaravanSession.TryFormAndSendCaravan));
                MP.RegisterSyncMethod(typeof(FormVehicleCaravanSession), nameof(FormVehicleCaravanSession.DebugTryFormCaravanInstantly)).SetDebugOnly();
                MP.RegisterSyncMethod(typeof(FormVehicleCaravanSession), nameof(FormVehicleCaravanSession.Reset));
                MP.RegisterSyncMethod(typeof(FormVehicleCaravanSession), nameof(FormVehicleCaravanSession.Remove));
                MP.RegisterSyncMethod(typeof(FormVehicleCaravanSession), nameof(FormVehicleCaravanSession.SetAssignedSeats));

                // Capture drawing so we can tie the dialog to the session and set the correct current session with transferables.
                // Mp prefers making a subclass of the session itself for it, we're doing it by patching it to avoid making extra classes.
                MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(Dialog_FormVehicleCaravan), nameof(Dialog_FormVehicleCaravan.DoWindowContents)),
                    prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(PreDrawFormVehicleCaravan)),
                    finalizer: new HarmonyMethod(typeof(VehicleFramework), nameof(FinalizeDrawFormVehicleCaravan)));

                // Replace the `Widgets.ButtonText` for cancel and reset buttons with our own to handle MP-specific stuff.
                MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(Dialog_FormVehicleCaravan), nameof(Dialog_FormVehicleCaravan.DoBottomButtons)),
                    transpiler: new HarmonyMethod(typeof(VehicleFramework), nameof(ReplaceButtonsTranspiler)));

                // Catch the selection of new route and sync it
                MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(Dialog_FormVehicleCaravan), nameof(Dialog_FormVehicleCaravan.Notify_ChoseRoute)),
                    prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(PreNotifyChoseRoute)));
                // Catch (potentially dev) (re)forming the caravan and sync it
                MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(Dialog_FormVehicleCaravan), nameof(Dialog_FormVehicleCaravan.TryReformCaravan)),
                    prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(PreTryReformCaravan)));
                MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(Dialog_FormVehicleCaravan), nameof(Dialog_FormVehicleCaravan.TryFormAndSendCaravan)),
                    prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(PreTryFormAndSendCaravan)));
                MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(Dialog_FormVehicleCaravan), nameof(Dialog_FormVehicleCaravan.DebugTryFormCaravanInstantly)),
                    prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(PreDebugTryFormCaravanInstantly)));

                // Catch (re)form caravan dialog gizmo to open session dialog tied to it or create session if there's none
                MpCompat.harmony.Patch(MpMethodUtil.GetLambda(typeof(Vehicles.Gizmos), nameof(Vehicles.Gizmos.AddVehicleGizmosPassthrough), lambdaOrdinal: 0),
                    prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(PreFormCaravanDialog)));
                MpCompat.harmony.Patch(MpMethodUtil.GetLambda(typeof(Vehicles.Gizmos), nameof(Vehicles.Gizmos.AddVehicleGizmosPassthrough), lambdaOrdinal: 1),
                    prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(PreReformCaravanDialog)));

                // Catch changes to the pawns assigned seats as well as pawn transferable count (won't cover Dialog_AssignSeats)
                MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(TransferableVehicleWidget), nameof(TransferableVehicleWidget.FillMainRect)),
                    prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(PreTransferableVehicleWidgetMainRect)),
                    postfix: new HarmonyMethod(typeof(VehicleFramework), nameof(PostTransferableVehicleWidgetMainRect)));
                // Catch changes to the vehicle transferable count (won't cover Dialog_AssignSeats)
                MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(UIHelper), nameof(UIHelper.DoCountAdjustInterface)),
                    prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(PreDoCountAdjustInterface)));

                // Set the CaravanHelper.assignedSeats so the dialog can be set up correctly
                // MpCompat.harmony.Patch(AccessTools.DeclaredConstructor(typeof(Dialog_AssignSeats), new[] { typeof(List<TransferableOneWay>), typeof(TransferableOneWay) }),
                //     prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(PreAssignSeatsCtor)),
                //     finalizer: new HarmonyMethod(typeof(VehicleFramework), nameof(FinalizeAssignSeatsCtor)));
                // Firstly, close the dialog if the related parent dialog or session are gone.
                // Second, setup the session as active so we can work on it.
                // Thirdly, update the list of available pawns as another player may have assigned them to a different vehicle.
                MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(Dialog_AssignSeats), nameof(Dialog_AssignSeats.DoWindowContents)),
                    prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(PreDrawAssignSeats)),
                    finalizer: new HarmonyMethod(typeof(VehicleFramework), nameof(FinalizeDrawAssignSeats)));
                // Set the CaravanHelper.assignedSeats and watch for changes in it, as well as transferables
                MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(Dialog_AssignSeats), nameof(Dialog_AssignSeats.FinalizeSeats)),
                    prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(PreFinalizeAssignSeats)),
                    finalizer: new HarmonyMethod(typeof(VehicleFramework), nameof(FinalizeFinalizeAssignSeats)));

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
                MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(Dialog_LoadCargo), nameof(Dialog_LoadCargo.SetToSendEverything)),
                    prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(PreLoadCargoSetToSendEverything)));

                // Replace the `Widgets.ButtonText` for several buttons with our own to handle MP-specific stuff.
                MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(Dialog_LoadCargo), nameof(Dialog_LoadCargo.BottomButtons)),
                    transpiler: new HarmonyMethod(typeof(VehicleFramework), nameof(ReplaceButtonsTranspiler)));

                // Catch load cargo dialog gizmo to open session dialog tied to it or create session if there's none
                method = MpMethodUtil.GetLambda(typeof(VehiclePawn), nameof(VehiclePawn.GetGizmos), lambdaOrdinal: 1);
                vehiclePawnInnerClassParentField = AccessTools.FieldRefAccess<VehiclePawn>(method.DeclaringType, "<>4__this");
                MpCompat.harmony.Patch(method, prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(PreLoadCargoDialog)));

                // Prevent the call in MP, as it'll mess with transferables if re-opening the window.
                MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(Dialog_LoadCargo), nameof(Dialog_LoadCargo.CalculateAndRecacheTransferables)),
                    prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(PreLoadCargoCalculateAndRecache)));

                #endregion

                #region Shared

                // Insert "Switch to map" button to the dialogs with session
                var types = new[]
                {
                    typeof(Dialog_FormVehicleCaravan),
                    typeof(Dialog_LoadCargo),
                };

                foreach (var type in types)
                {
                    MpCompat.harmony.Patch(
                        AccessTools.DeclaredMethod(type, nameof(Window.DoWindowContents), new[] { typeof(Rect) }),
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
                    TrySyncDeclaredMethod(type, nameof(ITab_Airdrop_Container.InterfaceDrop))?.SetContext(SyncContext.MapSelected);
                    TrySyncDeclaredMethod(type, nameof(ITab_Airdrop_Container.InterfaceDropAll))?.SetContext(SyncContext.MapSelected);
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
            }

            #endregion

            #region Flying vehicles

            {
                // To start off, there needs to be some explanation here.
                // In a lot of situations we need to sync additional data on top of
                // what a sync method normally does - namely, the flight path (list, static property).
                // We can't really make a sync worker for every context where it's used, and
                // there were a couple of issues of making sync workers that would sync the flight
                // path data as well.
                // As a workaround came GetFlightPathSerializer<T> method - it'll return a serializer
                // (to be used with sync transformers) to sync the object itself that was synced,
                // as-is (with no changes), along with the flight path itself. Doesn't work with
                // sync delegates and TransformTarget, so in those cases it requires transforming
                // an argument or a field.

                MP.RegisterSyncWorker<LaunchProtocol>(SyncLaunchProtocol, isImplicit: true);
                MP.RegisterSyncWorker<FlightNode>(SyncFlightNode);

                // Deselect destroyed vehicle caravans and pick their respective aerial vehicles (if there are any).
                MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(VehicleCaravan), nameof(VehicleCaravan.GetInspectString)),
                    prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(CleanupDestroyedCaravans)));

                // Settle with an aerial vehicle (from AerialVehicleInFlight gizmos)
                MP.RegisterSyncMethod(typeof(SettlementVehicleUtility), nameof(SettlementVehicleUtility.Settle))
                    .CancelIfAnyArgNull();
                // (Debug) land at nearest player settlement (6), initiate crash event (7)
                MpCompat.RegisterLambdaMethod(typeof(AerialVehicleInFlight), nameof(AerialVehicleInFlight.GetGizmos), 6, 7)
                    .SetDebugOnly();

                // Launch from gizmo
                // CompVehicleLauncher/LaunchProtocol
                MP.RegisterSyncMethod(typeof(CompVehicleLauncher), nameof(CompVehicleLauncher.TryLaunch))
                    .ExposeParameter(1)
                    .TransformTarget(GetFlightPathSerializer<CompVehicleLauncher>())
                    .SetPostInvoke(RestoreFlightPath);
                // AerialVehicleInFlight
                MP.RegisterSyncMethod(typeof(AerialVehicleInFlight), nameof(AerialVehicleInFlight.NewDestination))
                    .ExposeParameter(1)
                    .TransformTarget(GetFlightPathSerializer<AerialVehicleInFlight>())
                    .SetPostInvoke(RestoreFlightPath);

                // Launch from FloatMenu
                var syncDelegates = new List<ISyncDelegate>();
                // Landing on an empty tile
                syncDelegates.AddRange(MpCompat.RegisterLambdaDelegate(typeof(LaunchProtocol), nameof(LaunchProtocol.FloatMenuOption_LandCaravanEmptyTile), 0));
                // Landing in specific location on map
                syncDelegates.AddRange(MpCompat.RegisterLambdaDelegate(typeof(LaunchProtocol), nameof(LaunchProtocol.FloatMenuOption_LandInsideMap), 1));
                syncDelegates.AddRange(MpCompat.RegisterLambdaDelegate(typeof(DefaultTakeoff), nameof(DefaultTakeoff.FloatMenuOption_LandInsideMap), 1));
                // Land at settlement to visit (0), trade (1), offer gifts (2)
                syncDelegates.AddRange(MpCompat.RegisterLambdaDelegate(typeof(LaunchProtocol), nameof(LaunchProtocol.FloatMenuOption_LandAtSettlement), 0, 1, 2));

                // Setup extra data needed for syncing, syncing the flight path, restoring it after
                // invoking, and setting the context to WorldSelected (as it may be used by the mod).
                foreach (var syncDelegate in syncDelegates)
                    syncDelegate
                        .TransformField("<>4__this", GetFlightPathSerializer<LaunchProtocol>(), true) // Also sync DefaultTakeoff as LaunchProtocol
                        .SetPostInvoke(RestoreFlightPath)
                        .SetContext(SyncContext.WorldSelected);

                // FloatMenuOption_ReconMap is unused for now, so let's not sync it yet just in case it gets removed, renamed, etc.
                // FloatMenuOption_StrafeMap may be slightly more complex due to continuing targetting, despite immediately registering the action. Right now unused, so we don't really care.

                // Generic method, inside of a generic nested type. The normal way of acquiring those
                // won't really work here, so a slight change was needed to the method itself.
                // As for the patch itself, we replace the mod's actual call in MP with our own,
                // which will either call our synced method (SyncedLaunchOrFlyTo), or pass it to
                // the UI callback function (which will then presumably call our synced method
                // when pressing an "accept" button or whatever the mod makes the callback).
                MpCompat.harmony.Patch(
                    MpMethodUtil.GetLambdaGeneric(
                        typeof(VehicleArrivalActionUtility),
                        nameof(VehicleArrivalActionUtility.GetFloatMenuOptions),
                        MethodType.Normal,
                        null,
                        new[] { typeof(AerialVehicleArrivalAction) },
                        new[] { typeof(AerialVehicleArrivalAction) },
                        0),
                    prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(PreVehicleArrivalActionUtility)));

                MP.RegisterSyncMethod(typeof(VehicleFramework), nameof(SyncedLaunchOrFlyTo))
                    .SetContext(SyncContext.WorldSelected)
                    .ExposeParameter(0);

                // When launching from this specific location (unless the float menu is opened), it'll force rotation
                // on the vehicle (if it has forced location set). It should get set later on, so we ignore that here
                // as it would cause issues to force rotation from interface.
                MpCompat.harmony.Patch(
                    MpMethodUtil.GetLocalFunc(
                        typeof(LaunchProtocol),
                        nameof(LaunchProtocol.ChoseWorldTarget),
                        parentArgs: new[] { typeof(GlobalTargetInfo), typeof(float) },
                        localFunc: "Validator"),
                    transpiler: new HarmonyMethod(typeof(VehicleFramework), nameof(NoForcedRotationInInterface)));

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
                    postfix: new HarmonyMethod(typeof(VehicleFramework), nameof(PreventMapRemovalForLandingSessions)) { after = new[] { "SmashPhil.VehicleFramework" } });

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
                MP.RegisterSyncWorker<Vehicle_IgnitionController>(SyncVehicleIgnitionController);
                MP.RegisterSyncWorker<VehicleHandler>(SyncVehicleHandler);
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

                // Insert VehicleHandler as supported thing holder for syncing.
                // The mod uses VehicleHandler as IThingHolder and ends up being synced.
                // We should add support for adding more supported thing holders soon... I think the PokéWorld mod would benefit from it as well.
                const string supportedThingHoldersFieldPath = "Multiplayer.Client.RwImplSerialization:supportedThingHolders";
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
                    array[array.Length - 1] = typeof(VehicleHandler);
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
        private static bool NoThreadInMp(VehicleMapping mapping) => !MP.IsInMultiplayer && mapping.ThreadAvailable;

        private static IEnumerable<CodeInstruction> ReplaceThreadAvailable(IEnumerable<CodeInstruction> instr, MethodBase baseMethod)
        {
            var target = AccessTools.DeclaredPropertyGetter(typeof(VehicleMapping), nameof(VehicleMapping.ThreadAvailable));
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
                Log.Warning($"Failed to patch {nameof(VehicleMapping)}.{nameof(VehicleMapping.ThreadAvailable)} calls for method {name}");
            }
        }

        #endregion

        #region Other fixes

        private static IEnumerable<CodeInstruction> DisableDebugFlashing(IEnumerable<CodeInstruction> instr, MethodBase baseMethod)
        {
            var target = AccessTools.DeclaredMethod(typeof(Ext_Map), nameof(Ext_Map.DrawCell_ThreadSafe));
            var replacement = AccessTools.DeclaredMethod(typeof(VehicleFramework), nameof(NoCellDrawing));
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
                Log.Warning($"Failed to patch {nameof(Ext_Map)}.{nameof(Ext_Map.DrawCell_ThreadSafe)} calls (patch most likely no longer needed) for method {name}");
            }
        }

        private static void NoCellDrawing(Map map, IntVec3 cell, float colorPct, string text, int duration)
        {
            // Do nothing in MP.
            // We could prefix the method that we patch instead and prevent it from running, but
            // it would prevent it from running it from other places in the mod.

            if (!MP.IsInMultiplayer)
                map.DrawCell_ThreadSafe(cell, colorPct, text, duration);
        }

        #endregion

        #region MP safe pathfinding

        // private static bool PrePathTicker(Vehicle_PathFollower __instance)
        // {
        //     if (!MP.IsInMultiplayer)
        //         return true;
        //
        //     // In the mod, it wouldn't wait for the path to be calculated (and instead assign it the first tick it's ready).
        //     // In MP, we need to wait here for pathfinding to be finished.
        //     lock (__instance.pathLock)
        //     {
        //         while (__instance.CalculatingPath)
        //             Monitor.Wait(__instance.pathLock, 25); // Wait until the lock is pulsed, but just in case - check every 25ms if the path is ready.
        //     }
        //
        //     if (__instance.pathToAssign == PawnPath.NotFound)
        //     {
        //         __instance.pathToAssign = null;
        //         __instance.PatherFailed();
        //     }
        //     else if (__instance.pathToAssign != null)
        //     {
        //         __instance.curPath?.ReleaseToPool();
        //         __instance.curPath = __instance.pathToAssign;
        //         __instance.pathToAssign = null;
        //     }
        //
        //     return true;
        // }
        //
        // private static bool PreThreadedTrySetNewPath(Vehicle_PathFollower __instance)
        // {
        //     if (!MP.IsInMultiplayer)
        //         return true;
        //
        //     __instance.CalculatingPath = true;
        //
        //     var cachedMapComponent = __instance.vehicle.Map.GetCachedMapComponent<VehicleMapping>();
        //     if (cachedMapComponent.ThreadAvailable)
        //     {
        //         var asyncAction = AsyncPool<AsyncAction>.Get();
        //         asyncAction.Set(
        //             () => MpSafeThreadedPathfinding(__instance),
        //             () => __instance.moving && __instance.CalculatingPath,
        //             _ => MpSafeThreadedPathfindingErrorHandling(__instance));
        //         cachedMapComponent.dedicatedThread.Queue(asyncAction);
        //     }
        //     else
        //     {
        //         if (!VehicleMod.settings.debug.debugUseMultithreading)
        //             Log.WarningOnce("Finding path on main thread. DedicatedThread was not available.", __instance.vehicle.Map.GetHashCode());
        //         __instance.TrySetNewPath();
        //         __instance.CalculatingPath = false;
        //     }
        //
        //     return false;
        // }
        //
        // private static void MpSafeThreadedPathfinding(Vehicle_PathFollower pather)
        // {
        //     var path = pather.GenerateNewPath_Concurrent();
        //     if (path is not { Found: true })
        //     {
        //         pather.pathToAssign = PawnPath.NotFound;
        //         Messages.Message("VF_NoPathForVehicle".Translate(), MessageTypeDefOf.RejectInput, false);
        //     }
        //     else
        //     {
        //         pather.pathToAssign?.ReleaseToPool();
        //         pather.pathToAssign = path;
        //     }
        //
        //     pather.CalculatingPath = false;
        //     // No need to pulse all, as there should (at most) be 1 object waiting
        //     lock (pather.pathLock)
        //         Monitor.Pulse(pather.pathLock);
        // }
        //
        // private static void MpSafeThreadedPathfindingErrorHandling(Vehicle_PathFollower pather)
        // {
        //     pather.pathToAssign = PawnPath.NotFound;
        //     pather.CalculatingPath = false;
        //     // No need to pulse all, as there should (at most) be 1 object waiting
        //     lock (pather.pathLock)
        //         Monitor.Pulse(pather.pathLock);
        // }

        #endregion

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
        // the current mouse position, which we don't want and it's a feature we've disabled in MP.
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

        private static void RestoreFlightPath(object instance, object[] args)
        {
            // If we temporarily replaced it, it should at the very least be an empty list.
            if (tempFlightPath != null)
            {
                LaunchTargeter.FlightPath = tempFlightPath;
                tempFlightPath = null;
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

        private static bool PreVehicleArrivalActionUtility(Action<Action> ___uiConfirmationCallback, Func<FloatMenuAcceptanceReport> ___acceptanceReportGetter, Func<AerialVehicleArrivalAction> ___arrivalActionGetter, VehiclePawn ___vehicle, int ___destinationTile)
        {
            // If not in MP, let it run normally..
            // If in MP and not accepted, let it run to display error.
            if (!MP.IsInMultiplayer || !___acceptanceReportGetter().Accepted)
                return true;

            // Either sync the launch, or pass it as an action to the UI callback
            if (___uiConfirmationCallback == null)
                Action();
            else
                ___uiConfirmationCallback(Action);

            return false;

            void Action() => SyncedLaunchOrFlyTo(___arrivalActionGetter(), ___vehicle, ___vehicle.Spawned, ___destinationTile, LaunchTargeter.FlightPath);
        }

        private static void SyncedLaunchOrFlyTo(AerialVehicleArrivalAction action, VehiclePawn vehicle, bool wasSpawned, int destinationTile, List<FlightNode> path)
        {
            // Stop the call if the current spawned state of the vehicle doesn't match the one when syncing
            if (vehicle.Spawned != wasSpawned)
                return;

            if (vehicle.Spawned)
            {
                // If spawned, try launching vehicle
                try
                {
                    tempFlightPath = LaunchTargeter.FlightPath;
                    LaunchTargeter.FlightPath = path;

                    vehicle.CompVehicleLauncher.TryLaunch(destinationTile, action);
                }
                finally
                {
                    LaunchTargeter.FlightPath = tempFlightPath;
                }
            }
            else
            {
                // If not spawned, get the vehicle world object and set target
                var aerialVehicle = AerialVehicleLaunchHelper.GetOrMakeAerialVehicle(vehicle);
                aerialVehicle.OrderFlyToTiles(path, aerialVehicle.DrawPos, action);
            }
        }

        private static Serializer<T, (T target, List<FlightNode> path)> GetFlightPathSerializer<T>()
        {
            return Serializer.New(
                (T target) => (target, LaunchTargeter.FlightPath),
                (data) =>
                {
                    var (target, path) = data;

                    tempFlightPath = LaunchTargeter.FlightPath;
                    LaunchTargeter.FlightPath = path;

                    return target;
                });
        }

        private static IEnumerable<CodeInstruction> NoForcedRotationInInterface(IEnumerable<CodeInstruction> instr, MethodBase baseMethod)
        {
            var targetMethod = AccessTools.DeclaredPropertyGetter(typeof(Rot4?), nameof(Nullable<Rot4>.HasValue));
            var inMpCall = AccessTools.DeclaredMethod(typeof(VehicleFramework), nameof(NotInMultiplayer));
            var replacedCount = 0;

            foreach (var ci in instr)
            {
                yield return ci;

                if (ci.Calls(targetMethod))
                {
                    // Push !MP.IsInMultiplayer to the top of the stack
                    yield return new CodeInstruction(OpCodes.Call, inMpCall);
                    // Do a bitwise and on the 2 most recent value on the stack, namely
                    // the current mod's value, and our !MP.IsInMultiplayer call result
                    yield return new CodeInstruction(OpCodes.And);
                    // Basically, ensure that forcibly rotating the vehicle when launching it
                    // only happens in SP. It should get rotated in a different location (during actual launch).

                    replacedCount++;
                }
            }

            const int expected = 2;
            if (replacedCount != expected)
            {
                var name = (baseMethod.DeclaringType?.Namespace).NullOrEmpty() ? baseMethod.Name : $"{baseMethod.DeclaringType!.Name}:{baseMethod.Name}";
                Log.Warning($"Patched incorrect number of Widgets.ButtonText calls (patched {replacedCount}, expected {expected}) for method {name}");
            }
        }

        private static bool NotInMultiplayer() => !MP.IsInMultiplayer;

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
                    sync.Write((byte)1);
                    sync.Write(aerialVehicle);
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
                        vehicle = vehicleInFlight?.vehicle;

                        if (vehicle == null)
                            Log.Error($"Trying to read {nameof(VehiclePawn)}, but the {nameof(AerialVehicleInFlight)} is missing it. VehicleInFlight={vehicleInFlight}");

                        break;
                    }
                    case byte.MaxValue:
                        break;
                    default:
                        throw new Exception($"Trying to read {nameof(LaunchProtocol)}, but received an unsupported holder type ({type})");
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

        private static void SyncVehicleIgnitionController(SyncWorker sync, ref Vehicle_IgnitionController controller)
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
                    Log.Error($"Trying to read Vehicle_IgnitionController, but the vehicle is missing it. Vehicle={vehiclePawn}");
            }
        }

        private static void SyncVehicleHandler(SyncWorker sync, ref VehicleHandler handler)
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
                        Log.Error($"Trying to read VehicleHandler, received an uninitialized handler. Vehicle={vehiclePawn}, id=-1");
                        return;
                    case < -1:
                        Log.Warning($"Trying to read VehicleHandler, received handler with local ID. This shouldn't happen. Vehicle={vehiclePawn}, id={id}");
                        return;
                }

                if (vehiclePawn == null)
                {
                    Log.Error($"Trying to read VehicleHandler, received a null parent vehicle. id={id}");
                    return;
                }

                if (vehiclePawn.handlers == null)
                {
                    Log.Error($"Trying to read VehicleHandler, but the vehicle contains a null list of handlers. Vehicle={vehiclePawn}, id={id}");
                    return;
                }

                handler = vehiclePawn.handlers.FirstOrDefault(t => t.uniqueID == id);
                if (handler == null)
                    Log.Error($"Trying to read VehicleHandler, but the list of handlers does not contain the handler we're trying to read. Vehicle={vehiclePawn}, id={id}");
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
                var handler = sync.Read<VehicleHandler>();
                seat = new AssignedSeat
                {
                    handler = handler,
                    vehicle = handler.vehicle
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

        #endregion

        #region Sessions

        #region Form vehicle caravan

        #region Session class

        [MpCompatRequireMod("SmashPhil.VehicleFramework")]
        private class FormVehicleCaravanSession : ExposableSession, ISessionWithTransferables, ISessionWithCreationRestrictions
        {
            public static FormVehicleCaravanSession drawingSession;

            public override Map Map { get; }

            private bool reform;
            public int startingTile = -1;
            public int destinationTile = -1;
            private List<TransferableOneWay> transferables = new();
            public Dictionary<Pawn, AssignedSeat> assignedSeats = new();

            public bool uiDirty;

            private FormVehicleCaravanSession(Map map) : base(map)
            {
                Map = map;
            }

            // (Action onClosed) is always null and (bool mapAboutToBeRemoved) is always false 
            public FormVehicleCaravanSession(Map map, bool reform) : this(map)
            {
                this.reform = reform;

                AddItems();
            }

            private void AddItems()
            {
                var dialog = new Dialog_FormVehicleCaravan(Map, reform);
                dialog.CalculateAndRecacheTransferables();
                transferables = dialog.transferables;
                assignedSeats.AddRange(CaravanHelper.assignedSeats);
                CaravanHelper.assignedSeats.Clear();
            }

            public override void ExposeData()
            {
                base.ExposeData();

                Scribe_Values.Look(ref reform, "reform");
                Scribe_Values.Look(ref startingTile, "startingTile");
                Scribe_Values.Look(ref destinationTile, "destinationTile");

                Scribe_Collections.Look(ref transferables, "transferables", LookMode.Deep);
                Scribe_Collections.Look(ref assignedSeats, "assignedSeats", LookMode.Reference, LookMode.Deep);
            }

            public override bool IsCurrentlyPausing(Map map) => map == Map;

            private void OpenWindow(bool sound = true)
            {
                Log.Message($"session {sessionId}");

                var dialog = PrepareDummyDialog();
                if (!sound)
                    dialog.soundAppear = null;

                Find.WindowStack.Add(dialog);
                uiDirty = true;
            }

            private Dialog_FormVehicleCaravan PrepareDummyDialog()
            {
                var dialog = new Dialog_FormVehicleCaravan(Map, reform)
                {
                    transferables = transferables,
                    startingTile = startingTile,
                    destinationTile = destinationTile,
                    thisWindowInstanceEverOpened = true, // Prevent CalculateAndRecacheTransferables call
                };

                // Initialize UI
                UIHelper.CreateVehicleCaravanTransferableWidgets(
                    transferables,
                    out dialog.pawnsTransfer,
                    out dialog.vehiclesTransfer,
                    out dialog.itemsTransfer,
                    "FormCaravanColonyThingCountTip".Translate(),
                    dialog.IgnoreInventoryMode,
                    () => dialog.MassCapacity - dialog.MassUsage,
                    dialog.AutoStripSpawnedCorpses,
                    dialog.CurrentTile,
                    dialog.mapAboutToBeRemoved);

                return dialog;
            }

            public void ChooseRoute(int destination)
            {
                var dialog = PrepareDummyDialog();
                dialog.Notify_ChoseRoute(destination);

                startingTile = dialog.startingTile;
                destinationTile = dialog.destinationTile;

                uiDirty = true;
            }

            public void TryReformCaravan()
            {
                SafelyHandleSessionOperation(dialog =>
                {
                    if (dialog.TryReformCaravan())
                        Remove();
                });
            }

            public void TryFormAndSendCaravan()
            {
                SafelyHandleSessionOperation(dialog =>
                {
                    if (dialog.TryFormAndSendCaravan())
                        Remove();
                });
            }

            public void DebugTryFormCaravanInstantly()
            {
                SafelyHandleSessionOperation(dialog =>
                {
                    if (dialog.DebugTryFormCaravanInstantly())
                        Remove();
                });
            }

            public void Reset()
            {
                transferables.ForEach(t => t.CountToTransfer = 0);
                uiDirty = true;
            }

            public void Remove()
            {
                MP.GetLocalSessionManager(Map).RemoveSession(this);
                VehicleRoutePlanner.Instance.Stop();
            }

            public void SetAssignedSeats(Dictionary<Pawn, AssignedSeat> seats)
            {
                assignedSeats = seats;
                uiDirty = true;
            }

            private void SafelyHandleSessionOperation(Action<Dialog_FormVehicleCaravan> operation)
            {
                var dialog = PrepareDummyDialog();

                // Just in case, if there's a session setup - just let it run as normal.
                if (drawingSession != null)
                {
                    operation(dialog);
                    return;
                }

                // Set the session as active before doing the operation, and unset afterwards.
                try
                {
                    SetCurrentFormVehicleCaravanSessionState(this, dialog);
                    operation(dialog);
                }
                finally
                {
                    SetCurrentFormVehicleCaravanSessionState(null);
                }
            }

            public static bool TryOpenFormVehicleCaravanDialog(Map map)
            {
                if (map == null)
                    return false;

                var session = MP.GetLocalSessionManager(map).GetFirstOfType<FormVehicleCaravanSession>();
                if (session == null)
                    return false;

                session.OpenWindow();
                return true;
            }

            public static void CreateFormVehicleCaravanSession(Map map, bool reform)
            {
                if (map == null)
                {
                    Log.Error($"Trying to create {nameof(FormVehicleCaravanSession)} with a null map.");
                    return;
                }

                var manager = MP.GetLocalSessionManager(map);
                var session = manager.GetFirstOfType<FormVehicleCaravanSession>();
                if (session == null)
                {
                    session = new FormVehicleCaravanSession(map, reform);
                    if (!manager.AddSession(session))
                        session = null;
                }

                if (session == null)
                    Log.Error($"Couldn't get or create {nameof(FormVehicleCaravanSession)}");
                else if (MP.IsExecutingSyncCommandIssuedBySelf)
                    session.OpenWindow();
            }

            public override FloatMenuOption GetBlockingWindowOptions(ColonistBar.Entry entry)
            {
                return new FloatMenuOption("MpVehicleCaravanFormingSession".Translate(), () =>
                {
                    SwitchToMapOrWorld(Map);
                    OpenWindow();
                });
            }

            public Transferable GetTransferableByThingId(int thingId)
                => transferables.Find(tr => tr.things.Any(t => t.thingIDNumber == thingId));

            public void Notify_CountChanged(Transferable tr) => uiDirty = true;

            public bool CanExistWith(Session other) => other is not FormVehicleCaravanSession;
        }

        #endregion

        #region Dialog patches

        private static void SetCurrentFormVehicleCaravanSessionState(FormVehicleCaravanSession session, Dialog_FormVehicleCaravan dialog = null)
        {
            FormVehicleCaravanSession.drawingSession = session;
            MP.SetCurrentSessionWithTransferables(session);

            if (session == null)
            {
                Dialog_FormVehicleCaravan.CurrentFormingCaravan = null;
                CaravanHelper.assignedSeats.Clear();
            }
            else
            {
                Dialog_FormVehicleCaravan.CurrentFormingCaravan = dialog;
                CaravanHelper.assignedSeats.AddRange(session.assignedSeats);
            }
        }

        private static void PreDrawFormVehicleCaravan(Dialog_FormVehicleCaravan __instance)
        {
            if (!MP.IsInMultiplayer)
                return;

            var session = MP.GetLocalSessionManager(__instance.map).GetFirstOfType<FormVehicleCaravanSession>();
            if (session == null)
            {
                __instance.Close();
                return;
            }

            MP.WatchBegin();
            SetCurrentFormVehicleCaravanSessionState(session, __instance);

            if (session.uiDirty)
            {
                __instance.CountToTransferChanged();
                __instance.startingTile = session.startingTile;
                __instance.destinationTile = session.destinationTile;
                session.uiDirty = false;
            }
        }

        private static void FinalizeDrawFormVehicleCaravan()
        {
            if (FormVehicleCaravanSession.drawingSession != null)
            {
                MP.WatchEnd();
                SetCurrentFormVehicleCaravanSessionState(null);
            }
        }

        private static bool PreNotifyChoseRoute(Dialog_FormVehicleCaravan __instance, int destinationTile)
        {
            if (!MP.InInterface)
                return true;

            MP.GetLocalSessionManager(__instance.map).GetFirstOfType<FormVehicleCaravanSession>()?.ChooseRoute(destinationTile);
            return false;
        }

        private static bool PreTryReformCaravan(Dialog_FormVehicleCaravan __instance)
        {
            if (!MP.InInterface)
                return true;

            MP.GetLocalSessionManager(__instance.map).GetFirstOfType<FormVehicleCaravanSession>()?.TryReformCaravan();
            return false;
        }

        private static bool PreTryFormAndSendCaravan(Dialog_FormVehicleCaravan __instance)
        {
            if (!MP.InInterface)
                return true;

            MP.GetLocalSessionManager(__instance.map).GetFirstOfType<FormVehicleCaravanSession>()?.TryFormAndSendCaravan();
            return false;
        }

        private static bool PreDebugTryFormCaravanInstantly(Dialog_FormVehicleCaravan __instance)
        {
            if (!MP.InInterface)
                return true;

            MP.GetLocalSessionManager(__instance.map).GetFirstOfType<FormVehicleCaravanSession>()?.DebugTryFormCaravanInstantly();
            return false;
        }

        private static bool ReplacedFormCaravanCancelButton(Rect rect, string label, bool drawBackground, bool doMouseoverSound, bool active, TextAnchor? overrideTextAnchor)
        {
            bool DoButton() => Widgets.ButtonText(rect, label, drawBackground, doMouseoverSound, active, overrideTextAnchor);

            if (FormVehicleCaravanSession.drawingSession == null)
                return DoButton();

            var color = GUI.color;
            try
            {
                // Red button like in MP
                GUI.color = new Color(1f, 0.3f, 0.35f);

                // If the button was pressed sync removing the dialog
                if (DoButton())
                    FormVehicleCaravanSession.drawingSession.Remove();
            }
            finally
            {
                GUI.color = color;
            }

            return false;
        }

        private static bool ReplacedFormCaravanResetButton(Rect rect, string label, bool drawBackground, bool doMouseoverSound, bool active, TextAnchor? overrideTextAnchor)
        {
            var result = Widgets.ButtonText(rect, label, drawBackground, doMouseoverSound, active, overrideTextAnchor);

            if (!result || FormVehicleCaravanSession.drawingSession == null)
                return result;

            FormVehicleCaravanSession.drawingSession.Reset();
            return false;
        }

        #endregion

        #region Gizmo patches

        private static bool PreFormCaravanDialog(MapParent ___mapParent)
        {
            if (!MP.IsInMultiplayer)
                return true;

            if (!FormVehicleCaravanSession.TryOpenFormVehicleCaravanDialog(___mapParent.Map))
                FormVehicleCaravanSession.CreateFormVehicleCaravanSession(___mapParent.Map, false);
            return false;
        }

        private static bool PreReformCaravanDialog(MapParent ___mapParent)
        {
            if (!MP.IsInMultiplayer)
                return true;

            if (!FormVehicleCaravanSession.TryOpenFormVehicleCaravanDialog(___mapParent.Map))
                FormVehicleCaravanSession.CreateFormVehicleCaravanSession(___mapParent.Map, true);
            return false;
        }

        #endregion

        #region Transferable Vehicle Widget

        private static void PreTransferableVehicleWidgetMainRect(TransferableVehicleWidget __instance)
        {
            if (FormVehicleCaravanSession.drawingSession == null)
                return;

            foreach (var pawnTransferable in __instance.AvailablePawns)
                CreateAndSyncMpTransferableReference(FormVehicleCaravanSession.drawingSession, pawnTransferable);
        }

        private static void PostTransferableVehicleWidgetMainRect()
        {
            if (FormVehicleCaravanSession.drawingSession != null)
                TrySyncAssignedSeats(FormVehicleCaravanSession.drawingSession);
        }

        private static void PreDoCountAdjustInterface(Transferable trad)
        {
            if (FormVehicleCaravanSession.drawingSession != null)
                CreateAndSyncMpTransferableReference(FormVehicleCaravanSession.drawingSession, trad);
        }

        private static void PreDrawAssignSeats(Dialog_AssignSeats __instance)
        {
            if (!MP.IsInMultiplayer)
                return;

            var dialog = Find.WindowStack.WindowOfType<Dialog_FormVehicleCaravan>();
            if (dialog == null)
            {
                __instance.Close();
                return;
            }

            var session = MP.GetLocalSessionManager(__instance.transferable.AnyThing.Map).GetFirstOfType<FormVehicleCaravanSession>();
            if (session == null)
            {
                __instance.Close();
                return;
            }

            SetCurrentFormVehicleCaravanSessionState(session, dialog);

            // Update data if another player changed stuff
            var vehicle = __instance.Vehicle;
            // Reset the list of pawns valid for this vehicle (not assigned to other vehicles)
            Dialog_AssignSeats.GetTransferablePawns(dialog.vehiclesTransfer.AvailablePawns, vehicle, __instance.transferablePawns);

            // Get pawns out of transferables
            __instance.pawns.Clear();
            __instance.pawns.AddRange(__instance.transferablePawns.Select(x => x.AnyThing as Pawn));

            // Set the assigned seats
            foreach (var handler in vehicle.handlers)
            {
                foreach (var pawn in handler.handlers)
                {
                    // Add only if doesn't exist
                    __instance.assignedSeats.TryAdd(pawn, (vehicle, handler));
                }
            }

            foreach (var (pawn, seat) in CaravanHelper.assignedSeats)
            {
                if (__instance.assignedSeats.TryGetValue(pawn, out var current) && current.vehicle == vehicle)
                {
                    // Add only if doesn't exist
                    __instance.assignedSeats.TryAdd(pawn, seat);
                }
            }

            // Remove all the assigned pawns that got assigned to other vehicles (missing from pawn list)
            __instance.assignedSeats.RemoveAll(x => !__instance.pawns.Contains(x.Key));
        }

        private static void FinalizeDrawAssignSeats()
        {
            if (FormVehicleCaravanSession.drawingSession != null)
                SetCurrentFormVehicleCaravanSessionState(null);
        }

        private static void PreFinalizeAssignSeats(Dialog_AssignSeats __instance)
        {
            if (FormVehicleCaravanSession.drawingSession == null)
                return;

            MP.WatchBegin();

            foreach (var transferable in __instance.transferablePawns.Concat(__instance.transferable))
                CreateAndSyncMpTransferableReference(FormVehicleCaravanSession.drawingSession, transferable);
        }

        private static void FinalizeFinalizeAssignSeats()
        {
            if (FormVehicleCaravanSession.drawingSession == null)
                return;

            MP.WatchEnd();
            TrySyncAssignedSeats(FormVehicleCaravanSession.drawingSession);
        }

        private static void TrySyncAssignedSeats(FormVehicleCaravanSession session)
        {
            if (CaravanHelper.assignedSeats.Count != FormVehicleCaravanSession.drawingSession.assignedSeats.Count)
            {
                session.SetAssignedSeats(CaravanHelper.assignedSeats);
                return;
            }

            foreach (var (pawn, seat) in CaravanHelper.assignedSeats)
            {
                if (!FormVehicleCaravanSession.drawingSession.assignedSeats.TryGetValue(pawn, out var otherSeat))
                {
                    session.SetAssignedSeats(CaravanHelper.assignedSeats);
                    return;
                }

                if (seat.vehicle != otherSeat.vehicle || seat.handler != otherSeat.handler)
                {
                    session.SetAssignedSeats(CaravanHelper.assignedSeats);
                    return;
                }
            }
        }

        #endregion

        #endregion

        #region Load cargo session

        #region Session class

        [MpCompatRequireMod("SmashPhil.VehicleFramework")]
        private class LoadVehicleCargoSession : ExposableSession, ISessionWithTransferables, ISessionWithCreationRestrictions
        {
            public static LoadVehicleCargoSession drawingSession;
            public static bool allowedToRecacheTransferables = false;

            public override Map Map => vehicle.Map;

            private VehiclePawn vehicle;
            public List<TransferableOneWay> transferables = new();

            public bool uiDirty;
            public bool widgetDirty;

            [UsedImplicitly]
            public LoadVehicleCargoSession(Map _) : base(null)
            {
            }

            public LoadVehicleCargoSession(VehiclePawn vehicle) : base(null)
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
                Log.Message($"session {sessionId}");

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
                    Log.Error($"Trying to create {nameof(FormVehicleCaravanSession)} for a null vehicle.");
                    return;
                }

                if (vehicle.Map == null)
                {
                    Log.Error($"Trying to create {nameof(FormVehicleCaravanSession)} for a vehicle with null map. Vehicle={vehicle}");
                    return;
                }

                var manager = MP.GetLocalSessionManager(vehicle.Map);
                var session = manager.GetFirstOfType<LoadVehicleCargoSession>();
                if (session == null)
                {
                    session = new LoadVehicleCargoSession(vehicle);
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
            private List<VehiclePawn> vehicles = new();
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
                new[] { typeof(Rect), typeof(string), typeof(bool), typeof(bool), typeof(bool), typeof(TextAnchor?) });
            var buttonReplacements = new Dictionary<string, MethodInfo>();
            int expected;

            if (baseMethod.DeclaringType == typeof(Dialog_FormVehicleCaravan))
            {
                expected = 2;
                buttonReplacements.Add("CancelButton", AccessTools.DeclaredMethod(typeof(VehicleFramework), nameof(ReplacedFormCaravanCancelButton)));
                buttonReplacements.Add("ResetButton", AccessTools.DeclaredMethod(typeof(VehicleFramework), nameof(ReplacedFormCaravanResetButton)));
            }
            else if (baseMethod.DeclaringType == typeof(Dialog_LoadCargo))
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
                var switchToMapText = "MpVehiclesSwitchToMap".Translate();
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
            // of TurretRotation getter. However, calling it during drawing will cause issues,
            // and the turrets don't always tick, so we need to ensure this is updated when
            // the turret is not ticking, and it's done in a deterministic manner.
            foreach (var turret in turretsComp.turrets)
            {
                if (turret.IsTargetable || turret.attachedTo != null)
                {
                    turret.ValidateLockStatus();
                    turret.rotation = turret.rotation.ClampAndWrap(0f, 360f);
                }
            }
        }

        [MpCompatPrefix(typeof(VehicleTurret), nameof(VehicleTurret.TurretRotation), methodType: MethodType.Getter)]
        private static void PreTurretRotation(VehicleTurret __instance, float ___rotation, ref float? __state)
        {
            if (MP.InInterface)
                __state = ___rotation;
        }

        [MpCompatPostfix(typeof(VehicleTurret), nameof(VehicleTurret.TurretRotation), methodType: MethodType.Getter)]
        private static void PostTurretRotation(ref float ___rotation, float? __state)
        {
            if (__state.HasValue)
                ___rotation = __state.Value;
        }

        [MpCompatPrefix(typeof(TurretTargeter), nameof(TurretTargeter.Turret), methodType: MethodType.Getter)]
        private static bool PreTurretTargeterCurrentTurretGetter()
        {
            // A couple of places during ticking check the current turret from the targeter. This will cause
            // issues due to conditional statements based on `TurretTargeter.Turret != this`, etc. so just
            // prevent the mod from returning the actual turret in interface (return default value/null).
            return !MP.IsInMultiplayer || MP.InInterface; // The inverse of what PatchingUtilities.PatchCancelInInterface does
        }

        #endregion
    }
}