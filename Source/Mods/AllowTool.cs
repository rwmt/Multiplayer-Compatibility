using System;
using System.Linq;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Multiplayer.Compat
{
    /// <summary>Allow Tool by UnlimitedHugs</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=761421485"/>
    /// <see href="https://github.com/UnlimitedHugs/RimworldAllowTool"/>
    [MpCompatFor("UnlimitedHugs.AllowTool")]
    public class AllowTool
    {
        // Drafted hunt
        private static FastInvokeHandler partyHuntWorldSettingsGetter;

        // Right-click designator options
        private static FastInvokeHandler activationResultShowMessage;

        // WorkGiver_FinishOff
        private static FastInvokeHandler createWorkGiverInstance;
        private static AccessTools.FieldRef<object, Job> innerClassJobField;
        private static AccessTools.FieldRef<object, WorkGiver_Scanner> innerClassWorkGiverField;
        private static AccessTools.FieldRef<object, object> innerClassParentField;
        private static Type innerParentType;
        private static AccessTools.FieldRef<object, Pawn> parentInnerClassPawnField;
        private static AccessTools.FieldRef<object, Thing> parentInnerClassTargetField;

        // MP
        private static AccessTools.FieldRef<object, bool> dontSyncField;

        // Override for shift/control key press and visible map rect
        private static bool? shiftHeldState = null;
        private static bool? controlHeldState = null;
        private static CellRect? visibleMapRectState = null;

        public AllowTool(ModContentPack mod)
        {
            // MP stuff
            {
                dontSyncField = AccessTools.FieldRefAccess<bool>("Multiplayer.Client.Multiplayer:dontSync");
            }

            // Gizmos
            {
                // Drafted hunt
                var getter = AccessTools.PropertyGetter("AllowTool.Command_PartyHunt:WorldSettings");
                partyHuntWorldSettingsGetter = MethodInvoker.GetHandler(getter);

                var type = AccessTools.TypeByName("AllowTool.Settings.PartyHuntSettings");
                MP.RegisterSyncMethod(type, "AutoFinishOff");
                MP.RegisterSyncMethod(type, "HuntDesignatedOnly");
                MP.RegisterSyncMethod(type, "UnforbidDrops");
                MP.RegisterSyncMethod(type, "TogglePawnPartyHunting");
                MP.RegisterSyncWorker<object>(SyncDraftedHuntSettings, type);
            }

            // Right-click designator options
            {
                var type = AccessTools.TypeByName("AllowTool.Context.BaseContextMenuEntry");
                // All of them either handle client-only interactions, or use camera position (which would break) but are synced indirectly through CompForbiddable.
                var excludedTypes = new[]
                {
                    AccessTools.TypeByName("AllowTool.Context.MenuEntry_SelectSimilarAll"),
                    AccessTools.TypeByName("AllowTool.Context.MenuEntry_SelectSimilarHome"),
                    AccessTools.TypeByName("AllowTool.Context.MenuEntry_SelectSimilarVisible"),
                };

                foreach (var subtype in type.AllSubclasses().Concat(type).Except(excludedTypes))
                {
                    // Alternatively we could sync ActivateAndHandleResult in the parent type.
                    // This would require us using `dontSync` field in MP, which I'm trying to
                    // avoid wherever possible.
                    var method = AccessTools.DeclaredMethod(subtype, "Activate");
                    if (method != null)
                    {
                        MP.RegisterSyncMethod(method)
                            .SetContext(SyncContext.MapSelected) // Some of the types will require selected thing on map
                            .SetPostInvoke(ClearTemporaryState);
                        MpCompat.harmony.Patch(method, postfix: new HarmonyMethod(typeof(AllowTool), nameof(ShowActivationResultSelf)));
                    }
                }

                // None of them have any constructor arguments, and none of them story any relevant code - we only care for the code they call.
                // We also sync the state of shift/control keys held at the time of syncing, as those are used by some of those methods.
                MP.RegisterSyncWorker<object>(ShiftControlStateOnlySyncWorker, type, true, true);

                // Special cases - require the current visible camera position to work properly
                var usesCameraTypes = new[]
                {
                    AccessTools.TypeByName("AllowTool.Context.MenuEntry_AllowVisible"),
                    AccessTools.TypeByName("AllowTool.Context.MenuEntry_ForbidVisible"),
                    AccessTools.TypeByName("AllowTool.Context.MenuEntry_HaulUrgentVisible"),
                };
                foreach (var usesCameraType in usesCameraTypes)
                    MP.RegisterSyncWorker<object>(ShiftControlCameraStateOnlySyncWorker, usesCameraType, shouldConstruct: true);

                // Since we're syncing Activate instead of ActivateAndHandleResult, we need to manually display success/failure message
                activationResultShowMessage = MethodInvoker.GetHandler(AccessTools.Method("AllowTool.Context.ActivationResult:ShowMessage"));
            }

            // Designators
            {
                // AllowAll doesn't use the normal designator methods, sync the method directly.
                // Not necessary, but we're syncing the AllowAllTheThings as otherwise we'll end up with a spam of synced commands for CompForbiddable
                var type = AccessTools.TypeByName("AllowTool.Designator_AllowAll");
                MP.RegisterSyncMethod(AccessTools.Method(type, "AllowAllTheThings")).SetPostInvoke(ClearTemporaryState);
                MP.RegisterSyncWorker<object>(ShiftControlStateOnlySyncWorker, type, shouldConstruct: true);

                // Remove syncing from select similar designator (breaks the designator otherwise)
                MpCompat.harmony.Patch(AccessTools.DeclaredMethod("AllowTool.Designator_SelectSimilar:DesignateMultiCell"),
                    prefix: new HarmonyMethod(typeof(AllowTool), nameof(SelectSimilarPrefix)),
                    finalizer: new HarmonyMethod(typeof(AllowTool), nameof(SyncAgainFinalizer)));
            }

            // Recache haul urgently deterministically (currently uses Time.unscaledTime)
            {
                var type = AccessTools.TypeByName("AllowTool.HaulUrgentlyCacheHandler");
                MpCompat.harmony.Patch(AccessTools.Method(type, "RecacheIfNeeded"),
                    prefix: new HarmonyMethod(typeof(AllowTool), nameof(DeterministicallyHandleReCaching)));
                type = AccessTools.Inner(type, "ThingsCacheEntry");
                MpCompat.harmony.Patch(AccessTools.Method(type, "IsValid"),
                    prefix: new HarmonyMethod(typeof(AllowTool), nameof(ScaleReCachingTimerToTickSpeed)));
            }

            // Finish off from right-click.
            // Right now, syncing fails as syncing the job as IExposable fails to sync the verb to use.
            // I tried a couple of approaches  (like replicating their code here), and ended up just syncing the inner classes.
            {
                var type = AccessTools.TypeByName("AllowTool.WorkGiver_FinishOff");
                createWorkGiverInstance = MethodInvoker.GetHandler(AccessTools.Method(type, "CreateInstance"));

                var method = MpMethodUtil.GetLambda(type, "InjectThingFloatOptionIfNeeded", lambdaOrdinal: 0);
                type = method.DeclaringType;
                innerClassJobField = AccessTools.FieldRefAccess<Job>(type, "job");
                innerClassWorkGiverField = AccessTools.FieldRefAccess<WorkGiver_Scanner>(type, "giver");
                var parentField = AccessTools.GetDeclaredFields(type).FirstOrDefault(t => t.FieldType.Name.StartsWith("<>c__DisplayClass"));
                innerClassParentField = AccessTools.FieldRefAccess<object, object>(parentField);

                MP.RegisterSyncMethod(method);
                MP.RegisterSyncWorker<object>(SyncFinishOffInnerClass, type, shouldConstruct: true);

                innerParentType = parentField.FieldType;
                parentInnerClassPawnField = AccessTools.FieldRefAccess<Pawn>(innerParentType, "selPawn");
                parentInnerClassTargetField = AccessTools.FieldRefAccess<Thing>(innerParentType, "target");
            }

            // Override current state returned from HugsLib and AllowTool methods
            {
                var type = AccessTools.TypeByName("HugsLib.Utils.HugsLibUtility");
                MpCompat.harmony.Patch(AccessTools.PropertyGetter(type, "ShiftIsHeld"),
                    prefix: new HarmonyMethod(typeof(AllowTool), nameof(ReplaceShiftHeld)));
                MpCompat.harmony.Patch(AccessTools.PropertyGetter(type, "ControlIsHeld"),
                    prefix: new HarmonyMethod(typeof(AllowTool), nameof(ReplaceControlHeld)));

                MpCompat.harmony.Patch(AccessTools.Method("AllowTool.AllowToolUtility:GetVisibleMapRect"),
                    prefix: new HarmonyMethod(typeof(AllowTool), nameof(ReplaceCurrentMapRect)));
            }

            LongEventHandler.ExecuteWhenFinished(LatePatch);
        }

        private static void LatePatch()
        {
            // Strip mine
            MP.RegisterSyncMethod(AccessTools.Method("AllowTool.Designator_StripMine:DesignateCells"));
        }

        private static void ClearTemporaryState(object instance, object[] args)
        {
            shiftHeldState = null;
            controlHeldState = null;
            visibleMapRectState = null;
        }

        // By syncing the Activate method, we'll be returning the default (null) value. The base method will be
        // unable to show success/failure message because of that. We're displaying it for the person who used it.
        private static void ShowActivationResultSelf(object __result)
        {
            if (MP.IsInMultiplayer && MP.IsExecutingSyncCommandIssuedBySelf && __result != null)
                activationResultShowMessage(__result);
        }

        // MP will try to automatically sync the select similar designator, so we tell MP to not sync this one
        private static void SelectSimilarPrefix(ref bool __state)
        {
            if (!MP.IsInMultiplayer || dontSyncField()) return;

            __state = true;
            dontSyncField() = true;
        }

        // private static void ContextMenuEntryPrefix(object __instance, ref bool __state)
        // {
        //     // Prevent syncing of client-only menu entry types
        //     if (!MP.IsInMultiplayer || dontSyncField() || !unsyncedMenuEntries.Contains(__instance.GetType())) return;
        //
        //     __state = true;
        //     dontSyncField() = true;
        // }

        // We need to make sure the value is set back to false, or basically everything in MP will break
        private static void SyncAgainFinalizer(bool __state)
        {
            if (__state)
                dontSyncField() = false;
        }

        // Some designators use the current state of shift/control key, so we need to sync those as well
        private static bool ReplaceShiftHeld(ref bool __result)
        {
            if (shiftHeldState == null)
                return true;

            __result = shiftHeldState.Value;
            return false;
        }

        private static bool ReplaceControlHeld(ref bool __result)
        {
            if (controlHeldState == null)
                return true;

            __result = controlHeldState.Value;
            return false;
        }

        private static bool ReplaceCurrentMapRect(ref CellRect __result)
        {
            if (visibleMapRectState == null)
                return true;

            __result = visibleMapRectState.Value;
            return false;
        }

        private static void DeterministicallyHandleReCaching(ref float currentTime)
        {
            if (MP.IsInMultiplayer)
                currentTime = Find.TickManager.TicksGame;
        }

        private static bool ScaleReCachingTimerToTickSpeed(float currentTime, float ___createdTime, ref bool __result)
        {
            if (!MP.IsInMultiplayer)
                return true;

            var mult = Find.TickManager.TickRateMultiplier;
            if (mult > 0)
                mult = 0.15f;
            // The original method re-caches once a second. We check every 60 ticks multiplied by tick rate multiplier, so it will end up roughly every second no matter the game speed.
            // Also handle the situation of the game being paused by assuming the multiplier is 0.15 (small value to potentially force re-cache)
            __result = ___createdTime > 0 && ___createdTime < currentTime + (60 * mult);
            return false;
        }

        private static void SyncDraftedHuntSettings(SyncWorker sync, ref object settings)
        {
            if (!sync.isWriting)
                settings = partyHuntWorldSettingsGetter(null);
        }

        private static void ShiftControlStateOnlySyncWorker(SyncWorker sync, ref object _)
            => SyncShiftControlState(sync);

        private static void ShiftControlCameraStateOnlySyncWorker(SyncWorker sync, ref object _)
        {
            SyncShiftControlState(sync);
            SyncCameraState(sync);
        }

        private static void SyncShiftControlState(SyncWorker sync)
        {
            if (sync.isWriting)
            {
                sync.Write(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
                sync.Write(Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) || Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand));
            }
            else
            {
                shiftHeldState = sync.Read<bool>();
                controlHeldState = sync.Read<bool>();
            }
        }

        private static void SyncCameraState(SyncWorker sync)
        {
            // We should probably add a CellRect sync worker to MP.
            // The mod is using its own method for getting visible map rect instead of CameraDriver.CurrentViewRect, which is a bit pointless.
            if (sync.isWriting)
            {
                var rect = Find.CameraDriver.CurrentViewRect;

                sync.Write(rect.minX);
                sync.Write(rect.maxX);
                sync.Write(rect.minZ);
                sync.Write(rect.maxZ);
            }
            else
            {
                visibleMapRectState = new CellRect
                {
                    minX = sync.Read<int>(),
                    maxX = sync.Read<int>(),
                    minZ = sync.Read<int>(),
                    maxZ = sync.Read<int>(),
                };
            }
        }

        private static void SyncFinishOffInnerClass(SyncWorker sync, ref object obj)
        {
            if (sync.isWriting)
            {
                var parent = innerClassParentField(obj);
                sync.Write(parentInnerClassPawnField(parent));
                sync.Write(parentInnerClassTargetField(parent));
            }
            else
            {
                var parent = Activator.CreateInstance(innerParentType);
                
                var pawn = sync.Read<Pawn>();
                var target = sync.Read<Thing>();
                parentInnerClassPawnField(parent) = pawn;
                parentInnerClassTargetField(parent) = target;

                innerClassParentField(obj) = parent;

                var workGiver = (WorkGiver_Scanner)createWorkGiverInstance(null);
                innerClassWorkGiverField(obj) = workGiver;
                innerClassJobField(obj) = workGiver.JobOnThing(pawn, target, true);
            }
        }
    }
}