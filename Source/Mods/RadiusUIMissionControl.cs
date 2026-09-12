using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using UnityEngine;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Radius UI - Mission Control by Astryl</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3788256848"/>
    [MpCompatFor("astryl.RadiusUI.MissionControl")]
    internal class RadiusUIMissionControl
    {
        private static bool inGlobalControlsDraw = false;
        private static Type timeVoteType;
        private static MethodInfo sendTimeVoteMethod;
        private static MethodInfo togglePausedMethod;
        private static PropertyInfo curTimeSpeedUIProp;

        public RadiusUIMissionControl(ModContentPack mod) => LongEventHandler.ExecuteWhenFinished(LatePatch);

        private static void LatePatch()
        {
            var timeControlPatchType = AccessTools.TypeByName("Multiplayer.Client.AsyncTime.TimeControlPatch");
            timeVoteType = AccessTools.TypeByName("Multiplayer.Common.TimeVote");

            if (timeControlPatchType != null && timeVoteType != null)
            {
                sendTimeVoteMethod = AccessTools.Method(timeControlPatchType, "SendTimeVote", new[] { timeVoteType });
                togglePausedMethod = AccessTools.Method(timeControlPatchType, "TogglePaused", new[] { typeof(Nullable<>).MakeGenericType(timeVoteType) });
                curTimeSpeedUIProp = AccessTools.Property(timeControlPatchType, "CurTimeSpeedUI");
            }

            // Track when Mission Control's GlobalControlsRenderer is drawing
            var rendererType = AccessTools.TypeByName("RadiusUI.MissionControl.GlobalControlsRenderer");
            var drawMethod = AccessTools.Method(rendererType, "Draw");
            if (drawMethod != null)
            {
                MpCompat.harmony.Patch(
                    drawMethod,
                    prefix: new HarmonyMethod(typeof(RadiusUIMissionControl), nameof(PreGlobalControlsDraw)),
                    postfix: new HarmonyMethod(typeof(RadiusUIMissionControl), nameof(PostGlobalControlsDraw)),
                    finalizer: new HarmonyMethod(typeof(RadiusUIMissionControl), nameof(PostGlobalControlsDraw)));
            }

            // Intercept time speed mutations during GlobalControlsRenderer.Draw and route them to MP time votes
            var setCurTimeSpeedMethod = AccessTools.PropertySetter(typeof(TickManager), nameof(TickManager.CurTimeSpeed));
            MpCompat.harmony.Patch(
                setCurTimeSpeedMethod,
                prefix: new HarmonyMethod(typeof(RadiusUIMissionControl), nameof(PrefixSetCurTimeSpeed)));

            var togglePausedMethodRef = AccessTools.Method(typeof(TickManager), nameof(TickManager.TogglePaused));
            MpCompat.harmony.Patch(
                togglePausedMethodRef,
                prefix: new HarmonyMethod(typeof(RadiusUIMissionControl), nameof(PrefixTogglePaused)));

            // Provide MP's CurTimeSpeedUI to Mission Control when it queries TickManager
            var getCurTimeSpeedMethod = AccessTools.PropertyGetter(typeof(TickManager), nameof(TickManager.CurTimeSpeed));
            MpCompat.harmony.Patch(
                getCurTimeSpeedMethod,
                prefix: new HarmonyMethod(typeof(RadiusUIMissionControl), nameof(PrefixGetCurTimeSpeed)));

            var getPausedMethod = AccessTools.PropertyGetter(typeof(TickManager), nameof(TickManager.Paused));
            MpCompat.harmony.Patch(
                getPausedMethod,
                prefix: new HarmonyMethod(typeof(RadiusUIMissionControl), nameof(PrefixGetPaused)));

            // Transpile OccasionsTracker.CheckReminders to make reminder evaluation deterministic in MP
            var occasionsTrackerType = AccessTools.TypeByName("RadiusUI.MissionControl.OccasionsTracker");
            var checkRemindersMethod = AccessTools.Method(occasionsTrackerType, "CheckReminders");
            if (checkRemindersMethod != null)
            {
                MpCompat.harmony.Patch(
                    checkRemindersMethod,
                    transpiler: new HarmonyMethod(typeof(RadiusUIMissionControl), nameof(TranspileCheckReminders)));
            }
        }

        private static void PreGlobalControlsDraw() => inGlobalControlsDraw = true;
        private static void PostGlobalControlsDraw() => inGlobalControlsDraw = false;

        private static int GetCurrentTimeVoteInt()
        {
            if (curTimeSpeedUIProp != null)
            {
                try
                {
                    var cur = curTimeSpeedUIProp.GetValue(null);
                    if (cur != null)
                        return Convert.ToInt32(cur);
                }
                catch
                {
                    // Ignore reflection/unboxing failures
                }
            }

            return (int)(Find.TickManager?.curTimeSpeed ?? TimeSpeed.Normal);
        }

        private static bool PrefixGetCurTimeSpeed(ref TimeSpeed __result)
        {
            if (MP.IsInMultiplayer && inGlobalControlsDraw)
            {
                try
                {
                    __result = (TimeSpeed)GetCurrentTimeVoteInt();
                    return false;
                }
                catch
                {
                    return true; // Let vanilla handle it if state is invalid
                }
            }
            return true;
        }

        private static bool PrefixGetPaused(ref bool __result)
        {
            if (MP.IsInMultiplayer && inGlobalControlsDraw)
            {
                try
                {
                    __result = GetCurrentTimeVoteInt() == 0; // TimeSpeed.Paused == 0
                    return false;
                }
                catch
                {
                    return true;
                }
            }
            return true;
        }

        private static bool PrefixSetCurTimeSpeed(TimeSpeed value)
        {
            if (MP.IsInMultiplayer && inGlobalControlsDraw)
            {
                // Never mutate time speed on passive GUI render passes
                if (Event.current != null && (Event.current.type == EventType.Repaint || Event.current.type == EventType.Layout))
                    return false;

                int requestedSpeed = (int)value;
                if (requestedSpeed == GetCurrentTimeVoteInt())
                    return false; // Speed hasn't changed; suppress duplicate votes and sound spam

                SendVote(requestedSpeed);
                return false;
            }
            return true;
        }

        private static bool PrefixTogglePaused()
        {
            if (MP.IsInMultiplayer && inGlobalControlsDraw)
            {
                if (Event.current != null && (Event.current.type == EventType.Repaint || Event.current.type == EventType.Layout))
                    return false;

                SendPauseToggle();
                return false;
            }
            return true;
        }

        private static void SendVote(int speedInt)
        {
            if (sendTimeVoteMethod != null && timeVoteType != null)
            {
                var voteObj = Enum.ToObject(timeVoteType, speedInt);
                sendTimeVoteMethod.Invoke(null, new[] { voteObj });
            }
        }

        private static void SendPauseToggle()
        {
            if (sendTimeVoteMethod != null && togglePausedMethod != null && curTimeSpeedUIProp != null && timeVoteType != null)
            {
                var curSpeed = curTimeSpeedUIProp.GetValue(null);
                var newVote = togglePausedMethod.Invoke(null, new[] { curSpeed });
                sendTimeVoteMethod.Invoke(null, new[] { newVote });
            }
        }

        public static float GetDeterministicReminderTime()
        {
            if (MP.IsInMultiplayer)
            {
                return (Find.TickManager?.TicksGame ?? 0) / 60f;
            }
            return Time.realtimeSinceStartup;
        }

        private static IEnumerable<CodeInstruction> TranspileCheckReminders(IEnumerable<CodeInstruction> instructions)
        {
            var targetMethod = AccessTools.PropertyGetter(typeof(Time), nameof(Time.realtimeSinceStartup));
            var replacementMethod = AccessTools.Method(typeof(RadiusUIMissionControl), nameof(GetDeterministicReminderTime));

            foreach (var inst in instructions)
            {
                if (inst.opcode == OpCodes.Call && inst.operand is MethodInfo m && m == targetMethod)
                {
                    inst.operand = replacementMethod;
                }
                yield return inst;
            }
        }
    }
}