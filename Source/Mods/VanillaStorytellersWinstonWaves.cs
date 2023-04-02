using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Multiplayer.API;
using UnityEngine;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Vanilla Storytellers Expanded - Winston Waves by Oskar Potocki, Kikohi</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2734032569"/>
    /// <see href="https://github.com/Vanilla-Expanded/VanillaStorytellersExpanded-WinstonWave"/>
    [MpCompatFor("VanillaStorytellersExpanded.WinstonWave")]
    internal class VanillaStorytellersWinstonWaves
    {
        private static Type chooseRewardDialogType;
        private static AccessTools.FieldRef<object, Def> choosenRewardField;
        private static AccessTools.FieldRef<object, bool> nextRaidInfoSentField;
        private static bool ignoreCall = false;

        public VanillaStorytellersWinstonWaves(ModContentPack mod)
        {
            // RNG
            {
                PatchingUtilities.PatchSystemRand("VSEWW.Window_ChooseReward:DoWindowContents", false);
            }

            // Dialogs (input)
            {
                var type = chooseRewardDialogType = AccessTools.TypeByName("VSEWW.Window_ChooseReward");
                choosenRewardField = AccessTools.FieldRefAccess<Def>(type, "choosenReward");
                DialogUtilities.RegisterPauseLock(type);
                MpCompat.harmony.Patch(AccessTools.DeclaredConstructor(type, new[] { typeof(int), typeof(float), typeof(Map) }),
                    postfix: new HarmonyMethod(AccessTools.DeclaredMethod(typeof(VanillaStorytellersWinstonWaves), nameof(PostRewardDialogCreated))));
                MpCompat.harmony.Patch(AccessTools.DeclaredMethod(type, nameof(Window.DoWindowContents)),
                    prefix: new HarmonyMethod(AccessTools.DeclaredMethod(typeof(VanillaStorytellersWinstonWaves), nameof(PreRewardDoWindowContents))));

                type = AccessTools.TypeByName("VSEWW.RewardDef");
                MpCompat.harmony.Patch(AccessTools.DeclaredMethod(type, "DrawCard"),
                    transpiler: new HarmonyMethod(typeof(VanillaStorytellersWinstonWaves), nameof(ReplaceButton)));
                MP.RegisterSyncMethod(typeof(VanillaStorytellersWinstonWaves), nameof(SyncedChooseReward));

                type = AccessTools.TypeByName("VSEWW.MapComponent_Winston");
                var method = AccessTools.DeclaredMethod(type, "StartRaid");
                MP.RegisterSyncMethod(method); // Called during ticking and from button inside Window_WaveCounter (we only care for the button)
                MpCompat.harmony.Patch(method, prefix: new HarmonyMethod(typeof(VanillaStorytellersWinstonWaves), nameof(StopDuplicateRaidStart)));

                type = AccessTools.TypeByName("VSEWW.NextRaidInfo");
                nextRaidInfoSentField = AccessTools.FieldRefAccess<bool>(type, "sent");
            }
        }

        private static void PostRewardDialogCreated(Window __instance, IList ___rewards)
        {
            if (!MP.IsInMultiplayer)
                return;

            // No rewards chosen yet, call DoWindowContents to generate them.
            // It won't draw anything until those are generated first, so there's no issue calling this method.
            // It would naturally generate them inside of the first call to DoWindowContents, but the issue is
            // that it doesn't happen during ticking - causing each player to generate different rewards.
            if (___rewards is not { Count: > 0 })
            {
                try
                {
                    ignoreCall = true;
                    __instance.DoWindowContents(Rect.zero);
                }
                finally
                {
                    ignoreCall = false;
                }
            }

            // Potential alternative approach - patch DoWindowContents with seeded push/pop.
            // As for the seed itself - wave number combined with Find.World.ConstantRandSeed and/or Find.World.info.Seed?
            // The current approach will allow (just like the mod itself) for reloading the game for better rewards.
        }

        private static bool PreRewardDoWindowContents(Window __instance, IList ___rewards, Def ___choosenReward)
        {
            // Ignore if not in MP, or if MP but the method was specifically called from our patch to generate rewards.
            if (!MP.IsInMultiplayer || ignoreCall)
                return true;

            // A reward has been chosen (random reward mod option), close the dialog so it can be received by players.
            // Closing during constructor doesn't work, as the dialog is not yet in the WindowStack.
            if (___choosenReward != null)
            {
                __instance.Close();
                return false;
            }

            // Rewards list is empty or null, which means it failed generating rewards. Display an error and close the dialog,
            // as trying to generate them now would end up causing issues.
            if (___rewards is not { Count: > 0 })
            {
                __instance.Close();
                return false;
            }

            return true;
        }

        // If the method was called multiple times in a row (like spamming the button in MP) - prevent it from
        // attempting to start another one if there's one already going on. Will prevent some errors in logs from happening.
        private static bool StopDuplicateRaidStart(object ___nextRaidInfo) => ___nextRaidInfo != null && !nextRaidInfoSentField(___nextRaidInfo);

        private static bool InjectedButton(Rect rect, string label, bool drawBackground, bool doMouseoverSound, bool active, TextAnchor? anchor, Def instance)
        {
            if (Widgets.ButtonText(rect, label, drawBackground, doMouseoverSound, active, anchor))
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
            var target = AccessTools.Method(typeof(Widgets), nameof(Widgets.ButtonText), new[] { typeof(Rect), typeof(string), typeof(bool), typeof(bool), typeof(bool), typeof(TextAnchor?) });
            var replacement = AccessTools.Method(typeof(VanillaStorytellersWinstonWaves), nameof(InjectedButton));
            var replacedCount = 0;

            foreach (var ci in instr)
            {
                if (ci.opcode == OpCodes.Call && ci.operand is MethodInfo method && method == target)
                {
                    // Inject another argument (instance)
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    ci.operand = replacement;

                    replacedCount++;
                }

                yield return ci;
            }

            const int expectedReplacements = 1;
            if (replacedCount != expectedReplacements)
                Log.Warning($"Patched incorrect number of button calls inside of RewardDef.DrawCard: patched {replacedCount} methods, expected {expectedReplacements}");
        }
    }
}
