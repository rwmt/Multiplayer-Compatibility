using System;
using System.Collections.Generic;
using System.Reflection;
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
        // Ordered list of stat fields in IsekaiStatAllocation — index is shared across all arrays.
        private static readonly string[] StatAllocationFieldNames = ["strength", "vitality", "dexterity", "intelligence", "wisdom", "charisma"];
        // Matching pending field names in Window_StatsAttribution (same order).
        private static readonly string[] PendingFieldNames = ["pendingSTR", "pendingVIT", "pendingDEX", "pendingINT", "pendingWIS", "pendingCHA"];

        // ── Type references ──────────────────────────────────────────────
        private static Type isekaiComponentType;
        private static Type isekaiStatAllocationType;
        private static Type passiveTreeTrackerType;
        private static Type windowStatsType;
        private static Type iTabType;
        private static Type raidRankSystemType;
        private static Type pawnStatGeneratorType;
        private static Type treeAutoAssignerType;

        // ── IsekaiStatAllocation field accessors ─────────────────────────
        private static FieldInfo[] statAllocationFields;          // [6]: indexed by StatAllocationFieldNames
        private static FieldInfo statAllocationAvailablePoints;
        private static FieldInfo isekaiCompStatsField;
        private static FieldInfo isekaiCompPassiveTreeField;

        // ── Window_StatsAttribution field accessors ──────────────────────
        private static AccessTools.FieldRef<object, Pawn> statsWindowPawn;
        private static AccessTools.FieldRef<object, int>[] statsWindowPending; // [6]: indexed by PendingFieldNames
        private static AccessTools.FieldRef<object, int> statsWindowPointsSpent;

        // ── MP sync fields for ITab watch ────────────────────────────────
        // Indices 0..5 = stat fields (StatAllocationFieldNames), index 6 = availableStatPoints
        private static ISyncField[] statSyncFields;

        // ── Pawn generation RNG fields ────────────────────────────────────
        private static FieldInfo raidRankSystemRandomField;
        private static FieldInfo pawnStatGeneratorRandomField;
        private static FieldInfo treeAutoAssignerRngField;

        // ── ManaCore bulk-absorb sync ─────────────────────────────────────
        private static Type manaCoreCompType;
        private static FieldInfo manaCoreCompPendingBulkAbsorbField;

        // ── Debug logging toggle ──────────────────────────────────────────
        internal static bool DebugLog = false;

        // ── Constructor — called by MP at startup ─────────────────────────
        public IsekaiRPGCompat(ModContentPack mod)
        {
            // types
            isekaiComponentType = Resolve("IsekaiLeveling.IsekaiComponent");
            isekaiStatAllocationType = Resolve("IsekaiLeveling.IsekaiStatAllocation");
            iTabType = Resolve("IsekaiLeveling.UI.ITab_IsekaiStats");
            windowStatsType = Resolve("IsekaiLeveling.UI.Window_StatsAttribution");
            passiveTreeTrackerType = Resolve("IsekaiLeveling.SkillTree.PassiveTreeTracker");
            raidRankSystemType = Resolve("IsekaiLeveling.MobRanking.RaidRankSystem");
            pawnStatGeneratorType = Resolve("IsekaiLeveling.PawnStatGenerator");
            treeAutoAssignerType = Resolve("IsekaiLeveling.SkillTree.TreeAutoAssigner");

            if (isekaiComponentType == null
                || isekaiStatAllocationType == null
                || passiveTreeTrackerType == null
                || windowStatsType == null
                || iTabType == null
                || raidRankSystemType == null
                || pawnStatGeneratorType == null
                || treeAutoAssignerType == null
            )
            {
                Log.Error("[IsekaiMP] One or more required types could not be resolved — patches will NOT be applied.");
                return;
            }

            // random fields
            raidRankSystemRandomField = AccessTools.Field(raidRankSystemType, "random");
            pawnStatGeneratorRandomField = AccessTools.Field(pawnStatGeneratorType, "random");
            treeAutoAssignerRngField = AccessTools.Field(treeAutoAssignerType, "rng");

            if (raidRankSystemRandomField == null
                || pawnStatGeneratorRandomField == null
                || treeAutoAssignerRngField == null
            )
            {
                Log.Error("[IsekaiMP] One or more required fields could not be resolved — patches will NOT be applied.");
                return;
            }

            // IsekaiComponent
            isekaiCompStatsField = AccessTools.Field(isekaiComponentType, "stats");
            isekaiCompPassiveTreeField = AccessTools.Field(isekaiComponentType, "passiveTree");
            PatchAndLog(isekaiComponentType, "DevAddLevel", prefix: nameof(DevAddLevelPrefix));
            MP.RegisterSyncMethod(typeof(IsekaiRPGCompat), nameof(SyncedDevAddLevel));

            // IsekaiStatAllocation
            statAllocationFields = new FieldInfo[StatAllocationFieldNames.Length];
            for (int i = 0; i < StatAllocationFieldNames.Length; i++)
                statAllocationFields[i] = AccessTools.Field(isekaiStatAllocationType, StatAllocationFieldNames[i]);
            statAllocationAvailablePoints = AccessTools.Field(isekaiStatAllocationType, "availableStatPoints");
            statSyncFields = new ISyncField[StatAllocationFieldNames.Length + 1];
            MP.RegisterSyncWorker<object>(SyncIsekaiStatAllocation, isekaiStatAllocationType);

            // ITab_IsekaiStats
            for (int i = 0; i < StatAllocationFieldNames.Length; i++)
                statSyncFields[i] = MP.RegisterSyncField(isekaiStatAllocationType, StatAllocationFieldNames[i]);
            statSyncFields[StatAllocationFieldNames.Length] = MP.RegisterSyncField(isekaiStatAllocationType, "availableStatPoints");
            PatchAndLog(iTabType, "FillTab", prefix: nameof(ITabFillTabPrefix), postfix: nameof(ITabFillTabPostfix));

            // Window_StatsAttribution
            statsWindowPawn = AccessTools.FieldRefAccess<Pawn>(windowStatsType, "pawn");
            statsWindowPending = new AccessTools.FieldRef<object, int>[PendingFieldNames.Length];
            for (int i = 0; i < PendingFieldNames.Length; i++)
                statsWindowPending[i] = AccessTools.FieldRefAccess<int>(windowStatsType, PendingFieldNames[i]);
            statsWindowPointsSpent = AccessTools.FieldRefAccess<int>(windowStatsType, "pointsSpent");
            PatchAndLog(windowStatsType, "ApplyChanges", prefix: nameof(ApplyChangesPrefix));
            MP.RegisterSyncMethod(typeof(IsekaiRPGCompat), nameof(SyncedApplyStats));

            // PassiveTreeTracker
            PatchAndLog(passiveTreeTrackerType, "Unlock", prefix: nameof(UnlockNodePrefix));
            PatchAndLog(passiveTreeTrackerType, "Respec", prefix: nameof(RespecPrefix));
            MP.RegisterSyncMethod(typeof(IsekaiRPGCompat), nameof(SyncedUnlockNode));
            MP.RegisterSyncMethod(typeof(IsekaiRPGCompat), nameof(SyncedRespec));

            // RNG 
            PatchAndLog(raidRankSystemType, "AssignRaidPawnRank", prefix: nameof(AssignRaidPawnRankPrefix));
            PatchAndLog(pawnStatGeneratorType, "InitializePawnStats", prefix: nameof(InitializePawnStatsPrefix));
            PatchAndLog(isekaiComponentType, "PostSpawnSetup", prefix: nameof(PostSpawnSetupPrefix));
            PatchAndLog(isekaiComponentType, "LevelUp", prefix: nameof(LevelUpPrefix));

            // ManaCore Usage Sync
            manaCoreCompType = AccessTools.TypeByName("IsekaiLeveling.CompUseEffect_ManaCore");
            if (manaCoreCompType != null)
            {
                manaCoreCompPendingBulkAbsorbField = AccessTools.Field(manaCoreCompType, "pendingBulkAbsorb");
                PatchAndLog(manaCoreCompType, "CompFloatMenuOptions", postfix: nameof(ManaCoreFloatMenuOptionsPostfix));
                MP.RegisterSyncMethod(typeof(IsekaiRPGCompat), nameof(SyncedSetPendingBulkAbsorb));
            }

        }

        private static Type Resolve(string typeName)
        {
            var type = AccessTools.TypeByName(typeName);
            if (type == null)
                Log.Error($"[IsekaiMP] Could not resolve type '{typeName}' — mod version may have changed.");
            else if (DebugLog)
                Log.Message($"[IsekaiMP]   Resolved: {typeName}");
            return type;
        }

        private static void PatchAndLog(Type targetType, string methodName, string prefix = null, string postfix = null)
        {
            var method = AccessTools.DeclaredMethod(targetType, methodName);
            if (method == null)
            {
                Log.Error($"[IsekaiMP] Could not find method '{targetType.Name}.{methodName}' — patch skipped.");
                return;
            }
            var harmonyPrefix = prefix != null ? new HarmonyMethod(typeof(IsekaiRPGCompat), prefix) : null;
            var harmonyPostfix = postfix != null ? new HarmonyMethod(typeof(IsekaiRPGCompat), postfix) : null;
            MpCompat.harmony.Patch(method, prefix: harmonyPrefix, postfix: harmonyPostfix);
        }

        private static ThingComp GetCompByType(Pawn pawn, Type compType)
        {
            foreach (var comp in pawn.AllComps)
                if (compType.IsInstanceOfType(comp))
                    return comp;
            return null;
        }

        /// <summary>
        /// Finds the pawn whose IsekaiComponent satisfies <paramref name="match"/>.
        /// Searches spawned pawns on all maps, then world pawns as fallback.
        /// </summary>
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

        // ═══════════════════════════════════════════════════════════════
        // STAT ALLOCATION — ApplyChanges intercept
        // ═══════════════════════════════════════════════════════════════

        private static bool ApplyChangesPrefix(object __instance)
        {
            if (!MP.IsInMultiplayer) return true;

            Pawn pawn = statsWindowPawn(__instance);
            if (pawn == null) return true;

            // Capture all pending stat values from the window's staging fields.
            var pending = new int[StatAllocationFieldNames.Length];
            for (int i = 0; i < pending.Length; i++)
                pending[i] = statsWindowPending[i](__instance);

            // Compute points spent once on the initiating client (current authoritative values),
            // so every client deducts the same amount regardless of their local state.
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

            if (DebugLog) Log.Message($"[IsekaiMP] ApplyChanges for {pawn.LabelShort}: [{string.Join(",", pending)}] spent={pointsSpent} god={godMode}");

            SyncedApplyStats(pawn, pending, pointsSpent, godMode);
            return false;
        }

        private static void SyncedApplyStats(Pawn pawn, int[] statValues, int pointsSpent, bool godMode)
        {
            if (DebugLog) Log.Message($"[IsekaiMP] SyncedApplyStats for {pawn.LabelShort}: [{string.Join(",", statValues)}] spent={pointsSpent} god={godMode}");

            var comp = GetCompByType(pawn, isekaiComponentType);
            if (comp == null) return;

            object statsObj = isekaiCompStatsField.GetValue(comp);

            for (int i = 0; i < statAllocationFields.Length; i++)
                statAllocationFields[i].SetValue(statsObj, statValues[i]);

            if (!godMode && pointsSpent > 0)
            {
                int remaining = (int)statAllocationAvailablePoints.GetValue(statsObj);
                statAllocationAvailablePoints.SetValue(statsObj, remaining - pointsSpent);
                if (DebugLog) Log.Message($"[IsekaiMP] SyncedApplyStats: availableStatPoints {remaining} → {remaining - pointsSpent}");
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

                // Reset the cached pointsSpent counter so the display is correct immediately.
                statsWindowPointsSpent(window) = 0;

                if (DebugLog) Log.Message($"[IsekaiMP] RefreshStatsWindows: reset pending fields for {pawn.LabelShort}");
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // SKILL TREE — Node unlock intercept
        // ═══════════════════════════════════════════════════════════════

        private static bool _suppressUnlockPrefix = false;

        private static bool UnlockNodePrefix(string nodeId, Pawn pawn, ref bool __result)
        {
            if (!MP.IsInMultiplayer || _suppressUnlockPrefix || pawn == null) return true;

            if (DebugLog) Log.Message($"[IsekaiMP] UnlockNode intercepted for {pawn.LabelShort} — node '{nodeId}'");
            SyncedUnlockNode(pawn, nodeId);
            __result = false;
            return false;
        }

        private static void SyncedUnlockNode(Pawn pawn, string nodeId)
        {
            if (DebugLog) Log.Message($"[IsekaiMP] SyncedUnlockNode for {pawn.LabelShort} — node '{nodeId}'");

            var comp = GetCompByType(pawn, isekaiComponentType);
            if (comp == null) { Log.Warning($"[IsekaiMP] SyncedUnlockNode: no IsekaiComponent on {pawn.LabelShort}"); return; }

            var passiveTree = isekaiCompPassiveTreeField.GetValue(comp);
            if (passiveTree == null) { Log.Warning($"[IsekaiMP] SyncedUnlockNode: null passiveTree on {pawn.LabelShort}"); return; }

            _suppressUnlockPrefix = true;
            try
            {
                bool result = (bool)AccessTools.DeclaredMethod(passiveTreeTrackerType, "Unlock")
                                               .Invoke(passiveTree, new object[] { nodeId, pawn });
                if (DebugLog) Log.Message($"[IsekaiMP] SyncedUnlockNode: Unlock('{nodeId}') returned {result}");
            }
            finally { _suppressUnlockPrefix = false; }
        }

        // ═══════════════════════════════════════════════════════════════
        // SKILL TREE — Respec intercept
        // ═══════════════════════════════════════════════════════════════

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

            if (DebugLog) Log.Message($"[IsekaiMP] Respec intercepted for {owner.LabelShort}");
            SyncedRespec(owner);
            return false;
        }

        private static void SyncedRespec(Pawn pawn)
        {
            if (DebugLog) Log.Message($"[IsekaiMP] SyncedRespec for {pawn.LabelShort}");

            var comp = GetCompByType(pawn, isekaiComponentType);
            if (comp == null) { Log.Warning($"[IsekaiMP] SyncedRespec: no IsekaiComponent on {pawn.LabelShort}"); return; }

            var passiveTree = isekaiCompPassiveTreeField.GetValue(comp);
            if (passiveTree == null) { Log.Warning($"[IsekaiMP] SyncedRespec: null passiveTree on {pawn.LabelShort}"); return; }

            _suppressRespecPrefix = true;
            try
            {
                AccessTools.DeclaredMethod(passiveTreeTrackerType, "Respec").Invoke(passiveTree, null);
                if (DebugLog) Log.Message($"[IsekaiMP] SyncedRespec: completed for {pawn.LabelShort}");
            }
            finally { _suppressRespecPrefix = false; }
        }

        // ═══════════════════════════════════════════════════════════════
        // DEV BUTTONS — DevAddLevel
        // ═══════════════════════════════════════════════════════════════

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

            if (DebugLog) Log.Message($"[IsekaiMP] DevAddLevel intercepted for {pawn.LabelShort} — levels={levels}");
            SyncedDevAddLevel(pawn, levels);
            return false;
        }

        private static void SyncedDevAddLevel(Pawn pawn, int levels)
        {
            if (DebugLog) Log.Message($"[IsekaiMP] SyncedDevAddLevel for {pawn.LabelShort} — levels={levels}");

            var comp = GetCompByType(pawn, isekaiComponentType);
            if (comp == null) { Log.Warning($"[IsekaiMP] SyncedDevAddLevel: no IsekaiComponent on {pawn.LabelShort}"); return; }

            _suppressDevAddLevelPrefix = true;
            try
            {
                AccessTools.DeclaredMethod(isekaiComponentType, "DevAddLevel").Invoke(comp, new object[] { levels });
                if (DebugLog) Log.Message($"[IsekaiMP] SyncedDevAddLevel: completed for {pawn.LabelShort}");
            }
            finally { _suppressDevAddLevelPrefix = false; }
        }

        // ═══════════════════════════════════════════════════════════════
        // ITAB — field watch (covers all inline stat mutations in dev UI)
        // ═══════════════════════════════════════════════════════════════

        private static void ITabFillTabPrefix(object __instance, ref bool __state)
        {
            if (!MP.IsInMultiplayer) return;

            if (AccessTools.Property(typeof(RimWorld.ITab), "SelPawn")?.GetValue(__instance) is not Pawn selPawn) return;

            var comp = GetCompByType(selPawn, isekaiComponentType);
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

        // ═══════════════════════════════════════════════════════════════
        // SYNC WORKER — IsekaiStatAllocation
        // ═══════════════════════════════════════════════════════════════

        private static void SyncIsekaiStatAllocation(SyncWorker sync, ref object statsAllocation)
        {
            if (sync.isWriting)
            {
                // Identify the owning pawn by matching the stats object reference.
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

        // ═══════════════════════════════════════════════════════════════
        // RNG Prefixes
        // ═══════════════════════════════════════════════════════════════

        private static void AssignRaidPawnRankPrefix(Pawn pawn)
        {
            UpdateRandomSeedForPawn(pawn);
        }

        private static void InitializePawnStatsPrefix(Pawn pawn)
        {
            UpdateRandomSeedForPawn(pawn);
        }
        private static void PostSpawnSetupPrefix(object __instance)
        {
            if (!MP.IsInMultiplayer) return;
            var pawn = ((ThingComp)(object)__instance).parent as Pawn;
            UpdateRandomSeedForPawn(pawn);
        }

        private static void LevelUpPrefix(object __instance)
        {
            if (!MP.IsInMultiplayer) return;
            var pawn = ((ThingComp)(object)__instance).parent as Pawn;
            UpdateRandomSeedForPawn(pawn);
        }

        private static void UpdateRandomSeedForPawn(Pawn pawn)
        {
            if (pawn == null) return;
            int seed = Gen.HashCombineInt(pawn.thingIDNumber, Find.TickManager.TicksGame);

            pawnStatGeneratorRandomField?.SetValue(null, new Random(seed));
            raidRankSystemRandomField?.SetValue(null, new Random(seed));
            treeAutoAssignerRngField?.SetValue(null, new Random(seed));
        }

        // ═══════════════════════════════════════════════════════════════
        // DESYNC FIX — ManaCore "Absorb all" float menu
        // pendingBulkAbsorb is set by the float menu lambda on the
        // initiating client only. We wrap the enabled option to call a
        // SyncMethod that sets the field on ALL clients before the
        // UseItem job completes and DoEffect reads the value.
        // ═══════════════════════════════════════════════════════════════

        private static void ManaCoreFloatMenuOptionsPostfix(object __instance, Pawn selPawn,
            ref IEnumerable<FloatMenuOption> __result)
        {
            if (!MP.IsInMultiplayer) return;
            __result = WrapManaCoreAbsorbOptions(__instance, selPawn, __result);
        }

        private static IEnumerable<FloatMenuOption> WrapManaCoreAbsorbOptions(
            object comp, Pawn selPawn, IEnumerable<FloatMenuOption> original)
        {
            var item = ((ThingComp)(object)comp).parent;
            foreach (var opt in original)
            {
                // Disabled options (null action) need no wrapping.
                if (opt.action == null) { yield return opt; continue; }

                // Replace the enabled "Absorb all" action with a synced equivalent.
                var capturedItem = item;
                int count = capturedItem?.stackCount ?? 0;
                yield return new FloatMenuOption(opt.Label, () =>
                {
                    // Sync pendingBulkAbsorb to all clients, then queue the job.
                    SyncedSetPendingBulkAbsorb(capturedItem, count);
                    selPawn.jobs.TryTakeOrderedJob(
                        JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("UseItem"), capturedItem));
                });
            }
        }

        private static void SyncedSetPendingBulkAbsorb(Thing item, int count)
        {
            if (item == null || manaCoreCompType == null) return;
            if (item is not ThingWithComps twc) return;
            foreach (var comp in twc.AllComps)
            {
                if (manaCoreCompType.IsInstanceOfType(comp))
                {
                    manaCoreCompPendingBulkAbsorbField.SetValue(comp, count);
                    return;
                }
            }
        }
    }
}
