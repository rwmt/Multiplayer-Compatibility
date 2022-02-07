using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using Multiplayer.API;
using UnityEngine;
using Verse;
    
namespace Multiplayer.Compat
{
    /// <summary>Vanilla Storytellers Expanded - Winston Waves by Oskar Potocki, Kikohi</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2734032569"/>
    [MpCompatFor("VanillaStorytellersExpanded.WinstonWave")]
    internal class VanillaStorytellersWinstonWaves
    {
        // Dialogs
        private static Type chooseRewardDialogType;
        private static AccessTools.FieldRef<object, Def> choosenRewardField;

        public VanillaStorytellersWinstonWaves(ModContentPack mod)
        {
            // RNG
            {
                PatchingUtilities.PatchSystemRandCtor("VSEWW.Window_ChooseReward", false);
            }

            // Dialogs
            {
                var type = chooseRewardDialogType = AccessTools.TypeByName("VSEWW.Window_ChooseReward");
                choosenRewardField = AccessTools.FieldRefAccess<Def>(type, "choosenReward");

                type = AccessTools.TypeByName("VSEWW.RewardDef");
                MpCompat.harmony.Patch(AccessTools.Method(type, "DrawCard"),
                    transpiler: new HarmonyMethod(typeof(VanillaStorytellersWinstonWaves), nameof(ReplaceButton)));
                MP.RegisterSyncMethod(typeof(VanillaStorytellersWinstonWaves), nameof(SyncedChooseReward));

                // The window is always visible and its default position covers MP chat button
                // This patch will move it under the button (if MP is on),
                // especially considering that it'll move up on autosave/joinstate creation
                type = AccessTools.TypeByName("VSEWW.Window_WaveCounter");
                MpCompat.harmony.Patch(AccessTools.Method(type, "SetInitialSizeAndPosition"),
                    postfix: new HarmonyMethod(typeof(VanillaStorytellersWinstonWaves), nameof(PostSetInitialSizeAndPosition)));

                MP.RegisterPauseLock(PauseIfDialogOpen);
            }

            LongEventHandler.ExecuteWhenFinished(LatePatch);
        }

        private static void LatePatch()
        {
            var types = new[]
            {
                "VSEWW.MapComponent_Winston:SetNextNormalRaidInfo",
                "VSEWW.MapComponent_Winston:SetNextBossRaidInfo",
                "VSEWW.NextRaidInfo:WavePawns",
                "VSEWW.NextRaidInfo:ChooseAndApplyModifier",
            };

            PatchingUtilities.PatchSystemRand(types, false);
            
            var type = AccessTools.TypeByName("VSEWW.MapComponent_Winston");
            MP.RegisterSyncMethod(type, "ExecuteRaid");
        }

        private static void PostSetInitialSizeAndPosition(Window __instance)
        {
            if (!MP.IsInMultiplayer)
                return;

            // With dev mode and multiplayer debug settings enabled it covers them too,
            // but since the API doesn't let us check if MP debug settings are on,
            // we ignore it.
            __instance.windowRect.y = 30f;
        }

        private static bool InjectedButton(Rect rect, string label, bool drawBackground, bool doMouseoverSound, bool active, Def instance)
        {
            if (Widgets.ButtonText(rect, label, drawBackground, doMouseoverSound, active))
            {
                if (MP.IsInMultiplayer)
                    SyncedChooseReward(instance);
                // Not in MP - return true as the button was clicked
                else
                    return true;
            }

            return false;
        }

        private static void SyncedChooseReward(Def rewardDef)
        {
            var window = Find.WindowStack.windows.FirstOrDefault(w => w.GetType() == chooseRewardDialogType);
            if (window == null) return;

            choosenRewardField(window) = rewardDef;
            window.Close();
        }

        private static IEnumerable<CodeInstruction> ReplaceButton(IEnumerable<CodeInstruction> instr)
        {
            var method = AccessTools.Method(typeof(Widgets), nameof(Widgets.ButtonText), new[] { typeof(Rect), typeof(string), typeof(bool), typeof(bool), typeof(bool) });

            foreach (var ci in instr)
            {
                if (ci.Calls(method))
                {
                    // Inject another argument (instance)
                    yield return new CodeInstruction(OpCodes.Ldarg_0);

                    ci.operand = AccessTools.Method(typeof(VanillaStorytellersWinstonWaves), nameof(InjectedButton));
                }

                yield return ci;
            }
        }

        private static bool PauseIfDialogOpen(Map map)
            => Find.WindowStack.IsOpen(chooseRewardDialogType);
    }
}
