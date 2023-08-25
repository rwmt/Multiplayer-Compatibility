using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Threading;
using HarmonyLib;
using Multiplayer.API;
using UnityEngine;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Vehicle Framework by Smash Phil</summary>
    /// <see href="https://github.com/SmashPhil/Vehicle-Framework"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3014915404"/>
    [MpCompatFor("SmashPhil.VehicleFramework")]
    public class VehicleFramework
    {
        #region Fields
        
        // Mp Compat fields
        private static bool shouldSyncInInterface = false;
        // private static bool isDrawingDialog = false;
        
        // VehicleMod
        private static AccessTools.FieldRef<ModSettings> vehicleModSettingsField;
        
        // VehiclesModSettings
        private static AccessTools.FieldRef<ModSettings, bool> settingsShowAllCargoItemsField;

        // Vehicle_PathFollower
        // private static FastInvokeHandler trySetNewPathDelayedMethod;

        // VehicleComponent
        private static AccessTools.FieldRef<object, Pawn> vehicleComponentParentField;

        // VehicleTurret
        private static AccessTools.FieldRef<object, int> vehicleTurretUniqueIdField;
        private static AccessTools.FieldRef<object, Pawn> vehicleTurretParentPawnField;
        private static FastInvokeHandler setTargetTurretMethod;
        private static FastInvokeHandler alignToAngleTurretMethod;

        // Vehicle_IgnitionController
        private static AccessTools.FieldRef<object, Pawn> vehicleIgnitionControllerParentField;

        private static AccessTools.FieldRef<object, int> vehicleHandlerUniqueIdField;
        private static AccessTools.FieldRef<object, Pawn> vehicleHandlerParentField;

        // VehiclePawn
        private static AccessTools.FieldRef<Pawn, object> vehiclePawnStatHandlerField;
        private static AccessTools.FieldRef<Pawn, object> vehiclePawnIgnitionControllerField;
        private static AccessTools.FieldRef<Pawn, IList> vehiclePawnHandlersList;
        private static FastInvokeHandler vehiclePawnTurretCompGetter;

        // VehicleStatHandler
        private static AccessTools.FieldRef<object, IList> vehicleStatHandlerComponentsField;

        // CompVehicleTurrets
        private static AccessTools.FieldRef<ThingComp, IList> turretCompTurretListField;

        // VehicleTabHelper_Passenger
        private static AccessTools.FieldRef<Pawn> passengerTabDraggedPawnField;
        private static AccessTools.FieldRef<Pawn> passengerTabHoveringOverPawnField;
        private static AccessTools.FieldRef<IThingHolder> passengerTabTransferToHolderField;
        private static FastInvokeHandler passengerTabHandleDragEventMethod;
        
        // Command_Turret
        private static AccessTools.FieldRef<Command, Pawn> turretCommandVehicleField;
        private static AccessTools.FieldRef<Command, object> turretCommandTurretField;
        
        // Dialog_LoadCargo
        private static Type loadCargoDialogType;
        private static FastInvokeHandler loadCargoRecacheMethod;

        #endregion

        #region Constructor

        public VehicleFramework(ModContentPack mod)
        {
            // Some stuff needs it, some doesn't. Too much effort going through everything to see what
            // needs late patch and what doesn't.
            LongEventHandler.ExecuteWhenFinished(LatePatch);

            TemporaryPatch();
        }

        #endregion

        #region Main patch

        private static void LatePatch()
        {
            #region MP Compat

            Type type;
            MethodInfo method;

            static ISyncMethod TrySyncDeclaredMethod(Type targetType, string targetMethodName)
            {
                var declaredMethod = AccessTools.DeclaredMethod(targetType, targetMethodName);
                if (declaredMethod != null)
                    return MP.RegisterSyncMethod(declaredMethod);
                return null;
            }

            #endregion

            #region SmashTools

            // If any other mod uses this library, this part of the patch will need to
            // be moved out to a separate class, and possibly updated further.

            #region Multithreading

            {
                // TODO: Fully investigate if needed, re-enable if yes.
                // MpCompat.harmony.Patch(AccessTools.DeclaredMethod("SmashTools.Performance.ThreadManager:CreateNew"),
                //     prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(NoThreadInMp)));
            }

            #endregion

            #region Coroutine

            {
                // TODO: Investigate usage and check if patch is needed
                // SmashTools.CoroutineManager
            }

            #endregion

            #endregion

            #region VehicleFramework

            #region Multithreading

            {
                // TODO: Fully investigate if needed, re-enable if yes.
                // type = AccessTools.TypeByName("Vehicles.Vehicle_PathFollower");
                // trySetNewPathDelayedMethod = MethodInvoker.GetHandler(AccessTools.DeclaredMethod(type, "TrySetNewPath_Delayed"));
                // MpCompat.harmony.Patch(AccessTools.DeclaredMethod(type, "TrySetNewPath_Threaded"),
                //     prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(RedirectThreadedPathfindingToNonThreaded)));
                // TrySetNewPath_Async should be fine, I think? It's not really used anyway.

                // TODO: Check Vehicles.WorldVehiclePathGrid:RecalculateAllPerceivedPathCosts
                // TODO: Check Vehicles.WorldVehiclePathGrid:WorldComponentTick
                // Those are not needed for on-map stuff, so leaving it for now.
            }

            #endregion

            #region Gizmos

            {
                // Buildings

                // Switch to next verb (0), cancel target (1)
                MpCompat.RegisterLambdaMethod("Vehicles.Building_Artillery", nameof(Building.GetGizmos), 0, 1);


                // Vehicle pawn

                type = AccessTools.TypeByName("Vehicles.VehiclePawn");
                // Cancel designator (1), disembark all pawns (4), toggle fishing (7).
                // Load cargo (2) opens a dialog, which we need to handle instead.
                // Skip syncing LordJob_FormAndSendVehicles, as it'll be null and MP currently doesn't handle null LordJobs/Lords.
                MpCompat.RegisterLambdaDelegate(type, "GetGizmos", new[] { "<>4__this" }, 1, 4, 7);
                // Disembark singe pawn (5).
                MpCompat.RegisterLambdaDelegate(type, "GetGizmos", new[] { "CS$<>8__locals1/<>4__this", "currentPawn" }, 5);
                // Force leave caravan (8), cancel forming caravan (9).
                // Those 2 will have the LordJob setup, so we can't skip any fields.
                MpCompat.RegisterLambdaDelegate(type, "GetGizmos", 8, 9);
                // Dev mode: Destroy component (10), damage component (12), heal all components (14),
                // recache all stats (16), give random pawn mental state (17), kill random pawn (18)
                // Flash OccupiedRect (19) - most likely pointless syncing it.
                MpCompat.RegisterLambdaDelegate(type, "GetGizmos", new[] { "<>4__this" }, 10, 12, 14, 16, 17, 18).SetDebugOnly();

                type = AccessTools.TypeByName("Vehicles.Vehicle_IgnitionController");
                // Toggle drafted or (if moving) engage brakes.
                // Alternative approach - sync both Vehicle_IgnitionController:set_Drafted and Vehicle_PathFollower:EngageBrakes,
                // but it would require making a sync worker for Vehicle_PathFollower.
                MpCompat.RegisterLambdaMethod(type, "GetGizmos", 1);


                // Comps

                type = AccessTools.TypeByName("Vehicles.CompFueledTravel");
                // Target fuel level setter, used from Gizmo_RefuelableFuelTravel
                MP.RegisterSyncMethod(type, "TargetFuelLevel");
                // Refuel from inventory, used from Gizmo_RefuelableFuelTravel
                MP.RegisterSyncMethod(type, "Refuel", new SyncType[]{ typeof(List<Thing>) });
                MP.RegisterSyncMethod(type, "Refuel", new SyncType[]{ typeof(float) });
                // Toggle connect/disconnect from power for electric vehicles
                MpCompat.RegisterLambdaMethod(type, nameof(ThingComp.CompGetGizmosExtra), 1);
                // (Dev) set fuel to 0/0.1/half/max
                MpCompat.RegisterLambdaMethod(type, "DevModeGizmos", 0, 1, 2, 3).SetDebugOnly();
                // (Dev) set fuel to 0/max
                MpCompat.RegisterLambdaMethod(type, "CompCaravanGizmos", 0, 1).SetDebugOnly();

                type = AccessTools.TypeByName("Vehicles.CompVehicleTurrets");
                MP.RegisterSyncMethod(type, "SetQuotaLevel");
                // (Dev) full reload turret/cannon
                MpCompat.RegisterLambdaDelegate(type, nameof(ThingComp.CompGetGizmosExtra), 2, 4).SetDebugOnly();


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
                // for example when it opens targetter.
                type = AccessTools.TypeByName("Vehicles.Command_Turret");
                turretCommandVehicleField = AccessTools.FieldRefAccess<Pawn>(type, "vehicle");
                turretCommandTurretField = AccessTools.FieldRefAccess<object>(type, "turret");
                MP.RegisterSyncWorker<Command>(SyncCommandTurret, type, true, true);

                // Static turrets
                type = AccessTools.TypeByName("Vehicles.Command_CooldownAction");
                MP.RegisterSyncMethod(type, "FireTurret");
                MpCompat.harmony.Patch(AccessTools.DeclaredMethod(type, "GizmoOnGUI"),
                    transpiler: new HarmonyMethod(typeof(VehicleFramework), nameof(ReplaceSetTargetCall)));

                // Targetale turrets
                type = AccessTools.TypeByName("Vehicles.Command_TargeterCooldownAction");
                method = MpMethodUtil.GetLambda(type, "FireTurret", MethodType.Normal, null, 0);
                MP.RegisterSyncDelegate(type, method.DeclaringType.Name, method.Name);
                MpCompat.harmony.Patch(method, prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(PreSetTurretTarget)));

                type = AccessTools.TypeByName("Vehicles.VehicleTurret");
                setTargetTurretMethod = MethodInvoker.GetHandler(AccessTools.DeclaredMethod(type, "SetTarget"));
                alignToAngleTurretMethod = MethodInvoker.GetHandler(AccessTools.DeclaredMethod(type, "AlignToAngleRestricted"));
                // Called from Vehicles.TurretTargeter:BeginTargeting
                PatchingUtilities.PatchCancelInInterface(AccessTools.DeclaredMethod(type, "StartTicking"));
                // Called from Vehicles.TurretTargeter:TargeterUpdate and Vehicles.TurretTargeter:StopTargeting(bool)
                PatchingUtilities.PatchCancelInInterface(AccessTools.DeclaredMethod(type, "AlignToAngleRestricted"));
                MP.RegisterSyncMethod(type, "CycleFireMode");
                // Future-proofing, currently only affects the base type as the subclass doesn't override those.
                foreach (var subclass in type.AllSubclasses().Concat(type))
                {
                    method = AccessTools.DeclaredMethod(subclass, "SetTarget");
                    if (method != null)
                    {
                        // Sync the call
                        MP.RegisterSyncMethod(method);
                        // But only allow it to be synced under very specific circumstances
                        MpCompat.harmony.Patch(method, prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(CancelTurretSetTargetSync)));
                    }
                    TrySyncDeclaredMethod(subclass, "TryRemoveShell");
                    TrySyncDeclaredMethod(subclass, "ReloadCannon");
                    TrySyncDeclaredMethod(subclass, "SwitchAutoTarget");
                }

                // type = AccessTools.TypeByName("Vehicles.TurretTargeter");
            }

            #endregion

            #region Float Menus

            {
                // Enter vehicle. Can't sync through TryTakeOrderedJob, as the method does a bit more stuff.
                MpCompat.RegisterLambdaDelegate("Vehicles.VehiclePawn", "GetFloatMenuOptions", 0);
                MpCompat.RegisterLambdaDelegate("Vehicles.VehiclePawn", "MultiplePawnFloatMenuOptions", 0);
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

                #region Load cargo

                // type = loadCargoDialogType = AccessTools.TypeByName("Vehicles.Dialog_LoadCargo");
                // DialogUtilities.RegisterDialogCloseSync(type, true);
                // MP.RegisterSyncMethod(type, "SetToSendEverything").SetDebugOnly(); // Dev: select everything
                // MP.RegisterSyncMethod(typeof(VehicleFramework), nameof(SyncedSetShowAllCargoItems));
                // method = AccessTools.DeclaredMethod(type, "CalculateAndRecacheTransferables");
                // MP.RegisterSyncMethod(method); // Reset button
                // loadCargoRecacheMethod = MethodInvoker.GetHandler(method);
                // MpCompat.harmony.Patch(method, prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(PreLoadCargoRecache)));
                // MpCompat.harmony.Patch(AccessTools.DeclaredMethod(type, "DoWindowContents"),
                //     prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(PreDrawLoadCargo)),
                //     finalizer: new HarmonyMethod(typeof(VehicleFramework), nameof(PostDrawLoadCargo)));
                //
                // type = AccessTools.TypeByName("Vehicles.VehicleReservationManager");
                // // Called when accepting load cargo dialog
                // MP.RegisterSyncMethod(type, "RegisterLister");
                //
                // type = AccessTools.TypeByName("Vehicles.VehicleMod");
                // vehicleModSettingsField = AccessTools.StaticFieldRefAccess<ModSettings>(AccessTools.DeclaredField(type, "settings"));
                //
                // type = AccessTools.TypeByName("Vehicles.VehiclesModSettings");
                // settingsShowAllCargoItemsField = AccessTools.FieldRefAccess<bool>(type, "showAllCargoItems");

                #endregion
            }

            #endregion

            #region ITabs and WITabs

            {
                type = AccessTools.TypeByName("Vehicles.ITab_Vehicle_Cargo");
                MP.RegisterSyncMethod(type, "InterfaceDrop").SetContext(SyncContext.MapSelected);

                // Used by Vehicles.ITab_Vehicle_Passengers and Vehicles.WITab_Vehicle_Manifest
                type = AccessTools.TypeByName("Vehicles.VehicleTabHelper_Passenger");
                method = AccessTools.DeclaredMethod(type, "HandleDragEvent");
                passengerTabDraggedPawnField = AccessTools.StaticFieldRefAccess<Pawn>(AccessTools.DeclaredField(type, "draggedPawn"));
                passengerTabHoveringOverPawnField = AccessTools.StaticFieldRefAccess<Pawn>(AccessTools.DeclaredField(type, "hoveringOverPawn"));
                passengerTabTransferToHolderField = AccessTools.StaticFieldRefAccess<IThingHolder>(AccessTools.DeclaredField(type, "transferToHolder"));
                passengerTabHandleDragEventMethod = MethodInvoker.GetHandler(method);
                MpCompat.harmony.Patch(method, prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(PreHandleDragEvent)));
                MP.RegisterSyncMethod(typeof(VehicleFramework), nameof(SyncedHandleDragEvent));
            }

            #endregion

            #region SyncWorkers

            {
                type = AccessTools.TypeByName("Vehicles.ITab_Vehicle_Cargo");
                MP.RegisterSyncWorker<object>(NoSync, type, shouldConstruct: true);

                type = AccessTools.TypeByName("Vehicles.VehicleComponent");
                vehicleComponentParentField = AccessTools.FieldRefAccess<Pawn>(type, "vehicle");
                MP.RegisterSyncWorker<object>(SyncVehicleComponent, type, isImplicit: true);

                type = AccessTools.TypeByName("Vehicles.VehicleTurret");
                vehicleTurretUniqueIdField = AccessTools.FieldRefAccess<int>(type, "uniqueID");
                vehicleTurretParentPawnField = AccessTools.FieldRefAccess<Pawn>(type, "vehicle");
                MP.RegisterSyncWorker<object>(SyncVehicleTurret, type, isImplicit: true);

                type = AccessTools.TypeByName("Vehicles.Vehicle_IgnitionController");
                vehicleIgnitionControllerParentField = AccessTools.FieldRefAccess<Pawn>(type, "vehicle");
                MP.RegisterSyncWorker<object>(SyncVehicleIgnitionController, type);

                type = AccessTools.TypeByName("Vehicles.VehicleHandler");
                vehicleHandlerUniqueIdField = AccessTools.FieldRefAccess<int>(type, "uniqueID");
                vehicleHandlerParentField = AccessTools.FieldRefAccess<Pawn>(type, "vehicle");
                MP.RegisterSyncWorker<object>(SyncVehicleHandler, type);

                // Extra stuff for previous sync workers
                type = AccessTools.TypeByName("Vehicles.VehiclePawn");
                vehiclePawnStatHandlerField = AccessTools.FieldRefAccess<object>(type , "statHandler");
                vehiclePawnIgnitionControllerField = AccessTools.FieldRefAccess<object>(type, "ignition");
                vehiclePawnHandlersList = AccessTools.FieldRefAccess<IList>(type, "handlers");
                vehiclePawnTurretCompGetter = MethodInvoker.GetHandler(AccessTools.DeclaredPropertyGetter(type, "CompVehicleTurrets"));

                vehicleStatHandlerComponentsField = AccessTools.FieldRefAccess<IList>("Vehicles.VehicleStatHandler:components");
                turretCompTurretListField = AccessTools.FieldRefAccess<IList>("Vehicles.CompVehicleTurrets:turrets");
            }

            #endregion

            #region Multiplayer

            {
                // Insert VehicleHandler as supported thing holder for syncing.
                // The mod uses VehicleHandler as IThingHolder and ends up being synced.
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
                    type = AccessTools.TypeByName("Vehicles.VehicleHandler");
                    if (type == null)
                        Log.Error("Cannot insert VehicleHandler to supported thing holders list, cannot access the type.");
                    else
                    {
                        // Increase size by 1
                        Array.Resize(ref array, array.Length + 1);
                        // Fill the last element
                        array[array.Length - 1] = type;
                        // Set the original field to the value we set up
                        supportedThingHoldersField.SetValue(null, array);
                    }
                }
            }

            #endregion

            // TODO: Aerial vehicle tab
            // TODO: ITabs and WITabs
            // TODO: Dialog form vehicle caravan (going to be a pain I bet)
            // TODO: Aerial launch (LaunchProtocol, Rocket Takeoff?, DefaultTakeoff?)

            #endregion
        }

        #endregion

        #region Temporary MP Patch

        // TODO: Remove if merged and included in MP release https://github.com/rwmt/Multiplayer/pull/391
        private static bool isMpPatchActive = false;
        private static Assembly mpAssembly;
        private static Type patchTargetType;

        // Really ugly solution. We need a patch that replaces call to `Type.GetMethod(string, BindingFlags)` so that it avoids
        // ambiguous match. We can't patch the problematic method, as it's called from the static constructor (meaning it'll
        // end up being called before we do anything with it). We also can't touch it too early as it'll end up causing further issues.
        // Since the type we need to patch is marked with `[StaticConstructorOnStartup]`, we hook up to `StaticConstructorOnStartupUtility`
        // so when it calls the static constructors everywhere, it'll first apply our patches in the prefix, let the method run and
        // run the static constructors, and once it's time for the MP type we need to patch it'll let it run. In the postfix it'll then
        // cleanup the stuff we set up.
        // We could likely patch all of the calls like in the PR, but I just went with the safe route and only applied the change
        // as precisely as possible to only affect the single method in the single type we need this for.
        private static void TemporaryPatch()
        {
            mpAssembly = AccessTools.TypeByName("Multiplayer.Client.Multiplayer").Assembly;
            if (mpAssembly == null)
            {
                Log.Error("Trying to patch MP failed, assembly is null.");
                return;
            }

            patchTargetType = AccessTools.TypeByName("Vehicles.VehiclePawn");
            if (patchTargetType == null)
            {
                Log.Error("Trying to patch MP failed, target type is null.");
                return;
            }

            MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(StaticConstructorOnStartupUtility), nameof(StaticConstructorOnStartupUtility.CallAll)),
                prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(SetupGetMethodCall)),
                finalizer: new HarmonyMethod(typeof(VehicleFramework), nameof(ClearTemporaryPatches)));
        }

        private static void SetupGetMethodCall()
        {
            MpCompat.harmony.Patch(
                AccessTools.DeclaredMethod(typeof(Type), nameof(Type.GetMethod), new []{ typeof(string), typeof(BindingFlags) }),
                prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(PatchedGetMethodCall)));
            MpCompat.harmony.Patch(
                AccessTools.DeclaredMethod(typeof(RuntimeHelpers), nameof(RuntimeHelpers.RunClassConstructor), new []{ typeof(RuntimeTypeHandle) }),
                prefix: new HarmonyMethod(typeof(VehicleFramework), nameof(TryActivateMpPatch)),
                finalizer: new HarmonyMethod(typeof(VehicleFramework), nameof(TryDeactivateMpPatch)));
        }

        private static void ClearTemporaryPatches()
        {
            MpCompat.harmony.Unpatch(
                AccessTools.DeclaredMethod(typeof(Type), nameof(Type.GetMethod), new []{ typeof(string), typeof(BindingFlags) }),
                HarmonyPatchType.All,
                MpCompat.harmony.Id);
            MpCompat.harmony.Unpatch(
                AccessTools.DeclaredMethod(typeof(RuntimeHelpers), nameof(RuntimeHelpers.RunClassConstructor), new []{ typeof(RuntimeTypeHandle) }),
                HarmonyPatchType.All,
                MpCompat.harmony.Id);
        }

        private static bool PatchedGetMethodCall(Type __instance, string name, BindingFlags bindingAttr, ref MethodInfo __result)
        {
            if (!isMpPatchActive)
                return true;
            if (name != "Kill" || __instance != patchTargetType)
                return true;

            __result = __instance.GetMethod(name, bindingAttr, null, new[]{ typeof(DamageInfo?), typeof(Hediff) }, null);
            return false;

        }

        private static void TryActivateMpPatch(RuntimeTypeHandle type)
        {
            var currentType = Type.GetTypeFromHandle(type);
            isMpPatchActive = currentType.Assembly == mpAssembly && currentType.Name == "MultiplayerStatic";
        }

        private static void TryDeactivateMpPatch(RuntimeTypeHandle type) => isMpPatchActive = false;

        #endregion

        #region Multithreading

        // Stops a thread from being created, returning null instead of the newly created thread. The mod creates 1 thread
        // per map comp. In most cases it checks if it can use the thread by checking if it's not null and alive.
        private static bool NoThreadInMp() => !MP.IsInMultiplayer;
        
        // Prevent logging that a thread was unavailable if we specifically disable their usage.
        // private static bool RedirectThreadedPathfindingToNonThreaded(object __instance)
        // {
        //     if (!MP.IsInMultiplayer)
        //         return true;
        //
        //     trySetNewPathDelayedMethod(__instance);
        //     return false;
        // }

        #endregion

        #region ITabs

        private static bool PreHandleDragEvent(ref Pawn ___draggedPawn, Pawn ___hoveringOverPawn, IThingHolder ___transferToHolder)
        {
            if (!MP.IsInMultiplayer || MP.IsExecutingSyncCommand)
                return true;

            // If the method wasn't going to handle the event, just cancel the method execution
            if (Event.current.type != EventType.MouseUp || Event.current.button != 0)
                return false;

            // If the method was going to handle the event, we're going to sync it instead
            SyncedHandleDragEvent(___draggedPawn, ___hoveringOverPawn, ___transferToHolder);
            // If the event was handled, the dragged pawn is set to null.
            ___draggedPawn = null;
            return false;
        }

        private static void SyncedHandleDragEvent(Pawn dragged, Pawn hovering, IThingHolder holder)
        {
            // Get references to the fields
            ref var draggedPawn = ref passengerTabDraggedPawnField();
            ref var hoveringPawn = ref passengerTabHoveringOverPawnField();
            ref var transferToHolder = ref passengerTabTransferToHolderField();
            // Get current values as temporary values
            var currentDraggedPawn = draggedPawn;
            var currentHoveringPawn = hoveringPawn;
            var currentTransferToHolder = transferToHolder;
            var currentEvent = Event.current;

            try
            {
                // Change fields to the synced values
                draggedPawn = dragged;
                hoveringPawn = hovering;
                transferToHolder = holder;
                Event.current = new Event
                {
                    type = EventType.MouseUp,
                    button = 0,
                };
                // Run the original method with values we synced
                passengerTabHandleDragEventMethod(null);
            }
            finally
            {
                // Restore fields to their previous values
                draggedPawn = currentDraggedPawn;
                hoveringPawn = currentHoveringPawn;
                transferToHolder = currentTransferToHolder;
                Event.current = currentEvent;
            }
        }

        #endregion

        #region Turrets

        // In almost every situation that `SetTarget` is called, we want to cancel it in interface.
        // This is due to the `SetTarget` being called with intent to make the turret start following
        // the current mouse position, which we don't want and it's a feature we've disabled in MP.
        // There's however only 1 situation that it's not the case, this will handle it.
        // The situation is pressing the gizmo's cancel button to stop targetting att all.
        private static bool CancelTurretSetTargetSync() => shouldSyncInInterface || !PatchingUtilities.ShouldCancel;

        private static void SyncSetTarget(object turret, LocalTargetInfo target)
        {
            try
            {
                shouldSyncInInterface = true;
                setTargetTurretMethod(turret, target);
            }
            finally
            {
                shouldSyncInInterface = false;
            }
        }
        
        private static IEnumerable<CodeInstruction> ReplaceSetTargetCall(IEnumerable<CodeInstruction> instr, MethodBase baseMethod)
        {
            var target = AccessTools.DeclaredMethod("Vehicles.VehicleTurret:SetTarget");
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
        private static void PreSetTurretTarget(object ___turret)
        {
            if (!MP.IsInMultiplayer)
                return;


            // Only needed for the first shot. All the other ones would be fine without it.
            setTargetTurretMethod(___turret, LocalTargetInfo.Invalid);
            // Actually force the turret to recalculate stuff.
            alignToAngleTurretMethod(___turret, 0f);
        }

        #endregion

        #region Dialogs

        #region Load cargo dialog

        // private static void PreDrawLoadCargo(ref bool __state)
        // {
        //     if (!MP.IsInMultiplayer)
        //         return;
        //
        //     isDrawingDialog = true;
        //
        //     // No sync worker for ModSettings, just do it manually
        //     var settings = vehicleModSettingsField();
        //     __state = settingsShowAllCargoItemsField(settings);
        // }
        //
        // private static void PostDrawLoadCargo(ref bool __state)
        // {
        //     if (!MP.IsInMultiplayer)
        //         return;
        //
        //     isDrawingDialog = false;
        //
        //     var settings = vehicleModSettingsField();
        //     ref var value = ref settingsShowAllCargoItemsField(settings);
        //     if (value != __state)
        //         SyncedSetShowAllCargoItems(value);
        //     value = __state;
        // }
        //
        // private static void SyncedSetShowAllCargoItems(bool value)
        // {
        //     var settings = vehicleModSettingsField();
        //     settingsShowAllCargoItemsField(settings) = value;
        //
        //     var dialog = Find.WindowStack.Windows.FirstOrDefault(x => x.GetType() == loadCargoDialogType);
        //     if (dialog != null)
        //         loadCargoRecacheMethod(dialog);
        // }
        //
        // private static bool PreLoadCargoRecache() => !MP.IsInMultiplayer || !isDrawingDialog;

        #endregion

        #endregion

        #region SyncWorkers

        private static void NoSync(SyncWorker sync, ref object target)
        {
        }

        private static void SyncVehicleComponent(SyncWorker sync, ref object comp)
        {
            if (sync.isWriting)
            {
                var vehiclePawn = vehicleComponentParentField(comp);
                var statHandler = vehiclePawnStatHandlerField(vehiclePawn);
                var compList = vehicleStatHandlerComponentsField(statHandler);

                var compIndex = compList.IndexOf(comp);
                sync.Write(compIndex);
                if (compIndex >= 0)
                    sync.Write(vehicleComponentParentField(comp));
                else
                    Log.Error($"Trying to write a VehicleComponent, but the vehicle this component belongs to does not contain it. Vehicle={vehiclePawn}, compCount={compList.Count}");
            }
            else
            {
                var compIndex = sync.Read<int>();

                if (compIndex >= 0)
                {
                    var vehiclePawn = sync.Read<Pawn>();
                    var statHandler = vehiclePawnStatHandlerField(vehiclePawn);
                    var compList = vehicleStatHandlerComponentsField(statHandler);

                    if (compIndex < compList.Count)
                        comp = compList[compIndex];
                    else
                        Log.Error($"Trying to read VehicleComponent, but we've received component with index out of range. Vehicle={vehiclePawn}, index={compIndex}, compCount={compList.Count}");
                }
            }
        }

        private static void SyncVehicleTurret(SyncWorker sync, ref object turret)
        {
            if (sync.isWriting)
            {
                sync.Write(vehicleTurretUniqueIdField(turret));
                sync.Write(vehicleTurretParentPawnField(turret));
            }
            else
            {
                var id = sync.Read<int>();
                var vehiclePawn = sync.Read<Pawn>();

                if (id == -1)
                {
                    Log.Error($"Trying to read VehicleTurret, received an uninitialized turret. Vehicle={vehiclePawn}, id=-1");
                    return;
                }
                if (id < -1)
                {
                    Log.Warning($"Trying to read VehicleTurret, received a turret with local ID. This shouldn't happen. Vehicle={vehiclePawn}, id={id}");
                    return;
                }

                if (vehiclePawn == null)
                {
                    Log.Error($"Trying to read VehicleTurret, received a null parent vehicle. id={id}");
                    return;
                }

                var compObject = vehiclePawnTurretCompGetter(vehiclePawn);
                if (compObject == null)
                {
                    Log.Error($"Trying to read VehicleTurret, the vehicle doesn't contain CompVehicleTurrets. Vehicle={vehiclePawn}, id={id}");
                    return;
                }
                if (compObject is not ThingComp comp)
                {
                    Log.Error($"Trying to read VehicleTurret, but the CompVehicleTurrets is not a {nameof(ThingComp)}. Vehicle={vehiclePawn}, id={id}, comp={compObject}");
                    return;
                }

                var turretList = turretCompTurretListField(comp);
                if (turretList == null)
                {
                    Log.Error($"Trying to read VehicleTurret, but the CompVehicleTurrets contains a null list of turrets. Vehicle={vehiclePawn}, id={id}, comp={compObject}");
                    return;
                }

                turret = turretList.Cast<object>().FirstOrDefault(t => vehicleTurretUniqueIdField(t) == id);
                if (turret == null)
                    Log.Error($"Trying to read VehicleTurret, but the list of turrets does not contain the turret we're trying to read. Vehicle={vehiclePawn}, id={id}, comp={compObject}");
            }
        }

        private static void SyncVehicleIgnitionController(SyncWorker sync, ref object controller)
        {
            if (sync.isWriting)
            {
                sync.Write(vehicleIgnitionControllerParentField(controller));
            }
            else
            {
                var vehiclePawn = sync.Read<Pawn>();
                controller = vehiclePawnIgnitionControllerField(vehiclePawn);
                if (controller == null)
                    Log.Error($"Trying to read Vehicle_IgnitionController, but the vehicle is missing it. Vehicle={vehiclePawn}");
            }
        }

        private static void SyncVehicleHandler(SyncWorker sync, ref object handler)
        {
            if (sync.isWriting)
            {
                sync.Write(vehicleHandlerUniqueIdField(handler));
                sync.Write(vehicleHandlerParentField(handler));
            }
            else
            {
                var id = sync.Read<int>();
                var vehiclePawn = sync.Read<Pawn>();

                if (id == -1)
                {
                    Log.Error($"Trying to read VehicleHandler, received an uninitialized handler. Vehicle={vehiclePawn}, id=-1");
                    return;
                }
                if (id < -1)
                {
                    Log.Warning($"Trying to read VehicleHandler, received handler with local ID. This shouldn't happen. Vehicle={vehiclePawn}, id={id}");
                    return;
                }

                if (vehiclePawn == null)
                {
                    Log.Error($"Trying to read VehicleHandler, received a null parent vehicle. id={id}");
                    return;
                }

                var handlersList = vehiclePawnHandlersList(vehiclePawn);
                if (handlersList == null)
                {
                    Log.Error($"Trying to read VehicleHandler, but the vehicle contains a null list of handlers. Vehicle={vehiclePawn}, id={id}");
                    return;
                }

                handler = handlersList.Cast<object>().FirstOrDefault(t => vehicleHandlerUniqueIdField(t) == id);
                if (handler == null)
                    Log.Error($"Trying to read VehicleHandler, but the list of handlers does not contain the handler we're trying to read. Vehicle={vehiclePawn}, id={id}");
            }
        }

        private static void SyncCommandTurret(SyncWorker sync, ref Command command)
        {
            if (sync.isWriting)
            {
                // We could technically just sync the turret and use its vehicle field.
                // Syncing it anyway in case some mods do weird stuff with this.
                sync.Write(turretCommandVehicleField(command));
                
                // Not syncing other fields, as it doesn't seem they're needed.
            }
            else
            {
                // isImplicit: true
                // If any subclass ever introduces a constructor we'll need to replace it with call to `FormatterServices.GetUninitializedObject(type)`

                turretCommandVehicleField(command) = sync.Read<Pawn>();
            }

            SyncVehicleTurret(sync, ref turretCommandTurretField(command));
        }

        #endregion
    }
}