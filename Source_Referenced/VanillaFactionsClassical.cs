using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using VFEC;
using VFEC.Buildings;
using VFEC.Comps;
using VFEC.Perks.Workers;
using VFEC.Senators;

namespace Multiplayer.Compat
{
    /// <summary>Vanilla Factions Expanded - Classical by Oskar Potocki, ISOREX, xrushha, legodude17, Chowder</summary>
    /// <see href="https://github.com/Vanilla-Expanded/VanillaFactionsExpanded-Classical"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2787850474"/>
    [MpCompatFor("OskarPotocki.VFE.Classical")]
    class VanillaFactionsClassical
    {
        private static bool syncDialog = false;

        public VanillaFactionsClassical(ModContentPack mod)
        {
            LongEventHandler.ExecuteWhenFinished(LatePatch);

            // Road building
            MpCompat.RegisterLambdaDelegate(typeof(WorldComponent_RoadBuilding), nameof(WorldComponent_RoadBuilding.AddRoadGizmos), 1);
            // Close the world targeter, as syncing the previous delegate makes it return false (default for bool), making it not end targetting
            var roadGizmo = MpMethodUtil.GetLambda(typeof(WorldComponent_RoadBuilding), nameof(WorldComponent_RoadBuilding.AddRoadGizmos), MethodType.Normal, null, 1);
            MpCompat.harmony.Patch(roadGizmo, postfix: new HarmonyMethod(typeof(VanillaFactionsClassical), nameof(StopTargeter)));
            // Pick up the scorpion
            MpCompat.RegisterLambdaDelegate(typeof(ScorpionTurret), nameof(ScorpionTurret.GetGizmos), 0);
            // Toggle hediff
            MpCompat.RegisterLambdaMethod(typeof(CompToggleHediff), nameof(CompToggleHediff.CompGetWornGizmosExtra), 1);
            // Recruit pawn
            MpCompat.RegisterLambdaDelegate(typeof(VeniVidiVici), nameof(VeniVidiVici.AddGizmo), 0);

            // Deploying the scorpion
            MP.RegisterSyncWorker<Designator_InstallScorpion>(SyncInstallScorpion, shouldConstruct: true);

            // Sync dialog closing and add pause lock.
            // We only do it if it was opened from world map, not from faction list (which is for viewing only and has no interactions).
            DialogUtilities.InitializeDialogCloseSync(true);
            foreach (var ctor in AccessTools.GetDeclaredConstructors(typeof(Dialog_SenatorInfo)))
                MpCompat.harmony.Patch(ctor, new HarmonyMethod(typeof(VanillaFactionsClassical), nameof(PostDialogOpen)));

            // Replace the buttons from senator list with our own
            MpCompat.harmony.Patch(
                AccessTools.Method(typeof(Dialog_SenatorInfo), nameof(Dialog_SenatorInfo.DrawSenatorInfo)),
                transpiler: new HarmonyMethod(typeof(VanillaFactionsClassical), nameof(ReplaceSenatorButtons)));

            // Sync our replacement button actions for the senator interaction
            MP.RegisterSyncMethod(typeof(VanillaFactionsClassical), nameof(SyncedQuestButton));
            MP.RegisterSyncMethod(typeof(VanillaFactionsClassical), nameof(SyncedBribeButton));
        }

        private static void LatePatch()
        {
            // Lighting the beacon
            MpCompat.RegisterLambdaMethod(typeof(Beacon), nameof(Beacon.LightCommand), 0);

            // Initialize senator component (if not initialized yet)
            MP.RegisterSyncMethod(typeof(WorldComponent_Senators), nameof(WorldComponent_Senators.CheckInit));
            MpCompat.harmony.Patch(MpMethodUtil.GetLambda(typeof(WorldComponent_Senators), nameof(WorldComponent_Senators.AddSenatorsOption)),
                prefix: new HarmonyMethod(typeof(VanillaFactionsClassical), nameof(PreDialogCreated)));
        }

        private static void StopTargeter() => Find.WorldTargeter.StopTargeting();

        private static void SyncInstallScorpion(SyncWorker sync, ref Designator_InstallScorpion designator)
        {
            sync.Bind(ref designator.placingRot);

            if (sync.isWriting)
            {
                sync.Write(designator.GiveJobTo);
                sync.Write(designator.Scorpion);
            }
            else
            {
                designator.GiveJobTo = sync.Read<Pawn>();
                designator.Scorpion = sync.Read<Scorpion>();
            }
        }

        private static bool QuestButtonReplacement(Rect rect, string label, bool drawBackground, bool doMouseoverSounds, bool active, TextAnchor? overrideTextAnchor, Dialog_SenatorInfo.AllSenatorInfo senatorInfo,
            Dialog_SenatorInfo dialog)
        {
            var buttonResult = Widgets.ButtonText(rect, label, drawBackground, doMouseoverSounds, active, overrideTextAnchor);

            if (!MP.IsInMultiplayer || !buttonResult)
                return buttonResult;

            var index = dialog.senatorInfo.IndexOf(senatorInfo);
            if (index >= 0)
                SyncedQuestButton(index);
            return false; // Don't let the mod handle this case
        }

        private static bool BribeButtonReplacement(Rect rect, string label, bool drawBackground, bool doMouseoverSounds, bool active, TextAnchor? overrideTextAnchor, Dialog_SenatorInfo.AllSenatorInfo senatorInfo,
            Dialog_SenatorInfo dialog)
        {
            var buttonResult = Widgets.ButtonText(rect, label, drawBackground, doMouseoverSounds, active, overrideTextAnchor);

            if (!MP.IsInMultiplayer || !buttonResult)
                return buttonResult;

            var index = dialog.senatorInfo.IndexOf(senatorInfo);
            if (index >= 0)
                SyncedBribeButton(index);
            return false; // Don't let the mod handle this case // Don't let the mod handle this case
        }

