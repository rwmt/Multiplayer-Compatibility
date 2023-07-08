using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Multiplayer.API;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>RimNauts 2 by sindre0830</summary>
    /// <remarks>This compat assumes Universum is compatible, and at this point in time - it seems it is.</remarks>
    /// <see href="https://github.com/RimNauts/RimNauts2"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2880599514"/>
    [MpCompatFor("sindre0830.rimnauts2")]
    public class RimNauts2
    {
        #region Fields

        private static bool isTickingComponent = false;
        private static bool isCameraJumpAllowed = true;

        // RenderingManager
        private static AccessTools.FieldRef<Vector3> renderingManagerCenterField;
        private static FastInvokeHandler renderingManagerUpdateMethod;
        private static FastInvokeHandler renderingManagerGetFrameData;

        #endregion

        #region Main patch

        public RimNauts2(ModContentPack mod)
        {
            // Both RimNauts2 and Universum do a lot of operations using camera and camera position.
            // It looks like it's safe and mostly done to render stuff on world map, on space maps, etc.

            LongEventHandler.ExecuteWhenFinished(LatePatch);

            // RNG
            {
                // Possible not 100% needed, worth making sure stuff has correct values anyway.
                var type = AccessTools.TypeByName("RimNauts2.World.TypeExtension");
                PatchingUtilities.PatchUnityRand(AccessTools.DeclaredMethod(type, "rotation_angle"));
                PatchingUtilities.PatchUnityRand(AccessTools.DeclaredMethod(type, "transformation_rotation_angle"));
            }

            // Gizmos
            {
                // A bit messy with dev mode here. Some of those gizmos have some extra checks to stop them from being triggered
                // if certain conditions are met, but if god mode is enabled then those checks are skipped and the gizmo
                // is treated as a "dev" one. (SatelliteDish, TransportPod)
                // This could be worked around somewhat by catching `CompGetGizmosExtra` results and disabling them if
                // they are the dev mode gizmos (check if label ends with `(dev)`. But to do this, we'd most likely need
                // the API update to be able to check if the current player has access to dev mode as well, as we wouldn't
                // want to fully prevent the dev mode calls from being disabled.

                // ThingComps
                MP.RegisterSyncMethod(AccessTools.DeclaredMethod("RimNauts2.Things.SatelliteDish:generate_moon"));
                MP.RegisterSyncMethod(AccessTools.DeclaredMethod("RimNauts2.Things.TransportPod:launch"));
                MP.RegisterSyncMethod(AccessTools.DeclaredMethod("RimNauts2.Things.Comps.Mode:change_mode"));
                MP.RegisterSyncMethod(AccessTools.DeclaredMethod("RimNauts2.Things.Comps.Mode:order_fire"));
                // TODO: If this ever allows targeting anything besides cells, it'll cause issues due to trying to sync 2 maps.
                // TODO: Once we update the API to get sync transformers we should apply it to the LocalTargetInfo to save the headache in the future.
                MP.RegisterSyncMethod(AccessTools.DeclaredMethod("RimNauts2.Things.Comps.Targeter:chose_cell_target"));

                // WorldObjectComps
                MP.RegisterSyncMethod(AccessTools.DeclaredMethod("RimNauts2.World.Comps.DestroyObjectHolder:destroy_object")).SetDebugOnly();
                MP.RegisterSyncMethod(AccessTools.DeclaredMethod("RimNauts2.World.Comps.GenerateObjectMap:generate_map")).SetDebugOnly();
                MP.RegisterSyncMethod(AccessTools.DeclaredMethod("RimNauts2.World.Comps.RandomizeObjectHolder:randomize_object")).SetDebugOnly();

                // Caravan
                // Settle on the mood/mine the asteroid
                MP.RegisterSyncMethod(AccessTools.DeclaredMethod("RimNauts2.World.Patch.SettleInEmptyTileUtility_SettleCommand:settle"));
            }

            // Determinism
            {
                // TravelingTransportPods_TraveledPctStepPerTick:Postfix uses RenderingManager.center, which causes issues as
                // it may be a different value for each player, depending on where and if they have ever observed the world map.
                // The value is used to calculate the position of transport pods and when they arrive.
                MpCompat.harmony.Patch(AccessTools.DeclaredMethod("RimNauts2.World.Patch.TravelingTransportPods_TraveledPctStepPerTick:Postfix"),
                    prefix: new HarmonyMethod(typeof(RimNauts2), nameof(PreTraveledPctStepPerTickPatch)),
                    postfix: new HarmonyMethod(typeof(RimNauts2), nameof(PostTraveledPctStepPerTickPatch)));
            }

            // Feedback
            {
                // Calls to CameraJumper.TryJump are called for every player, which is undesirable.
                MpCompat.harmony.Patch(AccessTools.DeclaredMethod("RimNauts2.Things.Comps.Targeter:chose_cell_target"),
                    prefix: new HarmonyMethod(typeof(RimNauts2), nameof(PreJumpSetup)),
                    transpiler: new HarmonyMethod(typeof(RimNauts2), nameof(ReplaceTryJumpTranspiler)));

                var type = AccessTools.TypeByName("RimNauts2.World.Patch.SettleInEmptyTileUtility_SettleCommand");
                const string methodName = "settle";
                var method = AccessTools.DeclaredMethod(type, methodName);
                // Synced method, so we can check if executing command issued by self 
                MpCompat.harmony.Patch(method,
                    prefix: new HarmonyMethod(typeof(RimNauts2), nameof(PreJumpSetup)));
                // The actual method that handles camera jumping
                MpCompat.harmony.Patch(MpMethodUtil.GetLambda(type, methodName),
                    transpiler: new HarmonyMethod(typeof(RimNauts2), nameof(ReplaceTryJumpTranspiler)));
            }
        }

        private static void LatePatch()
        {
            // Gizmos
            {
                // Buildings
                // Place cargo pod blueprint
                MpCompat.RegisterLambdaMethod("RimNauts2.Things.Building.PodLauncher", "GetGizmos", 0);
            }

            // Determinism
            {
                // This patch basically moves `RenderingManager:update` to `RenderingManager:GameComponentTick`.
                // `RenderingManager:update` is normally called from `RenderingManager:GameComponentUpdate`.

                var type = AccessTools.TypeByName("RimNauts2.World.RenderingManager");
                renderingManagerCenterField = AccessTools.StaticFieldRefAccess<Vector3>(AccessTools.DeclaredField(type, "center"));
                renderingManagerGetFrameData = MethodInvoker.GetHandler(AccessTools.DeclaredMethod(type, "get_frame_data"));

                var method = AccessTools.DeclaredMethod(type, "update");
                renderingManagerUpdateMethod = MethodInvoker.GetHandler(method);
                // Only let the update method run during ticking. It handles stuff like positions of objects (very important when dealing
                // with drop pods and the like). If enabled, it does part of the job using Parallel.For - however, it seems like it's safe.
                // This does have a slight issue - the method updates the positions of labels and object trails. This causes issue when paused
                // and moving camera/opening the world map, where the labels/trails will be in incorrect position until unpaused.
                MpCompat.harmony.Patch(method, prefix: new HarmonyMethod(typeof(RimNauts2), nameof(CancelOutsideOfTicking)));

                // Call the update method during ticking
                MpCompat.harmony.Patch(AccessTools.DeclaredMethod(type, "GameComponentTick"),
                    postfix: new HarmonyMethod(typeof(RimNauts2), nameof(PostTick)));
            }
        }

        #endregion

        #region Move updates into ticking

        private static bool CancelOutsideOfTicking() => !MP.IsInMultiplayer || isTickingComponent;

        private static void PostTick(GameComponent __instance, ref bool ___unpaused, ref bool ___force_update)
        {
            if (!MP.IsInMultiplayer)
                return;

            // Cleanup camera jumper patch
            isCameraJumpAllowed = true;

            isTickingComponent = true;

            try
            {
                renderingManagerGetFrameData(__instance);
                // Make sure those 2 are true when we run update method so the mod always updates
                // the positions of all the objects, even if not looked at/the world map not open.
                ___unpaused = ___force_update = true;
                renderingManagerUpdateMethod(__instance);
            }
            finally
            {
                isTickingComponent = false;
            }
        }

        #endregion

        #region Transport Pod Patch

        private static void PreTraveledPctStepPerTickPatch(ref Vector3? __state)
        {
            if (MP.IsInMultiplayer)
            {
                __state = renderingManagerCenterField();
                renderingManagerCenterField() = Vector3.zero;
            }
        }

        private static void PostTraveledPctStepPerTickPatch(Vector3? __state)
        {
            if (__state.HasValue)
                renderingManagerCenterField() = __state.Value;
        }
        
        #endregion

        #region Camera Jumper Patch

        private static void PreJumpSetup()
        {
            // Jump only outside of MP, when not executing commands, or executing self issued command
            isCameraJumpAllowed = !MP.IsInMultiplayer || !MP.IsExecutingSyncCommand || MP.IsExecutingSyncCommandIssuedBySelf;
        }

        private static void ReplacedTryJump(GlobalTargetInfo target, CameraJumper.MovementMode mode = CameraJumper.MovementMode.Pan)
        {
            // We need PreJumpSetup to set this value, as TryJump can be called outside of synced command (LongEventHandler.QueueLongEvent)
            if (isCameraJumpAllowed)
                CameraJumper.TryJump(target, mode);
            // Cleanup the value
            else
                isCameraJumpAllowed = true;
        }

        private static IEnumerable<CodeInstruction> ReplaceTryJumpTranspiler(IEnumerable<CodeInstruction> instr, MethodBase baseMethod)
        {
            var target = AccessTools.DeclaredMethod(
                typeof(CameraJumper), 
                nameof(CameraJumper.TryJump), 
                new []{ typeof(GlobalTargetInfo), typeof(CameraJumper.MovementMode) });
            var replacement = AccessTools.DeclaredMethod(
                typeof(RimNauts2),
                nameof(ReplacedTryJump));

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
                Log.Warning($"Patched incorrect number of TryJump calls (patched {replacedCount}, expected {expected}) for method {name}");
            }
        }

        #endregion
    }
}