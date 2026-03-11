using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Isekai RPG Leveling by Jelly Creative</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3657580708"/>
    [MpCompatFor("JellyCreative.IsekaiLeveling")]
    public class IsekaiRPGCompat
    {
        private static readonly string[] StatAllocationFieldNames = ["strength", "vitality", "dexterity", "intelligence", "wisdom", "charisma"];
        private static readonly string[] PendingFieldNames        = ["pendingSTR", "pendingVIT", "pendingDEX", "pendingINT", "pendingWIS", "pendingCHA"];

        // ── Types ──────────────────────────────────────────────────────────
        private static Type isekaiComponentType;
        private static Type isekaiStatAllocationType;
        private static Type passiveTreeTrackerType;
        private static Type windowStatsType;
        private static Type iTabType;
        private static Type raidRankSystemType;
        private static Type pawnStatGeneratorType;
        private static Type treeAutoAssignerType;
        private static Type manaCoreCompType;

        // ── Field accessors ────────────────────────────────────────────────
        private static FieldInfo[] statAllocationFields;
        private static FieldInfo   statAllocationAvailablePoints;
        private static FieldInfo   isekaiCompStatsField;
        private static FieldInfo   isekaiCompPassiveTreeField;
        private static FieldInfo   raidRankSystemRandomField;
        private static FieldInfo   pawnStatGeneratorRandomField;
        private static FieldInfo   treeAutoAssignerRngField;

        private static AccessTools.FieldRef<object, Pawn> statsWindowPawn;
        private static AccessTools.FieldRef<object, int>[] statsWindowPending;
        private static AccessTools.FieldRef<object, int>   statsWindowPointsSpent;
        private static FastInvokeHandler iTabSelPawnGetter;

        // ── MP sync fields ─────────────────────────────────────────────────
        private static ISyncField[] statSyncFields; // [0..5] stats, [6] availableStatPoints

        // ── Deterministic RNG ──────────────────────────────────────────────
        // Seeded per-pawn from pawn.thingIDNumber before each generation call.
        // Transpilers redirect readonly Random fields in the mod to load this field instead.
        private static System.Random _currentPawnRng = new System.Random();

        // ── Constructor ────────────────────────────────────────────────────
        public IsekaiRPGCompat(ModContentPack mod)
        {
            isekaiComponentType      = Resolve("IsekaiLeveling.IsekaiComponent");
            isekaiStatAllocationType = Resolve("IsekaiLeveling.IsekaiStatAllocation");
            iTabType                 = Resolve("IsekaiLeveling.UI.ITab_IsekaiStats");
            windowStatsType          = Resolve("IsekaiLeveling.UI.Window_StatsAttribution");
            passiveTreeTrackerType   = Resolve("IsekaiLeveling.SkillTree.PassiveTreeTracker");
            raidRankSystemType       = Resolve("IsekaiLeveling.MobRanking.RaidRankSystem");
            pawnStatGeneratorType    = Resolve("IsekaiLeveling.PawnStatGenerator");
            treeAutoAssignerType     = Resolve("IsekaiLeveling.SkillTree.TreeAutoAssigner");
            manaCoreCompType         = Resolve("IsekaiLeveling.CompUseEffect_ManaCore");

            if (isekaiComponentType == null || isekaiStatAllocationType == null
                || passiveTreeTrackerType == null || windowStatsType == null
                || iTabType == null || raidRankSystemType == null
                || pawnStatGeneratorType == null || treeAutoAssignerType == null
                || manaCoreCompType == null)
            {
                Log.Error("[IsekaiMP] One or more required types could not be resolved — patches will NOT be applied.");
                return;
            }

            raidRankSystemRandomField    = AccessTools.Field(raidRankSystemType,    "random");
            pawnStatGeneratorRandomField = AccessTools.Field(pawnStatGeneratorType, "random");
            treeAutoAssignerRngField     = AccessTools.Field(treeAutoAssignerType,  "rng");

            if (raidRankSystemRandomField == null || pawnStatGeneratorRandomField == null || treeAutoAssignerRngField == null)
            {
                Log.Error("[IsekaiMP] One or more required fields could not be resolved — patches will NOT be applied.");
                return;
            }

            isekaiCompStatsField      = AccessTools.Field(isekaiComponentType, "stats");
            isekaiCompPassiveTreeField = AccessTools.Field(isekaiComponentType, "passiveTree");

            statAllocationFields = new FieldInfo[StatAllocationFieldNames.Length];
            for (int i = 0; i < StatAllocationFieldNames.Length; i++)
                statAllocationFields[i] = AccessTools.Field(isekaiStatAllocationType, StatAllocationFieldNames[i]);
            statAllocationAvailablePoints = AccessTools.Field(isekaiStatAllocationType, "availableStatPoints");

            statSyncFields = new ISyncField[StatAllocationFieldNames.Length + 1];
            for (int i = 0; i < StatAllocationFieldNames.Length; i++)
                statSyncFields[i] = MP.RegisterSyncField(isekaiStatAllocationType, StatAllocationFieldNames[i]);
            statSyncFields[StatAllocationFieldNames.Length] = MP.RegisterSyncField(isekaiStatAllocationType, "availableStatPoints");

            iTabSelPawnGetter  = MethodInvoker.GetHandler(AccessTools.PropertyGetter(typeof(ITab), "SelPawn"));
            
            statsWindowPawn    = AccessTools.FieldRefAccess<Pawn>(windowStatsType, "pawn");
            statsWindowPending = new AccessTools.FieldRef<object, int>[PendingFieldNames.Length];
            for (int i = 0; i < PendingFieldNames.Length; i++)
                statsWindowPending[i] = AccessTools.FieldRefAccess<int>(windowStatsType, PendingFieldNames[i]);
            statsWindowPointsSpent = AccessTools.FieldRefAccess<int>(windowStatsType, "pointsSpent");

            PatchAndLog(isekaiComponentType,   "DevAddLevel",          prefix: nameof(DevAddLevelPrefix));
            PatchAndLog(iTabType,              "FillTab",              prefix: nameof(ITabFillTabPrefix), postfix: nameof(ITabFillTabPostfix));
            PatchAndLog(windowStatsType,       "ApplyChanges",         prefix: nameof(ApplyChangesPrefix));
            PatchAndLog(passiveTreeTrackerType,"Unlock",               prefix: nameof(UnlockNodePrefix));
            PatchAndLog(passiveTreeTrackerType,"Respec",               prefix: nameof(RespecPrefix));
            PatchAndLog(pawnStatGeneratorType, "InitializePawnStats",  prefix: nameof(RandomSeedForPawnPrefix));
            PatchAndLog(raidRankSystemType,    "AssignRaidPawnRank",   prefix: nameof(RandomSeedForPawnPrefix), transpiler: nameof(UseSeededRaidRng));
            PatchAndLog(raidRankSystemType,    "RollVarianceOffset",   transpiler: nameof(UseSeededRaidRng));
            PatchAndLog(treeAutoAssignerType,  "AssignTreeProgression",transpiler: nameof(UseSeededTreeRng));
            PatchAndLog(treeAutoAssignerType,  "PickClass",            transpiler: nameof(UseSeededTreeRng));
            PatchAndLog(treeAutoAssignerType,  "SpendPointsOnTree",    transpiler: nameof(ReplaceShuffleWithSeeded));

            MP.RegisterSyncWorker<object>(SyncIsekaiStatAllocation, isekaiStatAllocationType);
            MP.RegisterSyncMethod(typeof(IsekaiRPGCompat), nameof(SyncedDevAddLevel));
            MP.RegisterSyncMethod(typeof(IsekaiRPGCompat), nameof(SyncedApplyStats));
            MP.RegisterSyncMethod(typeof(IsekaiRPGCompat), nameof(SyncedUnlockNode));
            MP.RegisterSyncMethod(typeof(IsekaiRPGCompat), nameof(SyncedRespec));
            MP.RegisterSyncDelegateLambda(manaCoreCompType, "GetBulkAbsorbOptions", 0);
        }

        private static Type Resolve(string typeName)
        {
            var type = AccessTools.TypeByName(typeName);
            if (type == null)
                Log.Error($"[IsekaiMP] Could not resolve type '{typeName}' — mod version may have changed.");
            return type;
        }

        private static void PatchAndLog(Type targetType, string methodName, string prefix = null, string postfix = null, string transpiler = null)
        {
            var method = AccessTools.DeclaredMethod(targetType, methodName);
            if (method == null)
            {
                Log.Error($"[IsekaiMP] Could not find method '{targetType.Name}.{methodName}' — patch skipped.");
                return;
            }
            MpCompat.harmony.Patch(method,
                prefix:     prefix     != null ? new HarmonyMethod(typeof(IsekaiRPGCompat), prefix)     : null,
                postfix:    postfix    != null ? new HarmonyMethod(typeof(IsekaiRPGCompat), postfix)    : null,
                transpiler: transpiler != null ? new HarmonyMethod(typeof(IsekaiRPGCompat), transpiler) : null);
        }

        private static ThingComp GetCompByType(Pawn pawn, Type compType)
        {
            foreach (var comp in pawn.AllComps)
                if (compType.IsInstanceOfType(comp))
                    return comp;
            return null;
        }

        private static Pawn FindPawnByComp(Func<ThingComp, bool> match)
        {
            foreach (var map in Find.Maps)
                foreach (var pawn in map.mapPawns.AllPawnsSpawned)
                {
                    var comp = GetCompByType(pawn, isekaiComponentType);
                    if (comp != null && match(comp))
                        return pawn;
                }

            if (Find.WorldPawns != null)
                foreach (var pawn in Find.WorldPawns.AllPawnsAlive)
                {
                    var comp = GetCompByType(pawn, isekaiComponentType);
                    if (comp != null && match(comp))
                        return pawn;
                }

            return null;
        }

        // ═══════════════════════════════════════════════════════════════════
        // STAT ALLOCATION
        // ═══════════════════════════════════════════════════════════════════

        private static bool ApplyChangesPrefix(object __instance)
        {
            if (!MP.IsInMultiplayer) return true;

            Pawn pawn = statsWindowPawn(__instance);
            if (pawn == null) return true;

            var pending = new int[StatAllocationFieldNames.Length];
            for (int i = 0; i < pending.Length; i++)
                pending[i] = statsWindowPending[i](__instance);

            int pointsSpent = 0;
            bool godMode = Prefs.DevMode && DebugSettings.godMode;
            if (!godMode)
            {
                var comp = GetCompByType(pawn, isekaiComponentType);
                if (comp != null)
                {
                    object statsObj = isekaiCompStatsField.GetValue(comp);
                    for (int i = 0; i < pending.Length; i++)
                        pointsSpent += pending[i] - (int)statAllocationFields[i].GetValue(statsObj);
                }
            }

            SyncedApplyStats(pawn, pending, pointsSpent, godMode);
            return false;
        }

        private static void SyncedApplyStats(Pawn pawn, int[] statValues, int pointsSpent, bool godMode)
        {
            var comp = GetCompByType(pawn, isekaiComponentType);
            if (comp == null) return;

            object statsObj = isekaiCompStatsField.GetValue(comp);

            for (int i = 0; i < statAllocationFields.Length; i++)
                statAllocationFields[i].SetValue(statsObj, statValues[i]);

            if (!godMode && pointsSpent > 0)
            {
                int remaining = (int)statAllocationAvailablePoints.GetValue(statsObj);
                statAllocationAvailablePoints.SetValue(statsObj, remaining - pointsSpent);
            }

            RefreshStatsWindows(pawn, statsObj);
        }

        private static void RefreshStatsWindows(Pawn pawn, object statsObj)
        {
            foreach (var window in Find.WindowStack.Windows)
            {
                if (!windowStatsType.IsInstanceOfType(window)) continue;
                if (!ReferenceEquals(statsWindowPawn(window), pawn)) continue;

                for (int i = 0; i < statAllocationFields.Length; i++)
                    statsWindowPending[i](window) = (int)statAllocationFields[i].GetValue(statsObj);

                statsWindowPointsSpent(window) = 0;
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // ITAB — stat field watch
        // ═══════════════════════════════════════════════════════════════════

        private static void ITabFillTabPrefix(object __instance, ref bool __state)
        {
            if (!MP.IsInMultiplayer) return;

            if (iTabSelPawnGetter(__instance) is not Pawn selPawn) return;

            var comp  = GetCompByType(selPawn, isekaiComponentType);
            var stats = comp != null ? isekaiCompStatsField.GetValue(comp) : null;
            if (stats == null) return;

            __state = true;
            MP.WatchBegin();
            foreach (var field in statSyncFields)
                field.Watch(stats);
        }

        private static void ITabFillTabPostfix(bool __state)
        {
            if (__state) MP.WatchEnd();
        }

        // ═══════════════════════════════════════════════════════════════════
        // SKILL TREE
        // ═══════════════════════════════════════════════════════════════════

        private static bool _suppressUnlockPrefix = false;

        private static bool UnlockNodePrefix(string nodeId, Pawn pawn, ref bool __result)
        {
            if (!MP.IsInMultiplayer || _suppressUnlockPrefix || pawn == null) return true;
            SyncedUnlockNode(pawn, nodeId);
            __result = false;
            return false;
        }

        private static void SyncedUnlockNode(Pawn pawn, string nodeId)
        {
            var comp = GetCompByType(pawn, isekaiComponentType);
            if (comp == null) { Log.Warning($"[IsekaiMP] SyncedUnlockNode: no IsekaiComponent on {pawn.LabelShort}"); return; }

            var passiveTree = isekaiCompPassiveTreeField.GetValue(comp);
            if (passiveTree == null) { Log.Warning($"[IsekaiMP] SyncedUnlockNode: null passiveTree on {pawn.LabelShort}"); return; }

            _suppressUnlockPrefix = true;
            try   { AccessTools.DeclaredMethod(passiveTreeTrackerType, "Unlock").Invoke(passiveTree, [nodeId, pawn]); }
            finally { _suppressUnlockPrefix = false; }
        }

        private static bool _suppressRespecPrefix = false;

        private static bool RespecPrefix(object __instance)
        {
            if (!MP.IsInMultiplayer || _suppressRespecPrefix) return true;

            Pawn owner = FindPawnByComp(c => ReferenceEquals(isekaiCompPassiveTreeField.GetValue(c), __instance));
            if (owner == null)
            {
                Log.Warning("[IsekaiMP] RespecPrefix: could not identify owning pawn — running locally (may desync!)");
                return true;
            }

            SyncedRespec(owner);
            return false;
        }

        private static void SyncedRespec(Pawn pawn)
        {
            var comp = GetCompByType(pawn, isekaiComponentType);
            if (comp == null) { Log.Warning($"[IsekaiMP] SyncedRespec: no IsekaiComponent on {pawn.LabelShort}"); return; }

            var passiveTree = isekaiCompPassiveTreeField.GetValue(comp);
            if (passiveTree == null) { Log.Warning($"[IsekaiMP] SyncedRespec: null passiveTree on {pawn.LabelShort}"); return; }

            _suppressRespecPrefix = true;
            try   { AccessTools.DeclaredMethod(passiveTreeTrackerType, "Respec").Invoke(passiveTree, null); }
            finally { _suppressRespecPrefix = false; }
        }

        // ═══════════════════════════════════════════════════════════════════
        // DEV TOOLS
        // ═══════════════════════════════════════════════════════════════════

        private static bool _suppressDevAddLevelPrefix = false;

        private static bool DevAddLevelPrefix(object __instance, int levels)
        {
            if (!MP.IsInMultiplayer || _suppressDevAddLevelPrefix) return true;

            Pawn pawn = FindPawnByComp(c => ReferenceEquals(c, __instance));
            if (pawn == null)
            {
                Log.Warning("[IsekaiMP] DevAddLevelPrefix: could not identify owning pawn — running locally (may desync!)");
                return true;
            }

            SyncedDevAddLevel(pawn, levels);
            return false;
        }

        private static void SyncedDevAddLevel(Pawn pawn, int levels)
        {
            var comp = GetCompByType(pawn, isekaiComponentType);
            if (comp == null) { Log.Warning($"[IsekaiMP] SyncedDevAddLevel: no IsekaiComponent on {pawn.LabelShort}"); return; }

            _suppressDevAddLevelPrefix = true;
            try   { AccessTools.DeclaredMethod(isekaiComponentType, "DevAddLevel").Invoke(comp, [levels]); }
            finally { _suppressDevAddLevelPrefix = false; }
        }

        // ═══════════════════════════════════════════════════════════════════
        // SYNC WORKER — IsekaiStatAllocation
        // ═══════════════════════════════════════════════════════════════════

        private static void SyncIsekaiStatAllocation(SyncWorker sync, ref object statsAllocation)
        {
            if (sync.isWriting)
            {
                var statsAllocationRef = statsAllocation;
                sync.Write(FindPawnByComp(c => ReferenceEquals(isekaiCompStatsField.GetValue(c), statsAllocationRef)));
            }
            else
            {
                var pawn = sync.Read<Pawn>();
                if (pawn != null)
                {
                    var comp = GetCompByType(pawn, isekaiComponentType);
                    if (comp != null)
                        statsAllocation = isekaiCompStatsField.GetValue(comp);
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // DETERMINISTIC RNG — pawn generation seeding and transpilers
        // ═══════════════════════════════════════════════════════════════════

        // Seed all mod RNG from pawn.thingIDNumber (deterministic, identical on every client).
        // PawnStatGenerator.random is not readonly — SetValue is reliable.
        // RaidRankSystem.random and TreeAutoAssigner.rng are readonly — transpilers redirect their ldsfld.
        private static void RandomSeedForPawnPrefix(Pawn pawn)
        {
            if (!MP.IsInMultiplayer) return;
            var rng = new Random(pawn.thingIDNumber);
            _currentPawnRng = rng;
            pawnStatGeneratorRandomField?.SetValue(null, rng);
        }

        // Redirects ldsfld RaidRankSystem::random → ldsfld _currentPawnRng.
        // Applied to: AssignRaidPawnRank, RollVarianceOffset.
        private static IEnumerable<CodeInstruction> UseSeededRaidRng(IEnumerable<CodeInstruction> instructions)
        {
            var target  = raidRankSystemRandomField;
            var replace = AccessTools.Field(typeof(IsekaiRPGCompat), nameof(Random));
            foreach (var instr in instructions)
                yield return (instr.opcode == OpCodes.Ldsfld && instr.operand is FieldInfo fi && fi == target)
                    ? new CodeInstruction(OpCodes.Ldsfld, replace) : instr;
        }

        // Redirects ldsfld TreeAutoAssigner::rng → ldsfld _currentPawnRng.
        // Applied to: AssignTreeProgression, PickClass.
        private static IEnumerable<CodeInstruction> UseSeededTreeRng(IEnumerable<CodeInstruction> instructions)
        {
            var target  = treeAutoAssignerRngField;
            var replace = AccessTools.Field(typeof(IsekaiRPGCompat), nameof(Random));
            foreach (var instr in instructions)
                yield return (instr.opcode == OpCodes.Ldsfld && instr.operand is FieldInfo fi && fi == target)
                    ? new CodeInstruction(OpCodes.Ldsfld, replace) : instr;
        }

        // Replaces call Shuffle<PassiveNodeRecord> → call SeededShuffleObj in SpendPointsOnTree.
        // Harmony cannot patch closed generic instantiations in Mono; call-site replacement is used instead.
        private static IEnumerable<CodeInstruction> ReplaceShuffleWithSeeded(IEnumerable<CodeInstruction> instructions)
        {
            var replacement = AccessTools.Method(typeof(IsekaiRPGCompat), nameof(SeededShuffleObj));
            foreach (var instr in instructions)
                yield return ((instr.opcode == OpCodes.Call || instr.opcode == OpCodes.Callvirt)
                    && instr.operand is MethodInfo mi && mi.Name == "Shuffle")
                    ? new CodeInstruction(OpCodes.Call, replacement) : instr;
        }

        // Fisher-Yates shuffle using _currentPawnRng.
        // Accepts object — IL-compatible with List<PassiveNodeRecord> (reference type, no boxing needed).
        private static void SeededShuffleObj(object listObj)
        {
            var list = listObj as System.Collections.IList;
            if (list == null) return;
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _currentPawnRng.Next(i + 1);
                object tmp = list[i];
                list[i]    = list[j];
                list[j]    = tmp;
            }
        }
    }
}