        private static void SyncedQuestButton(int senatorDataIndex)
        {
            if (senatorDataIndex < 0)
                return;

            var dialog = Find.WindowStack.WindowOfType<Dialog_SenatorInfo>();
            var senatorList = dialog?.senatorInfo;
            if (senatorList == null || senatorDataIndex >= senatorList.Count)
                return;

            var info = senatorList[senatorDataIndex];

            var canTakeQuest = true;

            if (info.Quest != null)
            {
                var state = info.Quest.State;
                if (state != QuestState.Ongoing && state != QuestState.NotYetAccepted)
                    info.Quest = null;
                else
                    canTakeQuest = false;
            }

            if (canTakeQuest)
            {
                var senatorInfo = WorldComponent_Senators.Instance.InfoFor(info.Pawn, dialog.Faction);
                info.Quest = (senatorInfo.Quest = SenatorQuests.GenerateQuestFor(senatorInfo, dialog.Faction));
                Find.QuestManager.Add(senatorInfo.Quest);
                QuestUtility.SendLetterQuestAvailable(senatorInfo.Quest);
            }
            else
                Messages.Message("VFEC.UI.AlreadyQuest".Translate(), MessageTypeDefOf.RejectInput, false);
        }

        private static void SyncedBribeButton(int senatorDataIndex)
        {
            if (senatorDataIndex < 0)
                return;

            var dialog = Find.WindowStack.WindowOfType<Dialog_SenatorInfo>();
            var senatorList = dialog?.senatorInfo;
            if (senatorList == null || senatorDataIndex >= senatorList.Count)
                return;

            var info = senatorList[senatorDataIndex];

            if (!CaravanInventoryUtility.HasThings(dialog.Caravan, ThingDefOf.Silver, Mathf.CeilToInt(dialog.moneyNeeded)))
            {
                Messages.Message("VFEC.UI.NotEnoughMoney".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            if (Rand.Chance(0.15f))
            {
                Messages.Message("VFEC.UI.BribeReject".Translate(info.Pawn.Name.ToStringFull), MessageTypeDefOf.RejectInput, true);
                info.CanBribe = false;
                WorldComponent_Senators.Instance.InfoFor(info.Pawn, dialog.Faction).CanBribe = false;
                return;
            }

            var remaining = Mathf.CeilToInt(dialog.moneyNeeded);

            CaravanInventoryUtility.TakeThings(dialog.Caravan, thing =>
            {
                if (thing.def != ThingDefOf.Silver)
                    return 0;

                var num = Mathf.Min(remaining, thing.stackCount);
                remaining -= num;
                return num;
            }).ForEach(t => t.Destroy());

            info.Favored = true;
            WorldComponent_Senators.Instance.NumBribes++;
            dialog.moneyNeeded = 1000f + 0.05f * WorldComponent_Senators.Instance.NumBribes * 0.5f *
                Find.WorldObjects.Settlements.Where(s => s.HasMap && s.Map.IsPlayerHome).Sum(s => s.Map.wealthWatcher.WealthTotal);
            WorldComponent_Senators.Instance.GainFavorOf(info.Pawn, dialog.Faction);
        }

        private static IEnumerable<CodeInstruction> ReplaceSenatorButtons(IEnumerable<CodeInstruction> instr)
        {
            var targetMethod = AccessTools.Method(typeof(Widgets), nameof(Widgets.ButtonText), new[] { typeof(Rect), typeof(string), typeof(bool), typeof(bool), typeof(bool), typeof(TextAnchor?) });
            MethodInfo replacementMethod = null;
            var patchedCount = 0;

            foreach (var ci in instr)
            {
                if (ci.opcode == OpCodes.Ldstr && ci.operand is string s)
                {
                    replacementMethod = s switch
                    {
                        "VFEC.UI.ReQuest" => AccessTools.Method(typeof(VanillaFactionsClassical), nameof(QuestButtonReplacement)),
                        "VFEC.UI.Bribe" => AccessTools.Method(typeof(VanillaFactionsClassical), nameof(BribeButtonReplacement)),
                        _ => replacementMethod
                    };
                }
                else if (replacementMethod != null && ci.opcode == OpCodes.Call && ci.operand is MethodInfo buttonMethod && buttonMethod == targetMethod)
                {
                    ci.operand = replacementMethod;
                    replacementMethod = null;
                    yield return new CodeInstruction(OpCodes.Ldarg_1); // Include parameter Dialog_SenatorInfo.AllSenatorInfo in the method
                    yield return new CodeInstruction(OpCodes.Ldarg_0); // Include self
                    patchedCount++;
                }

                yield return ci;
            }
            
            if (patchedCount == 0) Log.Warning("Failed to patch Vanilla Factions - Classical senator buttons");
            else if (patchedCount == 1) Log.Warning("Failed to fully patch Vanilla Factions - Classical senator buttons (only single patch was applied)");
        }

        private static void PreDialogCreated()
            => syncDialog = true;

        private static void PostDialogOpen(Window __instance)
        {
            if (MP.IsInMultiplayer && syncDialog)
            {
                syncDialog = false;
                DialogUtilities.PostDialogOpen_CloseSync_PauseLock(__instance);
            }
        }
    }
}