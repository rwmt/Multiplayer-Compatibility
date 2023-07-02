using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Vanilla Achievements Expanded by Oskar Potocki, Smash Phil</summary>
    /// <see href="https://github.com/Vanilla-Expanded/VanillaAchievementsExpanded"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2288125657"/>
    [MpCompatFor("vanillaexpanded.achievements")]
    class VanillaAchievements
    {
        private static MethodInfo purchaseRewardMethod;
        private static MethodInfo refundPointsMethod;
        private static MethodInfo tryExecuteEventMethod;

        private static AccessTools.FieldRef<IList> rewardDatabase;

        private static Type windowType;

        public VanillaAchievements(ModContentPack mod)
        {
            var type = AccessTools.TypeByName("AchievementsExpanded.AchievementReward");

            purchaseRewardMethod = AccessTools.Method(type, "PurchaseReward");
            refundPointsMethod = AccessTools.Method(type, "RefundPoints");
            tryExecuteEventMethod = AccessTools.Method(type, "TryExecuteEvent");

            rewardDatabase = AccessTools.StaticFieldRefAccess<IList>(AccessTools.Field(typeof(DefDatabase<>).MakeGenericType(type), "defsList"));

            windowType = AccessTools.TypeByName("AchievementsExpanded.MainTabWindow_Achievements");

            var methods = new[]
            {
                AccessTools.Method(windowType, "DrawSidePanel"),
                AccessTools.DeclaredMethod(AccessTools.TypeByName("AchievementsExpanded.TraderTracker"), "Trigger"),
            };

            foreach (var method in methods)
                MpCompat.harmony.Patch(method, transpiler: new HarmonyMethod(typeof(VanillaAchievements), nameof(Transpiler)));

            // Sync our method
            MP.RegisterSyncMethod(typeof(VanillaAchievements), nameof(SyncedCalls));
        }

        private static bool usedReward = false;

        private static bool PurchaseReward(Def reward)
        {
            usedReward = true;
            var database = rewardDatabase();
            SyncedCalls(database.IndexOf(reward));
            return false;
        }

        private static void SyncedCalls(int index)
        {
            var database = rewardDatabase();
            var reward = database[index];

            if ((bool)purchaseRewardMethod.Invoke(reward, Array.Empty<object>()))
            {
                if (!(bool)tryExecuteEventMethod.Invoke(reward, Array.Empty<object>()))
                    refundPointsMethod.Invoke(reward, Array.Empty<object>());
                // Match the mod behavior and close the window for the user that successfully bought the reward (trying to always close in our PurchaseReward method seemed to mess stuff up)
                else if (usedReward)
                    Find.WindowStack.Windows.FirstOrDefault(x => x.GetType() == windowType)?.Close(false);
            }

            usedReward = false;
        }

        // Tradeable.GetPriceFor() seems to be called with a Tradeable that has null Thing, this should handle it
        // Returning 0 here won't have any effect
        private static float SaferGetPriceFor(Tradeable tradeable, TradeAction action)
        {
            if (MP.IsInMultiplayer && tradeable.AnyThing == null)
                return 0;
            return tradeable.GetPriceFor(action);
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instr)
        {
            // Basically change reward.PurchaseReward() into VanillaAchievements.PurchaseReward(reward)
            // Callvirt uses first loaded parameter as the class we call the method on,
            // but we use static class with Call, so it becomes the first parameter instead.

            var getPriceForMethod = AccessTools.Method(typeof(Tradeable), nameof(Tradeable.GetPriceFor));

            foreach (var ci in instr)
            {
                if (ci.opcode == OpCodes.Callvirt)
                {
                    var operand = (MethodInfo)ci.operand;

                    if (operand == purchaseRewardMethod)
                    {
                        ci.opcode = OpCodes.Call;
                        ci.operand = AccessTools.Method(typeof(VanillaAchievements), nameof(PurchaseReward));
                        break;
                    }
                    else if (operand == getPriceForMethod)
                    {
                        ci.opcode = OpCodes.Call;
                        ci.operand = AccessTools.Method(typeof(VanillaAchievements), nameof(SaferGetPriceFor));
                        break;
                    }
                }
            }

            return instr;
        }
    }
}
