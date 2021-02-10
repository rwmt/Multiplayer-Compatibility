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
    /// <see href="https://github.com/AndroidQuazar/VanillaAchievementsExpanded"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2288125657"/>
    [MpCompatFor("vanillaexpanded.achievements")]
    class VanillaAchievements
    {
        private static MethodInfo purchaseRewardMethod;
        private static MethodInfo refundPointsMethod;
        private static MethodInfo tryExecuteEventMethod;

        private static MethodInfo rewardDatabase;

        private static Type windowType;

        public VanillaAchievements(ModContentPack mod)
        {
            var type = AccessTools.TypeByName("AchievementsExpanded.AchievementReward");

            purchaseRewardMethod = AccessTools.Method(type, "PurchaseReward");
            refundPointsMethod = AccessTools.Method(type, "RefundPoints");
            tryExecuteEventMethod = AccessTools.Method(type, "TryExecuteEvent");

            rewardDatabase = AccessTools.PropertyGetter(typeof(DefDatabase<>).MakeGenericType(type), "AllDefsListForReading");

            windowType = AccessTools.TypeByName("AchievementsExpanded.MainTabWindow_Achievements");

            MpCompat.harmony.Patch(AccessTools.Method(windowType, "DrawSidePanel"), transpiler: new HarmonyMethod(typeof(VanillaAchievements), nameof(Transpiler)));

            // Sync our method
            MP.RegisterSyncMethod(typeof(VanillaAchievements), nameof(SyncedCalls));
        }

        private static bool usedReward = false;

        private static bool PurchaseReward(Def reward)
        {
            usedReward = true;
            var database = rewardDatabase.Invoke(null, Array.Empty<object>()) as IList;
            SyncedCalls(database.IndexOf(reward));
            return false;
        }

        private static void SyncedCalls(int index)
        {
            var database = rewardDatabase.Invoke(null, Array.Empty<object>()) as IList;
            var reward = database[index];

            if ((bool)purchaseRewardMethod.Invoke(reward, Array.Empty<object>()))
            {
                if (!(bool)tryExecuteEventMethod.Invoke(reward, Array.Empty<object>()))
                    refundPointsMethod.Invoke(reward, Array.Empty<object>());
                // Match the mod behavior and close the window for the user that succesfully bought the reward (trying to always close in our PurchaseReward method seemed to mess stuff up)
                else if (usedReward) 
                    foreach (var window in Find.WindowStack.Windows.Where(x => x.GetType() == windowType)) window.Close(false);
            }

            usedReward = false;
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instr)
        {
            // Basically change reward.PurchaseReward() into VanillaAchievements.PurchaseReward(reward)
            // Callvirt uses first loaded parameter as the class we call the method on,
            // but we use static class with Call, so it becomes the first parameter instead.

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
                }
            }

            return instr;
        }
    }
}
