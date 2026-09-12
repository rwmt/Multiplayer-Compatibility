using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Modern History Menu by Astryl</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3742925193"/>
    [MpCompatFor("astryl.modernhistorymenu")]
    [MpCompatFor("astryl.ModernHistoryMenu")]
    internal class ModernHistoryMenu
    {
        public ModernHistoryMenu(ModContentPack mod) => LongEventHandler.ExecuteWhenFinished(LatePatch);

        private static void LatePatch()
        {
            // 1. HistoryAutoRecorderWorker_RealPlaytimeHours:PullRecord
            var pullRecordMethod = AccessTools.Method("HistoryExpanded.HistoryAutoRecorderWorker_RealPlaytimeHours:PullRecord");
            if (pullRecordMethod != null)
            {
                MpCompat.harmony.Patch(
                    pullRecordMethod,
                    prefix: new HarmonyMethod(typeof(ModernHistoryMenu), nameof(PullRecordPrefix)));
            }

            // 2. GameComponent_SkillHistory:SampleAll (transpile RealPlayTimeInteracting to deterministic in-game time)
            var sampleAllMethod = AccessTools.Method("HistoryExpanded.GameComponent_SkillHistory:SampleAll");
            if (sampleAllMethod != null)
            {
                MpCompat.harmony.Patch(
                    sampleAllMethod,
                    transpiler: new HarmonyMethod(typeof(ModernHistoryMenu), nameof(SampleAllTranspiler)));
            }

            // 3. Sync methods on GameComponent_SkillHistory that modify serialized state
            var compType = AccessTools.TypeByName("HistoryExpanded.GameComponent_SkillHistory");
            if (compType != null)
            {
                MP.RegisterSyncMethod(compType, "TogglePin");
                MP.RegisterSyncMethod(compType, "ToggleFavoriteGraph");
                MP.RegisterSyncMethod(compType, "SetGraphOrder");
            }
        }

        private static bool PullRecordPrefix(ref float __result)
        {
            if (MP.IsInMultiplayer)
            {
                // Equivalent 1x speed hours (60 ticks/sec * 3600 sec/hr = 216000 ticks/hr)
                __result = (float)Find.TickManager.TicksGame / 216000f;
                return false;
            }
            return true;
        }

        public static float GetDeterministicRealPlayTime(GameInfo gameInfo)
        {
            if (MP.IsInMultiplayer)
            {
                // Return playtime in seconds derived deterministically from game ticks
                return (float)Find.TickManager.TicksGame / 60f;
            }
            return gameInfo.RealPlayTimeInteracting;
        }

        private static IEnumerable<CodeInstruction> SampleAllTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var targetGetter = AccessTools.PropertyGetter(typeof(GameInfo), nameof(GameInfo.RealPlayTimeInteracting));
            var replacementMethod = AccessTools.Method(typeof(ModernHistoryMenu), nameof(GetDeterministicRealPlayTime));

            foreach (var instr in instructions)
            {
                if (instr.Calls(targetGetter))
                {
                    yield return new CodeInstruction(OpCodes.Call, replacementMethod);
                }
                else
                {
                    yield return instr;
                }
            }
        }
    }
}
