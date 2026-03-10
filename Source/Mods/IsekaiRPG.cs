using System;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Isekai RPG Leveling by Jelly Creative</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3657580708"/>
    [MpCompatFor("JellyCreative.IsekaiLeveling")]
    public class IsekaiRPGCompat
    {
        // ── Type references ──────────────────────────────────────────────
        private static Type isekaiComponentType;
        private static Type passiveTreeTrackerType;
        private static Type windowStatsType;
        private static Type iTabType;

        // ── Field accessors ──────────────────────────────────────────────
        // Window_StatsAttribution fields (pending allocations before confirm)
        private static AccessTools.FieldRef<object, Pawn>  statsWindowPawn;
        private static AccessTools.FieldRef<object, int>   statsWindowPendingSTR;
        private static AccessTools.FieldRef<object, int>   statsWindowPendingVIT;
        private static AccessTools.FieldRef<object, int>   statsWindowPendingDEX;
        private static AccessTools.FieldRef<object, int>   statsWindowPendingINT;
        private static AccessTools.FieldRef<object, int>   statsWindowPendingWIS;
        private static AccessTools.FieldRef<object, int>   statsWindowPendingCHA;
        private static AccessTools.FieldRef<object, int>   statsWindowPointsSpent;

        // IsekaiStatAllocation fields (cached at init)
        private static System.Reflection.FieldInfo statAllocStrength;
        private static System.Reflection.FieldInfo statAllocVitality;
        private static System.Reflection.FieldInfo statAllocDexterity;
        private static System.Reflection.FieldInfo statAllocIntelligence;
        private static System.Reflection.FieldInfo statAllocWisdom;
        private static System.Reflection.FieldInfo statAllocCharisma;
        private static System.Reflection.FieldInfo statAllocAvailablePoints;
        private static System.Reflection.FieldInfo isekaiCompStatsField;

        // ── Constructor — called by MP at startup ────────────────────────
        public IsekaiRPGCompat(ModContentPack mod)
        {
            Log.Message("[IsekaiMP] Initializing multiplayer compatibility for Isekai RPG Leveling...");

            // ── Resolve types ─────────────────────────────────────────────
            isekaiComponentType    = Resolve("IsekaiLeveling.IsekaiComponent");
            passiveTreeTrackerType = Resolve("IsekaiLeveling.SkillTree.PassiveTreeTracker");
            windowStatsType        = Resolve("IsekaiLeveling.UI.Window_StatsAttribution");
            iTabType               = Resolve("IsekaiLeveling.UI.ITab_IsekaiStats");

            // Abort early if any critical type is missing (mod version mismatch, etc.)
            if (isekaiComponentType == null || passiveTreeTrackerType == null ||
                windowStatsType == null || iTabType == null)
            {
                Log.Error("[IsekaiMP] One or more required types could not be resolved — " +
                          "compatibility patches will NOT be applied. Check the log above for details.");
                return;
            }

            // ── Cache IsekaiStatAllocation fields ────────────────────────
            var statAllocType          = AccessTools.TypeByName("IsekaiLeveling.IsekaiStatAllocation");
            statAllocStrength          = AccessTools.Field(statAllocType, "strength");
            statAllocVitality          = AccessTools.Field(statAllocType, "vitality");
            statAllocDexterity         = AccessTools.Field(statAllocType, "dexterity");
            statAllocIntelligence      = AccessTools.Field(statAllocType, "intelligence");
            statAllocWisdom            = AccessTools.Field(statAllocType, "wisdom");
            statAllocCharisma          = AccessTools.Field(statAllocType, "charisma");
            statAllocAvailablePoints   = AccessTools.Field(statAllocType, "availableStatPoints");
            isekaiCompStatsField       = AccessTools.Field(isekaiComponentType, "stats");

            // Register sync worker so MP can serialize IsekaiStatAllocation instances
            // (required for the field watch on availableStatPoints to work).
            MP.RegisterSyncWorker<object>(SyncIsekaiStatAllocation, statAllocType);

            // ── Stat allocation window ────────────────────────────────────
            // Intercept ApplyChanges: instead of letting it run locally, we
            // read the pending values and dispatch a synced method with the deltas.
            // ApplyChanges has no parameters — we need to capture state before it runs.
            statsWindowPawn       = AccessTools.FieldRefAccess<Pawn>(windowStatsType, "pawn");
            statsWindowPendingSTR = AccessTools.FieldRefAccess<int>(windowStatsType,  "pendingSTR");
            statsWindowPendingVIT = AccessTools.FieldRefAccess<int>(windowStatsType,  "pendingVIT");
            statsWindowPendingDEX = AccessTools.FieldRefAccess<int>(windowStatsType,  "pendingDEX");
            statsWindowPendingINT = AccessTools.FieldRefAccess<int>(windowStatsType,  "pendingINT");
            statsWindowPendingWIS = AccessTools.FieldRefAccess<int>(windowStatsType,  "pendingWIS");
            statsWindowPendingCHA    = AccessTools.FieldRefAccess<int>(windowStatsType, "pendingCHA");
            statsWindowPointsSpent   = AccessTools.FieldRefAccess<int>(windowStatsType, "pointsSpent");

            PatchAndLog(windowStatsType, "ApplyChanges",
                prefix: nameof(ApplyChangesPrefix));
            MP.RegisterSyncMethod(typeof(IsekaiRPGCompat), nameof(SyncedApplyStats));
            Log.Message("[IsekaiMP]   [OK] Stat attribution window (ApplyChanges) patched");

            // ── Skill tree: node unlock ───────────────────────────────────
            // Two call sites in Window_SkillTree both call comp.passiveTree.Unlock().
            // We patch the Unlock method on PassiveTreeTracker directly so that any
            // call site (double-click OR unlock button) is intercepted.
            PatchAndLog(passiveTreeTrackerType, "Unlock",
                prefix: nameof(UnlockNodePrefix));
            MP.RegisterSyncMethod(typeof(IsekaiRPGCompat), nameof(SyncedUnlockNode));
            Log.Message("[IsekaiMP]   [OK] Skill tree node unlock patched");

            // ── Skill tree: respec ────────────────────────────────────────
            // The respec button calls comp.passiveTree.Respec() directly.
            PatchAndLog(passiveTreeTrackerType, "Respec",
                prefix: nameof(RespecPrefix));
            MP.RegisterSyncMethod(typeof(IsekaiRPGCompat), nameof(SyncedRespec));
            Log.Message("[IsekaiMP]   [OK] Skill tree respec patched");

            // ── Dev-mode buttons (ITab_IsekaiStats & Window_SkillTree) ────
            // DevAddLevel is an instance method on IsekaiComponent (a ThingComp).
            // We patch it with a prefix and dispatch via a static synced method that
            // takes a Pawn (natively serializable) instead of the component itself.
            PatchAndLog(isekaiComponentType, "DevAddLevel", prefix: nameof(DevAddLevelPrefix));
            MP.RegisterSyncMethod(typeof(IsekaiRPGCompat), nameof(SyncedDevAddLevel));
            Log.Message("[IsekaiMP]   [OK] Dev button DevAddLevel patched");

            // ── ITab field watch ──────────────────────────────────────────
            // The +SP / Max buttons in ITab directly write stat fields inline.
            // We watch all stat fields + availableStatPoints around FillTab so MP
            // detects changes and syncs them.
            _strSyncField = MP.RegisterSyncField(statAllocType, "strength");
            _vitSyncField = MP.RegisterSyncField(statAllocType, "vitality");
            _dexSyncField = MP.RegisterSyncField(statAllocType, "dexterity");
            _intSyncField = MP.RegisterSyncField(statAllocType, "intelligence");
            _wisSyncField = MP.RegisterSyncField(statAllocType, "wisdom");
            _chaSyncField = MP.RegisterSyncField(statAllocType, "charisma");
            var statPointsField = AccessTools.Field(statAllocType, "availableStatPoints");
            _spSyncField = MP.RegisterSyncField(statPointsField);

            PatchAndLog(iTabType, "FillTab",
                prefix: nameof(ITabFillTabPrefix),
                postfix: nameof(ITabFillTabPostfix));
            Log.Message("[IsekaiMP]   [OK] ITab stat field watch patched (all 7 fields)");

            Log.Message("[IsekaiMP] Initialization complete — all patches applied successfully.");
        }

        /// <summary>Resolves a type by name and logs a clear error if not found.</summary>
        private static Type Resolve(string typeName)
        {
            var type = AccessTools.TypeByName(typeName);
            if (type == null)
                Log.Error($"[IsekaiMP] Could not resolve type '{typeName}' — mod version may have changed.");
            else
                Log.Message($"[IsekaiMP]   Resolved: {typeName}");
            return type;
        }

        /// <summary>
        /// Patches a method and logs success or failure clearly.
        /// <paramref name="prefix"/> and <paramref name="postfix"/> are method names on <see cref="IsekaiRPGCompat"/>.
        /// </summary>
        private static void PatchAndLog(Type targetType, string methodName,
            string prefix = null, string postfix = null)
        {
            var method = AccessTools.DeclaredMethod(targetType, methodName);
            if (method == null)
            {
                Log.Error($"[IsekaiMP] Could not find method '{targetType.Name}.{methodName}' — patch skipped.");
                return;
            }

            var harmonyPrefix  = prefix  != null ? new HarmonyMethod(typeof(IsekaiRPGCompat), prefix)  : null;
            var harmonyPostfix = postfix != null ? new HarmonyMethod(typeof(IsekaiRPGCompat), postfix) : null;
            MpCompat.harmony.Patch(method, prefix: harmonyPrefix, postfix: harmonyPostfix);
        }

        // ── Stored sync fields (used by ITab prefix/postfix) ────────────
        private static ISyncField _strSyncField;
        private static ISyncField _vitSyncField;
        private static ISyncField _dexSyncField;
        private static ISyncField _intSyncField;
        private static ISyncField _wisSyncField;
        private static ISyncField _chaSyncField;
        private static ISyncField _spSyncField;

        /// <summary>
        /// Runtime equivalent of pawn.GetComp&lt;T&gt;() when T is only known at runtime.
        /// Iterates ThingComps and returns the first comp whose type matches.
        /// </summary>
        private static ThingComp GetCompByType(Pawn pawn, Type compType)
        {
            foreach (var comp in pawn.AllComps)
                if (compType.IsInstanceOfType(comp))
                    return comp;
            return null;
        }

        // ═══════════════════════════════════════════════════════════════
        // STAT ALLOCATION — ApplyChanges intercept
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Prefix on Window_StatsAttribution.ApplyChanges().
        /// In multiplayer, captures pending stat values and dispatches a synced
        /// call instead of running the local method.
        /// </summary>
        private static bool ApplyChangesPrefix(object __instance)
        {
            if (!MP.IsInMultiplayer) return true; // run original in singleplayer

            Pawn pawn = statsWindowPawn(__instance);
            if (pawn == null) return true;

            // Read desired final stat values from the window's pending fields.
            int str   = statsWindowPendingSTR(__instance);
            int vit   = statsWindowPendingVIT(__instance);
            int dex   = statsWindowPendingDEX(__instance);
            int intel = statsWindowPendingINT(__instance);
            int wis   = statsWindowPendingWIS(__instance);
            int cha   = statsWindowPendingCHA(__instance);

            // Compute points spent now, while we have the authoritative current values.
            // Sending this along avoids each client independently computing a delta
            // from their own (potentially desynced) current stats.
            int pointsSpent = 0;
            bool godMode = Prefs.DevMode && DebugSettings.godMode;
            if (!godMode)
            {
                var comp = GetCompByType(pawn, isekaiComponentType);
                if (comp != null)
                {
                    object statsObj = isekaiCompStatsField.GetValue(comp);
                    int curSTR   = (int)statAllocStrength.GetValue(statsObj);
                    int curVIT   = (int)statAllocVitality.GetValue(statsObj);
                    int curDEX   = (int)statAllocDexterity.GetValue(statsObj);
                    int curINT   = (int)statAllocIntelligence.GetValue(statsObj);
                    int curWIS   = (int)statAllocWisdom.GetValue(statsObj);
                    int curCHA   = (int)statAllocCharisma.GetValue(statsObj);
                    pointsSpent = (str - curSTR) + (vit - curVIT) + (dex - curDEX)
                                + (intel - curINT) + (wis - curWIS) + (cha - curCHA);
                }
            }

            Log.Message($"[IsekaiMP] ApplyChanges intercepted for {pawn.LabelShort} " +
                        $"— dispatching sync: STR={str} VIT={vit} DEX={dex} INT={intel} WIS={wis} CHA={cha} " +
                        $"pointsSpent={pointsSpent} godMode={godMode}");

            SyncedApplyStats(pawn, str, vit, dex, intel, wis, cha, pointsSpent, godMode);
            return false; // skip original — synced method will run on all clients
        }

        /// <summary>
        /// Synced: apply confirmed stat allocation for the given pawn.
        /// Runs identically on every client. Mirrors the logic of ApplyChanges().
        /// </summary>
        private static void SyncedApplyStats(Pawn pawn,
            int pendingSTR, int pendingVIT, int pendingDEX,
            int pendingINT, int pendingWIS, int pendingCHA,
            int pointsSpent, bool godMode)
        {
            Log.Message($"[IsekaiMP] SyncedApplyStats executing for {pawn.LabelShort} " +
                        $"— STR={pendingSTR} VIT={pendingVIT} DEX={pendingDEX} " +
                        $"INT={pendingINT} WIS={pendingWIS} CHA={pendingCHA} " +
                        $"pointsSpent={pointsSpent} godMode={godMode}");

            var comp = GetCompByType(pawn, isekaiComponentType);
            if (comp == null) return;

            object statsObj = isekaiCompStatsField.GetValue(comp);

            // Set all stat values directly — bypasses AllocatePoint's guard checks,
            // which could silently fail if availableStatPoints is desynced between clients.
            statAllocStrength.SetValue(statsObj,     pendingSTR);
            statAllocVitality.SetValue(statsObj,     pendingVIT);
            statAllocDexterity.SetValue(statsObj,    pendingDEX);
            statAllocIntelligence.SetValue(statsObj, pendingINT);
            statAllocWisdom.SetValue(statsObj,       pendingWIS);
            statAllocCharisma.SetValue(statsObj,     pendingCHA);

            // Deduct the same pointsSpent on every client (computed once by the initiating client).
            if (!godMode && pointsSpent > 0)
            {
                int remaining = (int)statAllocAvailablePoints.GetValue(statsObj);
                statAllocAvailablePoints.SetValue(statsObj, remaining - pointsSpent);
                Log.Message($"[IsekaiMP] SyncedApplyStats: availableStatPoints {remaining} → {remaining - pointsSpent}");
            }

            // Update rank trait
            var pawnStatGen = AccessTools.TypeByName("IsekaiLeveling.PawnStatGenerator");
            AccessTools.DeclaredMethod(pawnStatGen, "UpdateRankTraitFromStats")
                       .Invoke(null, new object[] { pawn, comp });

            // Sync the pending fields of any open Window_StatsAttribution for this pawn
            // so the UI reflects the newly applied values rather than stale pre-apply values.
            RefreshStatsWindows(pawn, statsObj);
        }

        /// <summary>
        /// Finds any open Window_StatsAttribution for the given pawn and resets its
        /// pending stat fields to match the current (just-applied) values.
        /// Called after SyncedApplyStats so all clients' UIs stay consistent.
        /// </summary>
        private static void RefreshStatsWindows(Pawn pawn, object statsObj)
        {
            foreach (var window in Find.WindowStack.Windows)
            {
                if (!windowStatsType.IsInstanceOfType(window)) continue;
                if (!ReferenceEquals(statsWindowPawn(window), pawn)) continue;

                // Reset pending fields to match the now-current stats.
                statsWindowPendingSTR(window) = (int)statAllocStrength.GetValue(statsObj);
                statsWindowPendingVIT(window) = (int)statAllocVitality.GetValue(statsObj);
                statsWindowPendingDEX(window) = (int)statAllocDexterity.GetValue(statsObj);
                statsWindowPendingINT(window) = (int)statAllocIntelligence.GetValue(statsObj);
                statsWindowPendingWIS(window) = (int)statAllocWisdom.GetValue(statsObj);
                statsWindowPendingCHA(window) = (int)statAllocCharisma.GetValue(statsObj);
                // Reset the cached pointsSpent counter — since pending now equals committed,
                // the window would otherwise show stale pending point usage.
                statsWindowPointsSpent(window) = 0;

                Log.Message($"[IsekaiMP] RefreshStatsWindows: reset pending fields for {pawn.LabelShort}");
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // SKILL TREE — Node unlock intercept
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Prefix on PassiveTreeTracker.Unlock(string nodeId, Pawn pawn).
        /// In multiplayer, dispatches a synced call and suppresses the local one.
        /// The synced call will execute Unlock on every client.
        /// </summary>
        private static bool UnlockNodePrefix(string nodeId, Pawn pawn, ref bool __result)
        {
            if (!MP.IsInMultiplayer) return true;
            if (_suppressUnlockPrefix) return true; // re-entrant call from SyncedUnlockNode
            if (pawn == null) return true;

            Log.Message($"[IsekaiMP] UnlockNode intercepted for {pawn.LabelShort} — node '{nodeId}' — dispatching sync");

            SyncedUnlockNode(pawn, nodeId);
            __result = false; // return value placeholder before sync completes
            return false;
        }

        /// <summary>
        /// Synced: unlock a skill tree node on all clients.
        /// Calls PassiveTreeTracker.Unlock directly via the comp, bypassing our prefix
        /// by checking MP.IsInMultiplayer — the re-entrant call happens outside the sync
        /// dispatch path, so the prefix won't intercept it again.
        /// </summary>
        private static void SyncedUnlockNode(Pawn pawn, string nodeId)
        {
            Log.Message($"[IsekaiMP] SyncedUnlockNode executing for {pawn.LabelShort} — node '{nodeId}'");

            var comp = GetCompByType(pawn, isekaiComponentType);
            if (comp == null) { Log.Warning($"[IsekaiMP] SyncedUnlockNode: no IsekaiComponent on {pawn.LabelShort}"); return; }

            var passiveTree = AccessTools.Field(isekaiComponentType, "passiveTree").GetValue(comp);
            if (passiveTree == null) { Log.Warning($"[IsekaiMP] SyncedUnlockNode: null passiveTree on {pawn.LabelShort}"); return; }

            // Temporarily disable our prefix to avoid recursion.
            _suppressUnlockPrefix = true;
            try
            {
                bool result = (bool)AccessTools.DeclaredMethod(passiveTreeTrackerType, "Unlock")
                                               .Invoke(passiveTree, new object[] { nodeId, pawn });
                Log.Message($"[IsekaiMP] SyncedUnlockNode: Unlock('{nodeId}') returned {result} for {pawn.LabelShort}");
            }
            finally
            {
                _suppressUnlockPrefix = false;
            }
        }

        private static bool _suppressUnlockPrefix = false;

        // Adjusted prefix to respect the re-entrancy guard
        // (replaces the one registered in constructor — handled via the flag check below)

        // ═══════════════════════════════════════════════════════════════
        // SKILL TREE — Respec intercept
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Prefix on PassiveTreeTracker.Respec().
        /// PassiveTreeTracker doesn't hold a pawn reference, so we find the pawn
        /// by searching spawned pawns for a matching passiveTree instance.
        /// </summary>
        private static bool RespecPrefix(object __instance)
        {
            if (!MP.IsInMultiplayer) return true;
            if (_suppressRespecPrefix) return true; // re-entrant call from SyncedRespec

            Pawn owner = FindPawnForPassiveTree(__instance);
            if (owner == null)
            {
                Log.Warning("[IsekaiMP] RespecPrefix: could not identify owning pawn — running locally (may desync!)");
                return true;
            }

            Log.Message($"[IsekaiMP] Respec intercepted for {owner.LabelShort} — dispatching sync");

            SyncedRespec(owner);
            return false;
        }

        /// <summary>Synced: respec (refund non-Start nodes) for a pawn.</summary>
        private static void SyncedRespec(Pawn pawn)
        {
            Log.Message($"[IsekaiMP] SyncedRespec executing for {pawn.LabelShort}");

            var comp = GetCompByType(pawn, isekaiComponentType);
            if (comp == null) { Log.Warning($"[IsekaiMP] SyncedRespec: no IsekaiComponent on {pawn.LabelShort}"); return; }

            var passiveTree = AccessTools.Field(isekaiComponentType, "passiveTree").GetValue(comp);
            if (passiveTree == null) { Log.Warning($"[IsekaiMP] SyncedRespec: null passiveTree on {pawn.LabelShort}"); return; }

            _suppressRespecPrefix = true;
            try
            {
                AccessTools.DeclaredMethod(passiveTreeTrackerType, "Respec")
                           .Invoke(passiveTree, null);
                Log.Message($"[IsekaiMP] SyncedRespec: Respec() completed for {pawn.LabelShort}");
            }
            finally
            {
                _suppressRespecPrefix = false;
            }
        }

        private static bool _suppressRespecPrefix = false;

        // ═══════════════════════════════════════════════════════════════
        // DEV BUTTONS — DevAddLevel
        // ═══════════════════════════════════════════════════════════════

        private static bool _suppressDevAddLevelPrefix = false;

        /// <summary>
        /// Prefix on IsekaiComponent.DevAddLevel(int levels).
        /// Intercepts the call, dispatches a synced version that takes a Pawn
        /// (natively serializable) instead of the component instance.
        /// </summary>
        private static bool DevAddLevelPrefix(object __instance, int levels)
        {
            if (!MP.IsInMultiplayer) return true;
            if (_suppressDevAddLevelPrefix) return true;

            Pawn pawn = FindPawnForComp(__instance);
            if (pawn == null)
            {
                Log.Warning("[IsekaiMP] DevAddLevelPrefix: could not identify owning pawn — running locally (may desync!)");
                return true;
            }

            Log.Message($"[IsekaiMP] DevAddLevel intercepted for {pawn.LabelShort} — levels={levels} — dispatching sync");
            SyncedDevAddLevel(pawn, levels);
            return false;
        }

        /// <summary>Synced: add levels for a pawn via DevAddLevel.</summary>
        private static void SyncedDevAddLevel(Pawn pawn, int levels)
        {
            Log.Message($"[IsekaiMP] SyncedDevAddLevel executing for {pawn.LabelShort} — levels={levels}");

            var comp = GetCompByType(pawn, isekaiComponentType);
            if (comp == null) { Log.Warning($"[IsekaiMP] SyncedDevAddLevel: no IsekaiComponent on {pawn.LabelShort}"); return; }

            _suppressDevAddLevelPrefix = true;
            try
            {
                AccessTools.DeclaredMethod(isekaiComponentType, "DevAddLevel")
                           .Invoke(comp, new object[] { levels });
                Log.Message($"[IsekaiMP] SyncedDevAddLevel: DevAddLevel({levels}) completed for {pawn.LabelShort}");
            }
            finally
            {
                _suppressDevAddLevelPrefix = false;
            }
        }

        // ── ITab FillTab watch (covers all inline stat mutations in dev UI) ──

        private static void ITabFillTabPrefix(object __instance, ref bool __state)
        {
            if (!MP.IsInMultiplayer) return;

            if (AccessTools.Property(typeof(RimWorld.ITab), "SelPawn")?.GetValue(__instance) is not Pawn selPawn) return;

            var comp = GetCompByType(selPawn, isekaiComponentType);
            if (comp == null) return;

            var stats = isekaiCompStatsField.GetValue(comp);
            if (stats == null) return;

            __state = true;
            MP.WatchBegin();
            // Watch all stat fields so +SP, Max auto-allocate, etc. are all synced.
            _strSyncField.Watch(stats);
            _vitSyncField.Watch(stats);
            _dexSyncField.Watch(stats);
            _intSyncField.Watch(stats);
            _wisSyncField.Watch(stats);
            _chaSyncField.Watch(stats);
            _spSyncField.Watch(stats);
        }

        private static void ITabFillTabPostfix(bool __state)
        {
            if (__state)
                MP.WatchEnd();
        }

        // ═══════════════════════════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Finds the pawn whose IsekaiComponent instance matches the given object.
        /// Used by DevAddLevelPrefix to identify the owning pawn.
        /// </summary>
        private static Pawn FindPawnForComp(object compInstance)
        {
            foreach (var map in Find.Maps)
            {
                foreach (var pawn in map.mapPawns.AllPawnsSpawned)
                {
                    var comp = GetCompByType(pawn, isekaiComponentType);
                    if (ReferenceEquals(comp, compInstance))
                        return pawn;
                }
            }

            if (Find.WorldPawns != null)
            {
                foreach (var pawn in Find.WorldPawns.AllPawnsAlive)
                {
                    var comp = GetCompByType(pawn, isekaiComponentType);
                    if (ReferenceEquals(comp, compInstance))
                        return pawn;
                }
            }

            return null;
        }

        /// <summary>
        /// Finds the pawn whose IsekaiComponent.passiveTree is the given instance.
        /// Used by RespecPrefix to identify the owning pawn.
        /// Searches all maps' spawned pawns (and world pawns as fallback).
        /// </summary>
        private static Pawn FindPawnForPassiveTree(object treeInstance)
        {
            var passiveTreeField = AccessTools.Field(isekaiComponentType, "passiveTree");

            foreach (var map in Find.Maps)
            {
                foreach (var pawn in map.mapPawns.AllPawnsSpawned)
                {
                    var comp = GetCompByType(pawn, isekaiComponentType);
                    if (comp == null) continue;
                    if (ReferenceEquals(passiveTreeField.GetValue(comp), treeInstance))
                        return pawn;
                }
            }

            // Fallback: world pawns
            if (Find.WorldPawns != null)
            {
                foreach (var pawn in Find.WorldPawns.AllPawnsAlive)
                {
                    var comp = GetCompByType(pawn, isekaiComponentType);
                    if (comp == null) continue;
                    if (ReferenceEquals(passiveTreeField.GetValue(comp), treeInstance))
                        return pawn;
                }
            }

            return null;
        }

        /// <summary>
        /// Finds the pawn whose IsekaiComponent.stats is the given instance.
        /// Used by the IsekaiStatAllocation sync worker to identify the owning pawn.
        /// </summary>
        private static Pawn FindPawnForStats(object statsInstance)
        {
            foreach (var map in Find.Maps)
            {
                foreach (var pawn in map.mapPawns.AllPawnsSpawned)
                {
                    var comp = GetCompByType(pawn, isekaiComponentType);
                    if (comp != null && ReferenceEquals(isekaiCompStatsField.GetValue(comp), statsInstance))
                        return pawn;
                }
            }

            if (Find.WorldPawns != null)
            {
                foreach (var pawn in Find.WorldPawns.AllPawnsAlive)
                {
                    var comp = GetCompByType(pawn, isekaiComponentType);
                    if (comp != null && ReferenceEquals(isekaiCompStatsField.GetValue(comp), statsInstance))
                        return pawn;
                }
            }

            return null;
        }

        /// <summary>
        /// Sync worker for IsekaiStatAllocation.
        /// Serializes the instance by its owning pawn so MP can transmit the field target.
        /// </summary>
        private static void SyncIsekaiStatAllocation(SyncWorker sync, ref object statsAlloc)
        {
            if (sync.isWriting)
            {
                sync.Write(FindPawnForStats(statsAlloc));
            }
            else
            {
                var pawn = sync.Read<Pawn>();
                if (pawn != null)
                {
                    var comp = GetCompByType(pawn, isekaiComponentType);
                    if (comp != null)
                        statsAlloc = isekaiCompStatsField.GetValue(comp);
                }
            }
        }
    }
}
