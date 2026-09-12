using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using UnityEngine;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Adaptive Work Priorities by Astryl</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3792993740"/>
    [MpCompatFor("astryl.adaptiveworkpriorities")]
    internal class AdaptiveWorkPriorities
    {
        // Types
        private static Type learningStoreType;
        private static Type proposalType;
        private static Type pawnProfileType;
        private static Type priorityWriterType;
        private static Type emergencyManagerType;
        private static Type demandProbeType;
        private static Type activeCoverType;
        private static Type emergencyViewType;
        private static Type mainWindowType;
        private static Type pawnCardType;
        private static Type awSettingsType;
        private static Type awModType;
        private static Type workTypeTuningDefType;
        private static Type emergencyStateDefType;
        private static Type emergencyProfileEntryType;
        private static Type priorityRangesType;
        private static Type toastsType;

        // Reflection Accessors - LearningStore
        private static PropertyInfo learningStoreInstanceProp;
        private static MethodInfo learningStoreApplyProposalMethod;
        private static MethodInfo learningStoreSnoozeMethod;
        private static MethodInfo learningStoreNeverMethod;
        private static MethodInfo learningStoreResetLearningMethod;
        private static MethodInfo learningStoreUndoLastMethod;
        private static MethodInfo learningStoreMarkDirtyMethod;
        private static MethodInfo learningStoreMarkAllDirtyMethod;
        private static MethodInfo learningStoreTracksMethod;
        private static MethodInfo learningStoreProfileForMethod;
        private static MethodInfo learningStoreProposalsForMethod;
        private static MethodInfo learningStoreFindPawnMethod;
        private static MethodInfo learningStoreChainForMethod;
        private static MethodInfo learningStoreTryEmitDigestMethod;
        private static AccessTools.FieldRef<object, int> lastWriterPassAtField;
        private static AccessTools.FieldRef<object, int> writerBucketField;
        private static AccessTools.FieldRef<object, IList> schedulerRosterField;
        private static AccessTools.FieldRef<object, IList> storeCoversField;
        private static AccessTools.FieldRef<object, HashSet<int>> storeDirtyField;

        // Reflection Accessors - Proposal
        private static AccessTools.FieldRef<object, int> proposalPawnId;
        private static AccessTools.FieldRef<object, WorkTypeDef> proposalWt;
        private static AccessTools.FieldRef<object, int> proposalFrom;
        private static AccessTools.FieldRef<object, int> proposalTo;
        private static AccessTools.FieldRef<object, float> proposalConf;
        private static AccessTools.FieldRef<object, float> proposalAff;
        private static AccessTools.FieldRef<object, int> proposalEvCount;
        private static AccessTools.FieldRef<object, int> proposalSkill;
        private static AccessTools.FieldRef<object, int> proposalPassion;
        private static AccessTools.FieldRef<object, int> proposalCoverage;
        private static AccessTools.FieldRef<object, bool> proposalDemote;
        private static AccessTools.FieldRef<object, int> proposalColdDays;
        private static PropertyInfo proposalCellKeyProp;

        // Reflection Accessors - PawnProfile
        private static AccessTools.FieldRef<object, int> pawnProfileIdField;
        private static AccessTools.FieldRef<object, byte> pawnProfileModeField;
        private static AccessTools.FieldRef<object, List<string>> pawnProfileLockedField;

        // Reflection Accessors - PriorityWriter
        private static MethodInfo priorityWriterApplyMethod;
        private static MethodInfo priorityWriterApplyEmergencyMethod;
        private static MethodInfo priorityWriterEnsureNumericModeMethod;

        // Reflection Accessors - EmergencyManager
        private static MethodInfo emergencyManagerTickMethod;
        private static MethodInfo emergencyManagerReleaseAllMethod;
        private static MethodInfo emergencyManagerCoveredCountMethod;
        private static MethodInfo emergencyManagerPickFromChainMethod;
        private static MethodInfo emergencyManagerReconcileMethod;
        private static MethodInfo emergencyManagerRefreshStatesMethod;
        private static AccessTools.FieldRef<IList> emergencyActiveStatesField;

        // Reflection Accessors - ActiveCover
        private static AccessTools.FieldRef<object, int> activeCoverPawnId;
        private static AccessTools.FieldRef<object, string> activeCoverWt;
        private static AccessTools.FieldRef<object, int> activeCoverPrev;
        private static AccessTools.FieldRef<object, int> activeCoverApplied;
        private static AccessTools.FieldRef<object, byte> activeCoverSource;
        private static AccessTools.FieldRef<object, string> activeCoverStateDef;

        // Reflection Accessors - Settings & Misc
        private static FieldInfo awModSettingsField;
        private static AccessTools.FieldRef<object, bool> settingsMasterField;
        private static AccessTools.FieldRef<object, int> settingsModeField;
        private static AccessTools.FieldRef<object, bool> settingsEmergencyCoverField;
        private static AccessTools.FieldRef<object, bool> settingsEmergencyTriageField;
        private static AccessTools.FieldRef<object, int> settingsWriteIntervalTicksField;
        private static AccessTools.FieldRef<object, int> settingsWritesPerPassField;
        private static AccessTools.FieldRef<object, float> settingsSemiConfidenceField;
        private static AccessTools.FieldRef<object, bool> settingsToastsSuggestField;
        private static AccessTools.FieldRef<object, bool> settingsToastsLearningField;
        private static MethodInfo writeSettingsMethod;
        private static MethodInfo dropCellToastMethod;
        private static MethodInfo emergencyCoverTuningMethod;
        private static MethodInfo demandKindTuningMethod;
        private static PropertyInfo priorityRangesMaxProp;
        private static AccessTools.FieldRef<WorkTypeDef> emergencyViewSelField;
        private static AccessTools.FieldRef<object, IList> stateDefProfileField;
        private static AccessTools.FieldRef<object, int> stateDefOrderField;
        private static AccessTools.FieldRef<object, string> profileEntryWorkTypeField;
        private static AccessTools.FieldRef<object, int> profileEntryPriorityField;

        // Runtime Multi-Map State
        private static Map currentEvaluatingMap;
        private static readonly Dictionary<(int mapId, string kind), bool> multiMapDemand = new();
        private static int multiMapDemandTick = -1;
        private static readonly Dictionary<(int mapId, int mechs, ushort wtIndex), int> multiMapCoverage = new();
        private static int lastCoverageTick = -1;

        public AdaptiveWorkPriorities(ModContentPack mod) => LongEventHandler.ExecuteWhenFinished(LatePatch);

        private static void LatePatch()
        {
            // Ensure textures in AWTex are initialized on the main thread if needed
            var awTexType = AccessTools.TypeByName("AdaptiveWork.Core.AWTex");
            if (awTexType != null)
            {
                var workAtlasField = AccessTools.Field(awTexType, "WorkAtlas");
                if (workAtlasField != null && workAtlasField.GetValue(null) == null)
                {
                    awTexType.TypeInitializer?.Invoke(null, null);
                }
            }

            // Resolve Mod Types
            learningStoreType = AccessTools.TypeByName("AdaptiveWork.Data.LearningStore");
            proposalType = AccessTools.TypeByName("AdaptiveWork.Model.Proposal");
            pawnProfileType = AccessTools.TypeByName("AdaptiveWork.Model.PawnProfile");
            priorityWriterType = AccessTools.TypeByName("AdaptiveWork.Engine.PriorityWriter");
            emergencyManagerType = AccessTools.TypeByName("AdaptiveWork.Engine.EmergencyManager");
            demandProbeType = AccessTools.TypeByName("AdaptiveWork.Engine.DemandProbe");
            activeCoverType = AccessTools.TypeByName("AdaptiveWork.Engine.ActiveCover");
            emergencyViewType = AccessTools.TypeByName("AdaptiveWork.UI.EmergencyView");
            mainWindowType = AccessTools.TypeByName("AdaptiveWork.UI.MainWindow");
            pawnCardType = AccessTools.TypeByName("AdaptiveWork.UI.PawnCard");
            awSettingsType = AccessTools.TypeByName("AdaptiveWork.AWSettings");
            awModType = AccessTools.TypeByName("AdaptiveWork.AWMod");
            workTypeTuningDefType = AccessTools.TypeByName("AdaptiveWork.WorkTypeTuningDef");
            emergencyStateDefType = AccessTools.TypeByName("AdaptiveWork.EmergencyStateDef");
            emergencyProfileEntryType = AccessTools.TypeByName("AdaptiveWork.EmergencyProfileEntry");
            priorityRangesType = AccessTools.TypeByName("AdaptiveWork.Engine.PriorityRanges");
            toastsType = AccessTools.TypeByName("AdaptiveWork.UI.Toasts");

            if (learningStoreType == null || proposalType == null)
            {
                Log.Error("[MpCompat] AdaptiveWorkPriorities: Failed to resolve core types.");
                return;
            }

            // LearningStore Accessors
            learningStoreInstanceProp = AccessTools.Property(learningStoreType, "Instance");
            learningStoreApplyProposalMethod = AccessTools.Method(learningStoreType, "ApplyProposal", new[] { typeof(Pawn), proposalType, typeof(bool) });
            learningStoreSnoozeMethod = AccessTools.Method(learningStoreType, "Snooze", new[] { typeof(Pawn), proposalType });
            learningStoreNeverMethod = AccessTools.Method(learningStoreType, "Never", new[] { typeof(Pawn), proposalType });
            learningStoreResetLearningMethod = AccessTools.Method(learningStoreType, "ResetLearning", new[] { typeof(Pawn) });
            learningStoreUndoLastMethod = AccessTools.Method(learningStoreType, "UndoLast");
            learningStoreMarkDirtyMethod = AccessTools.Method(learningStoreType, "MarkDirty", new[] { typeof(Pawn) });
            learningStoreMarkAllDirtyMethod = AccessTools.Method(learningStoreType, "MarkAllDirty");
            learningStoreTracksMethod = AccessTools.Method(learningStoreType, "Tracks", new[] { typeof(Pawn) });
            learningStoreProfileForMethod = AccessTools.Method(learningStoreType, "ProfileFor", new[] { typeof(Pawn) });
            learningStoreProposalsForMethod = AccessTools.Method(learningStoreType, "ProposalsFor", new[] { typeof(Pawn) });
            learningStoreFindPawnMethod = AccessTools.Method(learningStoreType, "FindPawn", new[] { typeof(int) });
            learningStoreChainForMethod = AccessTools.Method(learningStoreType, "ChainFor", new[] { typeof(WorkTypeDef) });
            learningStoreTryEmitDigestMethod = AccessTools.Method(learningStoreType, "TryEmitDigest", new[] { typeof(int) });
            lastWriterPassAtField = AccessTools.FieldRefAccess<int>(learningStoreType, "lastWriterPassAt");
            writerBucketField = AccessTools.FieldRefAccess<int>(learningStoreType, "writerBucket");
            schedulerRosterField = AccessTools.FieldRefAccess<IList>(learningStoreType, "schedulerRoster");
            storeCoversField = AccessTools.FieldRefAccess<IList>(learningStoreType, "Covers");
            storeDirtyField = AccessTools.FieldRefAccess<HashSet<int>>(learningStoreType, "dirty");

            // Proposal Accessors
            proposalPawnId = AccessTools.FieldRefAccess<int>(proposalType, "pawnId");
            proposalWt = AccessTools.FieldRefAccess<WorkTypeDef>(proposalType, "wt");
            proposalFrom = AccessTools.FieldRefAccess<int>(proposalType, "from");
            proposalTo = AccessTools.FieldRefAccess<int>(proposalType, "to");
            proposalConf = AccessTools.FieldRefAccess<float>(proposalType, "conf");
            proposalAff = AccessTools.FieldRefAccess<float>(proposalType, "aff");
            proposalEvCount = AccessTools.FieldRefAccess<int>(proposalType, "evCount");
            proposalSkill = AccessTools.FieldRefAccess<int>(proposalType, "skill");
            proposalPassion = AccessTools.FieldRefAccess<int>(proposalType, "passion");
            proposalCoverage = AccessTools.FieldRefAccess<int>(proposalType, "coverage");
            proposalDemote = AccessTools.FieldRefAccess<bool>(proposalType, "demote");
            proposalColdDays = AccessTools.FieldRefAccess<int>(proposalType, "coldDays");
            proposalCellKeyProp = AccessTools.Property(proposalType, "CellKey");

            // PawnProfile Accessors
            pawnProfileIdField = AccessTools.FieldRefAccess<int>(pawnProfileType, "pawnId");
            pawnProfileModeField = AccessTools.FieldRefAccess<byte>(pawnProfileType, "mode");
            pawnProfileLockedField = AccessTools.FieldRefAccess<List<string>>(pawnProfileType, "locked");

            // PriorityWriter Accessors
            priorityWriterApplyMethod = AccessTools.Method(priorityWriterType, "Apply", new[] { typeof(Pawn), typeof(WorkTypeDef), typeof(int), typeof(bool), typeof(bool) });
            priorityWriterApplyEmergencyMethod = AccessTools.Method(priorityWriterType, "ApplyEmergency", new[] { typeof(Pawn), typeof(WorkTypeDef), typeof(int) });
            priorityWriterEnsureNumericModeMethod = AccessTools.Method(priorityWriterType, "EnsureNumericMode");

            // EmergencyManager Accessors
            emergencyManagerTickMethod = AccessTools.Method(emergencyManagerType, "Tick", new[] { typeof(int) });
            emergencyManagerReleaseAllMethod = AccessTools.Method(emergencyManagerType, "ReleaseAll", new[] { typeof(string) });
            emergencyManagerCoveredCountMethod = AccessTools.Method(emergencyManagerType, "CoveredCount", new[] { typeof(Map), typeof(WorkTypeDef) });
            emergencyManagerPickFromChainMethod = AccessTools.Method(emergencyManagerType, "PickFromChain");
            emergencyManagerReconcileMethod = AccessTools.Method(emergencyManagerType, "Reconcile");
            emergencyManagerRefreshStatesMethod = AccessTools.Method(emergencyManagerType, "RefreshStates");
            emergencyActiveStatesField = AccessTools.StaticFieldRefAccess<IList>(AccessTools.Field(emergencyManagerType, "ActiveStates"));

            // ActiveCover Accessors
            activeCoverPawnId = AccessTools.FieldRefAccess<int>(activeCoverType, "pawnId");
            activeCoverWt = AccessTools.FieldRefAccess<string>(activeCoverType, "wt");
            activeCoverPrev = AccessTools.FieldRefAccess<int>(activeCoverType, "prev");
            activeCoverApplied = AccessTools.FieldRefAccess<int>(activeCoverType, "applied");
            activeCoverSource = AccessTools.FieldRefAccess<byte>(activeCoverType, "source");
            activeCoverStateDef = AccessTools.FieldRefAccess<string>(activeCoverType, "stateDef");

            // Settings & Misc Accessors
            awModSettingsField = AccessTools.Field(awModType, "Settings");
            settingsMasterField = AccessTools.FieldRefAccess<bool>(awSettingsType, "master");
            settingsModeField = AccessTools.FieldRefAccess<int>(awSettingsType, "mode");
            settingsEmergencyCoverField = AccessTools.FieldRefAccess<bool>(awSettingsType, "emergencyCover");
            settingsEmergencyTriageField = AccessTools.FieldRefAccess<bool>(awSettingsType, "emergencyTriage");
            settingsWriteIntervalTicksField = AccessTools.FieldRefAccess<int>(awSettingsType, "writeIntervalTicks");
            settingsWritesPerPassField = AccessTools.FieldRefAccess<int>(awSettingsType, "writesPerPass");
            settingsSemiConfidenceField = AccessTools.FieldRefAccess<float>(awSettingsType, "semiConfidence");
            settingsToastsSuggestField = AccessTools.FieldRefAccess<bool>(awSettingsType, "toastsSuggest");
            settingsToastsLearningField = AccessTools.FieldRefAccess<bool>(awSettingsType, "toastsLearning");
            writeSettingsMethod = AccessTools.Method(typeof(ModSettings), nameof(ModSettings.Write));
            dropCellToastMethod = AccessTools.Method(toastsType, "DropCell", new[] { typeof(long) });
            emergencyCoverTuningMethod = AccessTools.Method(workTypeTuningDefType, "EmergencyCover", new[] { typeof(WorkTypeDef) });
            demandKindTuningMethod = AccessTools.Method(workTypeTuningDefType, "DemandKind", new[] { typeof(WorkTypeDef) });
            priorityRangesMaxProp = AccessTools.Property(priorityRangesType, "Max");
            emergencyViewSelField = AccessTools.StaticFieldRefAccess<WorkTypeDef>(AccessTools.Field(emergencyViewType, "sel"));
            stateDefProfileField = AccessTools.FieldRefAccess<IList>(emergencyStateDefType, "profile");
            stateDefOrderField = AccessTools.FieldRefAccess<int>(emergencyStateDefType, "order");
            profileEntryWorkTypeField = AccessTools.FieldRefAccess<string>(emergencyProfileEntryType, "workType");
            profileEntryPriorityField = AccessTools.FieldRefAccess<int>(emergencyProfileEntryType, "priority");

            // Register Sync Workers
            MP.RegisterSyncWorker<object>(SyncLearningStore, learningStoreType);
            MP.RegisterSyncWorker<object>(SyncProposal, proposalType, shouldConstruct: true);

            // Register Sync Methods - Builtin
            if (learningStoreApplyProposalMethod != null) MP.RegisterSyncMethod(learningStoreApplyProposalMethod);
            if (learningStoreSnoozeMethod != null) MP.RegisterSyncMethod(learningStoreSnoozeMethod);
            if (learningStoreNeverMethod != null) MP.RegisterSyncMethod(learningStoreNeverMethod);
            if (learningStoreResetLearningMethod != null) MP.RegisterSyncMethod(learningStoreResetLearningMethod);
            if (learningStoreUndoLastMethod != null) MP.RegisterSyncMethod(learningStoreUndoLastMethod);
            if (priorityWriterEnsureNumericModeMethod != null)
            {
                MP.RegisterSyncMethod(priorityWriterEnsureNumericModeMethod);
                MpCompat.harmony.Patch(
                    priorityWriterEnsureNumericModeMethod,
                    postfix: new HarmonyMethod(typeof(AdaptiveWorkPriorities), nameof(PostfixEnsureNumericMode)));
            }

            // Register Sync Methods - Compatibility Handlers
            MP.RegisterSyncMethod(typeof(AdaptiveWorkPriorities), nameof(SyncedCyclePawnMode));
            MP.RegisterSyncMethod(typeof(AdaptiveWorkPriorities), nameof(SyncedSetPawnMode));
            MP.RegisterSyncMethod(typeof(AdaptiveWorkPriorities), nameof(SyncedSetGlobalMode));
            MP.RegisterSyncMethod(typeof(AdaptiveWorkPriorities), nameof(SyncedSetMaster));
            MP.RegisterSyncMethod(typeof(AdaptiveWorkPriorities), nameof(SyncedToggleLock));
            MP.RegisterSyncMethod(typeof(AdaptiveWorkPriorities), nameof(SyncedSetChain));
            MP.RegisterSyncMethod(typeof(AdaptiveWorkPriorities), nameof(SyncedApplyAll));
            MP.RegisterSyncMethod(typeof(AdaptiveWorkPriorities), nameof(SyncedApplyManual));

            // Harmony Patches - PriorityWriter Manual Sync Interception
            if (priorityWriterApplyMethod != null)
            {
                MpCompat.harmony.Patch(
                    priorityWriterApplyMethod,
                    prefix: new HarmonyMethod(typeof(AdaptiveWorkPriorities), nameof(PrefixPriorityWriterApply)));
            }

            // Harmony Patches - Toasts Deterministic Ticks Throttling
            if (toastsType != null)
            {
                var notifySignalMethod = AccessTools.Method(toastsType, "NotifySignal");
                if (notifySignalMethod != null)
                {
                    MpCompat.harmony.Patch(
                        notifySignalMethod,
                        prefix: new HarmonyMethod(typeof(AdaptiveWorkPriorities), nameof(PrefixNotifySignal)));
                }

                var notifyAppliedMethod = AccessTools.Method(toastsType, "NotifyApplied");
                if (notifyAppliedMethod != null)
                {
                    MpCompat.harmony.Patch(
                        notifyAppliedMethod,
                        prefix: new HarmonyMethod(typeof(AdaptiveWorkPriorities), nameof(PrefixNotifyApplied)));
                }
            }

            // Harmony Patches - LearningStore Multi-Map Determinism
            MpCompat.harmony.Patch(
                AccessTools.Method(learningStoreType, "GameComponentTick"),
                prefix: new HarmonyMethod(typeof(AdaptiveWorkPriorities), nameof(PrefixLearningStoreTick)));

            MpCompat.harmony.Patch(
                AccessTools.Method(learningStoreType, "Coverage", new[] { typeof(WorkTypeDef), typeof(bool) }),
                prefix: new HarmonyMethod(typeof(AdaptiveWorkPriorities), nameof(PrefixLearningStoreCoverage)));

            MpCompat.harmony.Patch(
                AccessTools.Method(learningStoreType, "MarkAllDirty"),
                prefix: new HarmonyMethod(typeof(AdaptiveWorkPriorities), nameof(PrefixLearningStoreMarkAllDirty)));

            // Harmony Patches - Emergency Multi-Map Evaluation
            MpCompat.harmony.Patch(
                AccessTools.Method(emergencyManagerType, "Evaluate"),
                prefix: new HarmonyMethod(typeof(AdaptiveWorkPriorities), nameof(PrefixEmergencyManagerEvaluate)));

            MpCompat.harmony.Patch(
                AccessTools.Method(demandProbeType, "Refresh", new[] { typeof(Map) }),
                prefix: new HarmonyMethod(typeof(AdaptiveWorkPriorities), nameof(PrefixDemandProbeRefresh)));

            MpCompat.harmony.Patch(
                AccessTools.Method(demandProbeType, "HasDemand", new[] { typeof(Map), typeof(WorkTypeDef) }),
                prefix: new HarmonyMethod(typeof(AdaptiveWorkPriorities), nameof(PrefixDemandProbeHasDemand)));

            // Harmony Patches - UI Interceptions
            MpCompat.harmony.Patch(
                AccessTools.Method(pawnProfileType, "ToggleLock", new[] { typeof(WorkTypeDef) }),
                prefix: new HarmonyMethod(typeof(AdaptiveWorkPriorities), nameof(PrefixPawnProfileToggleLock)));

            MpCompat.harmony.Patch(
                AccessTools.Method(mainWindowType, "ApplyAllVisible"),
                prefix: new HarmonyMethod(typeof(AdaptiveWorkPriorities), nameof(PrefixApplyAllVisible)));

            MpCompat.harmony.Patch(
                AccessTools.Method(emergencyViewType, "DrawChainEditor"),
                prefix: new HarmonyMethod(typeof(AdaptiveWorkPriorities), nameof(PreDrawChainEditor)),
                postfix: new HarmonyMethod(typeof(AdaptiveWorkPriorities), nameof(PostDrawChainEditor)));

            if (mainWindowType != null)
            {
                var drawHeaderMethod = AccessTools.Method(mainWindowType, "DrawHeader", new[] { typeof(Rect) });
                if (drawHeaderMethod != null)
                {
                    MpCompat.harmony.Patch(
                        drawHeaderMethod,
                        prefix: new HarmonyMethod(typeof(AdaptiveWorkPriorities), nameof(PreDrawHeader)),
                        postfix: new HarmonyMethod(typeof(AdaptiveWorkPriorities), nameof(PostDrawHeader)));
                }
                else
                {
                    var doWindowContentsMethod = AccessTools.Method(mainWindowType, "DoWindowContents", new[] { typeof(Rect) });
                    if (doWindowContentsMethod != null)
                    {
                        MpCompat.harmony.Patch(
                            doWindowContentsMethod,
                            prefix: new HarmonyMethod(typeof(AdaptiveWorkPriorities), nameof(PreDrawHeader)),
                            postfix: new HarmonyMethod(typeof(AdaptiveWorkPriorities), nameof(PostDrawHeader)));
                    }
                }
            }

            if (awSettingsType != null)
            {
                var doWindowContentsMethod = AccessTools.Method(awSettingsType, "DoWindowContents", new[] { typeof(Rect) });
                if (doWindowContentsMethod != null)
                {
                    MpCompat.harmony.Patch(
                        doWindowContentsMethod,
                        prefix: new HarmonyMethod(typeof(AdaptiveWorkPriorities), nameof(PreDrawHeader)),
                        postfix: new HarmonyMethod(typeof(AdaptiveWorkPriorities), nameof(PostDrawHeader)));
                }
            }

            if (pawnCardType != null)
            {
                var drawIdentityMethod = AccessTools.Method(pawnCardType, "DrawIdentity");
                if (drawIdentityMethod != null)
                {
                    MpCompat.harmony.Patch(
                        drawIdentityMethod,
                        prefix: new HarmonyMethod(typeof(AdaptiveWorkPriorities), nameof(PreDrawIdentity)),
                        postfix: new HarmonyMethod(typeof(AdaptiveWorkPriorities), nameof(PostDrawIdentity)));
                }
            }
        }

        #region Sync Workers

        private static void SyncLearningStore(SyncWorker sync, ref object store)
        {
            if (!sync.isWriting)
            {
                store = GetLearningStore();
            }
        }

        private static void SyncProposal(SyncWorker sync, ref object obj)
        {
            if (sync.isWriting)
            {
                sync.Write(proposalPawnId(obj));
                sync.Write(proposalWt(obj));
                sync.Write(proposalFrom(obj));
                sync.Write(proposalTo(obj));
                sync.Write(proposalConf(obj));
                sync.Write(proposalAff(obj));
                sync.Write(proposalEvCount(obj));
                sync.Write(proposalSkill(obj));
                sync.Write(proposalPassion(obj));
                sync.Write(proposalCoverage(obj));
                sync.Write(proposalDemote(obj));
                sync.Write(proposalColdDays(obj));
            }
            else
            {
                proposalPawnId(obj) = sync.Read<int>();
                proposalWt(obj) = sync.Read<WorkTypeDef>();
                proposalFrom(obj) = sync.Read<int>();
                proposalTo(obj) = sync.Read<int>();
                proposalConf(obj) = sync.Read<float>();
                proposalAff(obj) = sync.Read<float>();
                proposalEvCount(obj) = sync.Read<int>();
                proposalSkill(obj) = sync.Read<int>();
                proposalPassion(obj) = sync.Read<int>();
                proposalCoverage(obj) = sync.Read<int>();
                proposalDemote(obj) = sync.Read<bool>();
                proposalColdDays(obj) = sync.Read<int>();
            }
        }

        #endregion

        #region Synced Methods

        [SyncMethod]
        public static void SyncedCyclePawnMode(Pawn pawn)
        {
            var store = GetLearningStore();
            if (store == null || pawn == null) return;
            var profile = learningStoreProfileForMethod?.Invoke(store, new object[] { pawn });
            if (profile == null) return;
            int curMode = pawnProfileModeField(profile);
            byte nextMode = (byte)((curMode + 1) % 3);
            pawnProfileModeField(profile) = nextMode;
            learningStoreMarkDirtyMethod?.Invoke(store, new object[] { pawn });
        }

        [SyncMethod]
        public static void SyncedSetPawnMode(Pawn pawn, int mode)
        {
            var store = GetLearningStore();
            if (store == null || pawn == null) return;
            var profile = learningStoreProfileForMethod?.Invoke(store, new object[] { pawn });
            if (profile == null) return;
            pawnProfileModeField(profile) = (byte)mode;
            learningStoreMarkDirtyMethod?.Invoke(store, new object[] { pawn });
        }

        [SyncMethod]
        public static void SyncedSetGlobalMode(int mode)
        {
            var settings = GetSettings();
            if (settings == null) return;
            settingsModeField(settings) = mode;
            writeSettingsMethod?.Invoke(settings, null);
            var store = GetLearningStore();
            if (store != null)
                learningStoreMarkAllDirtyMethod?.Invoke(store, null);
            Log.Message($"[MpCompat] AdaptiveWorkPriorities: SyncedSetGlobalMode applied mode={mode}");
        }

        [SyncMethod]
        public static void SyncedSetMaster(bool master)
        {
            var settings = GetSettings();
            if (settings == null) return;
            settingsMasterField(settings) = master;
            writeSettingsMethod?.Invoke(settings, null);
        }

        [SyncMethod]
        public static void SyncedToggleLock(Pawn pawn, WorkTypeDef wt)
        {
            var store = GetLearningStore();
            if (store == null || pawn == null || wt == null) return;
            var profile = learningStoreProfileForMethod?.Invoke(store, new object[] { pawn });
            if (profile == null) return;
            var lockedList = pawnProfileLockedField(profile);
            if (lockedList == null) return;

            if (lockedList.Contains(wt.defName))
                lockedList.Remove(wt.defName);
            else
                lockedList.Add(wt.defName);

            learningStoreMarkDirtyMethod?.Invoke(store, new object[] { pawn });
        }

        [SyncMethod]
        public static void SyncedSetChain(WorkTypeDef wt, List<int> pawnIds)
        {
            var store = GetLearningStore();
            if (store == null || wt == null) return;
            var chain = learningStoreChainForMethod?.Invoke(store, new object[] { wt }) as List<int>;
            if (chain == null) return;
            chain.Clear();
            if (pawnIds != null)
                chain.AddRange(pawnIds);
        }

        [SyncMethod]
        public static void SyncedApplyAll(Map map, int cohortInt)
        {
            var store = GetLearningStore();
            if (store == null || map == null) return;

            var cohortsType = AccessTools.TypeByName("AdaptiveWork.UI.Cohorts");
            var rosterMethod = AccessTools.Method(cohortsType, "Roster");
            if (rosterMethod == null) return;

            var roster = rosterMethod.Invoke(null, new object[] { cohortInt == 1 ? 1 : 0, map }) as List<Pawn>;
            if (roster == null) return;

            for (int i = 0; i < roster.Count; i++)
            {
                var proposals = learningStoreProposalsForMethod?.Invoke(store, new object[] { roster[i] }) as IList;
                if (proposals == null) continue;

                for (int num = proposals.Count - 1; num >= 0; num--)
                {
                    learningStoreApplyProposalMethod?.Invoke(store, new object[] { roster[i], proposals[num], true });
                }
            }
        }

        private static void PostfixEnsureNumericMode()
        {
            if (Current.Game?.playSettings == null) return;
            foreach (Pawn item in PawnsFinder.AllMapsWorldAndTemporary_Alive)
            {
                if (item.Faction == Faction.OfPlayer && item.workSettings != null)
                {
                    item.workSettings.Notify_UseWorkPrioritiesChanged();
                }
            }
            learningStoreMarkAllDirtyMethod?.Invoke(learningStoreInstanceProp?.GetValue(null), null);
        }

        [SyncMethod]
        public static void SyncedApplyManual(Pawn pawn, WorkTypeDef wt, int to)
        {
            priorityWriterApplyMethod?.Invoke(null, new object[] { pawn, wt, to, true, true });
        }

        #endregion

        #region Multi-Map Determinism Patches

        private static bool PrefixLearningStoreTick(object __instance)
        {
            if (!MP.IsInMultiplayer)
                return true;

            var settings = GetSettings();
            if (settings == null || !settingsMasterField(settings))
                return false;

            int ticksGame = Find.TickManager.TicksGame;
            if (ticksGame % 1000 == 0)
            {
                learningStoreTryEmitDigestMethod?.Invoke(__instance, new object[] { ticksGame });
            }

            emergencyManagerTickMethod?.Invoke(null, new object[] { ticksGame });

            int mode = settingsModeField(settings);
            // 0: Observe, 1: Suggest, 2: Semi, 3: Auto
            if (mode != 2 && mode != 3)
                return false;

            int interval = Mathf.Max(60, settingsWriteIntervalTicksField(settings));
            int lastPass = lastWriterPassAtField(__instance);
            if (lastPass >= 0 && ticksGame - lastPass < interval && lastPass <= ticksGame)
                return false;

            lastWriterPassAtField(__instance) = ticksGame;

            Faction ofPlayer = Faction.OfPlayerSilentFail;
            if (ofPlayer == null)
                return false;

            var schedulerRoster = schedulerRosterField(__instance);
            schedulerRoster.Clear();

            // Gather colonists across all player home maps deterministically
            var playerMaps = Find.Maps.Where(m => m.IsPlayerHome).OrderBy(m => m.uniqueID).ToList();
            if (playerMaps.Count == 0)
            {
                playerMaps = Find.Maps.Where(m => m.mapPawns.SpawnedPawnsInFaction(ofPlayer).Count > 0)
                                      .OrderBy(m => m.uniqueID).ToList();
            }

            foreach (var map in playerMaps)
            {
                var spawned = map.mapPawns.SpawnedPawnsInFaction(ofPlayer);
                spawned.SortBy(p => p.thingIDNumber);
                for (int i = 0; i < spawned.Count; i++)
                {
                    bool tracks = (bool)learningStoreTracksMethod.Invoke(null, new object[] { spawned[i] });
                    if (tracks)
                        schedulerRoster.Add(spawned[i]);
                }
            }

            if (schedulerRoster.Count == 0)
                return false;

            int bucket = (writerBucketField(__instance) + 1) % schedulerRoster.Count;
            writerBucketField(__instance) = bucket;

            Pawn pawn = (Pawn)schedulerRoster[bucket];
            var profile = learningStoreProfileForMethod?.Invoke(__instance, new object[] { pawn });
            if (profile == null || pawnProfileModeField(profile) != 0) // 0: Adaptive
                return false;

            try
            {
                currentEvaluatingMap = pawn.MapHeld ?? pawn.Map;

                var proposals = learningStoreProposalsForMethod?.Invoke(__instance, new object[] { pawn }) as IList;
                if (proposals == null || proposals.Count == 0)
                    return false;

                int writesAllowed = settingsWritesPerPassField(settings);
                float semiConf = settingsSemiConfidenceField(settings);

                int idx = proposals.Count - 1;
                while (idx >= 0 && writesAllowed > 0)
                {
                    var p = proposals[idx];
                    float conf = proposalConf(p);
                    if (mode != 2 || conf >= semiConf)
                    {
                        var wt = proposalWt(p);
                        int to = proposalTo(p);
                        bool applied = (bool)priorityWriterApplyMethod.Invoke(null, new object[] { pawn, wt, to, false, true });
                        if (applied)
                        {
                            proposals.RemoveAt(idx);
                            long cellKey = (long)proposalCellKeyProp.GetValue(p);
                            dropCellToastMethod?.Invoke(null, new object[] { cellKey });
                            writesAllowed--;
                        }
                    }
                    idx--;
                }
            }
            finally
            {
                currentEvaluatingMap = null;
            }

            return false;
        }

        private static bool PrefixLearningStoreCoverage(WorkTypeDef wt, bool mechs, ref int __result)
        {
            if (!MP.IsInMultiplayer)
                return true;

            Map map = currentEvaluatingMap ?? Find.CurrentMap;
            if (map == null || wt == null)
            {
                __result = 0;
                return false;
            }

            int now = Find.TickManager.TicksGame;
            if (now - lastCoverageTick > 250 || lastCoverageTick > now)
            {
                multiMapCoverage.Clear();
                lastCoverageTick = now;
            }

            int mechsInt = mechs ? 1 : 0;
            if (!multiMapCoverage.TryGetValue((map.uniqueID, mechsInt, wt.index), out __result))
            {
                // Compute coverage for all work types on this map
                var list = mechs ? map.mapPawns.SpawnedColonyMechs : map.mapPawns.FreeColonists;
                var allDefs = DefDatabase<WorkTypeDef>.AllDefsListForReading;

                for (int d = 0; d < allDefs.Count; d++)
                {
                    multiMapCoverage[(map.uniqueID, mechsInt, allDefs[d].index)] = 0;
                }

                for (int i = 0; i < list.Count; i++)
                {
                    Pawn p = list[i];
                    if (p?.workSettings == null || !p.workSettings.EverWork)
                        continue;

                    for (int j = 0; j < allDefs.Count; j++)
                    {
                        WorkTypeDef def = allDefs[j];
                        if (p.workSettings.GetPriority(def) > 0 && !p.WorkTypeIsDisabled(def))
                        {
                            multiMapCoverage.TryGetValue((map.uniqueID, mechsInt, def.index), out int count);
                            multiMapCoverage[(map.uniqueID, mechsInt, def.index)] = count + 1;
                        }
                    }
                }

                multiMapCoverage.TryGetValue((map.uniqueID, mechsInt, wt.index), out __result);
            }

            return false;
        }

        private static bool PrefixLearningStoreMarkAllDirty(object __instance)
        {
            if (!MP.IsInMultiplayer)
                return true;

            Faction ofPlayer = Faction.OfPlayerSilentFail;
            if (ofPlayer == null)
                return false;

            var dirtySet = storeDirtyField(__instance);
            foreach (var map in Find.Maps)
            {
                var pawns = map.mapPawns.SpawnedPawnsInFaction(ofPlayer);
                for (int i = 0; i < pawns.Count; i++)
                {
                    bool tracks = (bool)learningStoreTracksMethod.Invoke(null, new object[] { pawns[i] });
                    if (tracks)
                        dirtySet.Add(pawns[i].thingIDNumber);
                }
            }

            return false;
        }

        private static bool PrefixEmergencyManagerEvaluate()
        {
            if (!MP.IsInMultiplayer)
                return true;

            var settings = GetSettings();
            var instance = GetLearningStore();
            if (instance == null || Find.Maps.Count == 0)
            {
                emergencyManagerReleaseAllMethod?.Invoke(null, new object[] { null });
                return false;
            }

            if (settings == null || !settingsEmergencyCoverField(settings))
            {
                emergencyManagerReleaseAllMethod?.Invoke(null, new object[] { "feature off" });
                return false;
            }

            var homeMaps = Find.Maps.Where(m => m.IsPlayerHome).OrderBy(m => m.uniqueID).ToList();
            if (homeMaps.Count == 0)
            {
                homeMaps = Find.Maps.Where(m => m.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayerSilentFail).Count > 0)
                                   .OrderBy(m => m.uniqueID).ToList();
            }

            if (homeMaps.Count == 0)
            {
                emergencyManagerReleaseAllMethod?.Invoke(null, new object[] { null });
                return false;
            }

            var wanted = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(activeCoverType));
            var activeStatesList = emergencyActiveStatesField();
            var accumulatedStates = new List<object>();

            var allDefs = DefDatabase<WorkTypeDef>.AllDefsListForReading;
            int maxPriority = (int)priorityRangesMaxProp.GetValue(null);

            foreach (var map in homeMaps)
            {
                // Refresh demand probe & states for map
                PrefixDemandProbeRefresh(map);
                emergencyManagerRefreshStatesMethod?.Invoke(null, new object[] { map, settings });

                // Accumulate states for UI
                for (int s = 0; s < activeStatesList.Count; s++)
                {
                    var st = activeStatesList[s];
                    if (!accumulatedStates.Contains(st))
                        accumulatedStates.Add(st);
                }

                // Emergency Cover evaluation
                for (int i = 0; i < allDefs.Count; i++)
                {
                    WorkTypeDef wt = allDefs[i];
                    bool ec = (bool)emergencyCoverTuningMethod.Invoke(null, new object[] { wt });
                    bool hasDemand = false;
                    PrefixDemandProbeHasDemand(map, wt, ref hasDemand);
                    int coveredCount = (int)emergencyManagerCoveredCountMethod.Invoke(null, new object[] { map, wt });

                    if (ec && hasDemand && coveredCount <= 0)
                    {
                        object[] pickArgs = new object[] { map, instance, wt, (byte)0 };
                        Pawn picked = (Pawn)emergencyManagerPickFromChainMethod.Invoke(null, pickArgs);
                        byte source = (byte)pickArgs[3];

                        if (picked != null)
                        {
                            var cover = Activator.CreateInstance(activeCoverType);
                            activeCoverPawnId(cover) = picked.thingIDNumber;
                            activeCoverWt(cover) = wt.defName;
                            activeCoverApplied(cover) = 1;
                            activeCoverSource(cover) = source;
                            wanted.Add(cover);
                        }
                    }
                }

                // Emergency Triage evaluation
                if (settingsEmergencyTriageField(settings) && activeStatesList.Count > 0)
                {
                    var freeColonists = map.mapPawns.FreeColonistsSpawned;
                    for (int j = 0; j < activeStatesList.Count; j++)
                    {
                        var stateDef = activeStatesList[j];
                        var profileList = stateDefProfileField(stateDef);
                        if (profileList == null) continue;

                        for (int k = 0; k < profileList.Count; k++)
                        {
                            var entry = profileList[k];
                            string workTypeName = profileEntryWorkTypeField(entry);
                            WorkTypeDef wt = DefDatabase<WorkTypeDef>.GetNamedSilentFail(workTypeName);
                            if (wt == null) continue;

                            for (int l = 0; l < freeColonists.Count; l++)
                            {
                                Pawn colonist = freeColonists[l];
                                bool tracks = (bool)learningStoreTracksMethod.Invoke(null, new object[] { colonist });
                                if (tracks && !colonist.WorkTypeIsDisabled(wt) && !colonist.Downed && !colonist.InMentalState)
                                {
                                    int desiredPrio = Mathf.Clamp(profileEntryPriorityField(entry), 0, maxPriority);
                                    if (colonist.workSettings.GetPriority(wt) != desiredPrio &&
                                        !CoverExistsInList(wanted, colonist.thingIDNumber, wt.defName))
                                    {
                                        var cover = Activator.CreateInstance(activeCoverType);
                                        activeCoverPawnId(cover) = colonist.thingIDNumber;
                                        activeCoverWt(cover) = wt.defName;
                                        activeCoverApplied(cover) = desiredPrio;
                                        activeCoverSource(cover) = 2; // Triage
                                        activeCoverStateDef(cover) = ((Def)stateDef).defName;
                                        wanted.Add(cover);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Restore sorted accumulated states to EmergencyManager.ActiveStates for UI rendering
            activeStatesList.Clear();
            accumulatedStates.Sort((a, b) => stateDefOrderField(a).CompareTo(stateDefOrderField(b)));
            for (int i = 0; i < accumulatedStates.Count; i++)
                activeStatesList.Add(accumulatedStates[i]);

            emergencyManagerReconcileMethod?.Invoke(null, new object[] { instance, wanted });

            return false;
        }

        private static bool CoverExistsInList(IList list, int pawnId, string wt)
        {
            for (int i = 0; i < list.Count; i++)
            {
                var item = list[i];
                if (activeCoverPawnId(item) == pawnId && activeCoverWt(item) == wt)
                    return true;
            }
            return false;
        }

        private static bool PrefixDemandProbeRefresh(Map map)
        {
            if (!MP.IsInMultiplayer)
                return true;

            if (map == null)
                return false;

            int ticks = Find.TickManager.TicksGame;
            if (ticks != multiMapDemandTick)
            {
                multiMapDemandTick = ticks;
                multiMapDemand.Clear();
            }

            multiMapDemand[(map.uniqueID, "Tending")] = AnyNeedsTending(map);
            multiMapDemand[(map.uniqueID, "Fire")] = map.listerThings.ThingsOfDef(ThingDefOf.Fire).Count > 0;
            multiMapDemand[(map.uniqueID, "Food")] = AnyHungry(map);
            multiMapDemand[(map.uniqueID, "Prisoners")] = map.mapPawns.PrisonersOfColonySpawned.Count > 0;
            multiMapDemand[(map.uniqueID, "Always")] = true;

            return false;
        }

        private static bool PrefixDemandProbeHasDemand(Map map, WorkTypeDef wt, ref bool __result)
        {
            if (!MP.IsInMultiplayer)
                return true;

            if (map == null || wt == null)
            {
                __result = false;
                return false;
            }

            string kind = (string)demandKindTuningMethod?.Invoke(null, new object[] { wt });
            if (!string.IsNullOrEmpty(kind) && multiMapDemand.TryGetValue((map.uniqueID, kind), out bool val))
            {
                __result = val;
            }
            else
            {
                __result = false;
            }
            return false;
        }

        private static bool AnyNeedsTending(Map map)
        {
            try
            {
                var list = map.mapPawns.FreeColonistsAndPrisonersSpawned;
                for (int i = 0; i < list.Count; i++)
                {
                    if (HealthAIUtility.ShouldBeTendedNowByPlayer(list[i]))
                        return true;
                }
            }
            catch
            {
            }
            return false;
        }

        private static bool AnyHungry(Map map)
        {
            try
            {
                var list = map.mapPawns.FreeColonistsSpawned;
                for (int i = 0; i < list.Count; i++)
                {
                    Need_Food food = list[i].needs?.food;
                    if (food != null && food.CurLevelPercentage < 0.32f)
                        return true;
                }
            }
            catch
            {
            }
            return false;
        }

        #endregion

        #region UI Interceptions

        private static int lastAppliedAtTick = -99999;
        private static int lastLearnAtTick = -99999;

        private static bool PrefixPriorityWriterApply(Pawn pawn, WorkTypeDef wt, int to, bool manualGesture, bool recordUndo)
        {
            if (!MP.IsInMultiplayer)
                return true;

            if (MP.InInterface && manualGesture)
            {
                SyncedApplyManual(pawn, wt, to);
                return false;
            }

            return true;
        }

        private static bool PrefixNotifySignal(Pawn pawn, WorkTypeDef wt, object kind, string label)
        {
            if (!MP.IsInMultiplayer)
                return true;

            try
            {
                var settings = GetSettings();
                int ticksGame = Find.TickManager?.TicksGame ?? 0;
                int kindInt = kind != null ? Convert.ToInt32(kind) : 0;
                if (settings != null && settingsToastsLearningField != null && settingsToastsLearningField(settings) && kindInt != 2 && kindInt != 3 && (ticksGame - lastLearnAtTick >= 300 || lastLearnAtTick > ticksGame))
                {
                    lastLearnAtTick = ticksGame;
                    if (pawn != null && wt != null)
                    {
                        string wtLabel = wt.labelShort.NullOrEmpty() ? wt.label : wt.labelShort;
                        Messages.Message("AW.Msg.Learned".Translate(pawn.LabelShortCap, wtLabel), pawn, MessageTypeDefOf.SilentInput, false);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[MpCompat] AdaptiveWorkPriorities: Error in PrefixNotifySignal: {ex}");
            }

            return false;
        }

        private static bool PrefixNotifyApplied(Pawn pawn, WorkTypeDef wt, int from, int to)
        {
            if (!MP.IsInMultiplayer)
                return true;

            try
            {
                var settings = GetSettings();
                int ticksGame = Find.TickManager?.TicksGame ?? 0;
                if (settings != null && settingsToastsSuggestField != null && settingsToastsSuggestField(settings) && (ticksGame - lastAppliedAtTick >= 240 || lastAppliedAtTick > ticksGame))
                {
                    lastAppliedAtTick = ticksGame;
                    if (pawn != null && wt != null)
                    {
                        string wtLabel = wt.labelShort.NullOrEmpty() ? wt.label : wt.labelShort;
                        string fromStr = from == 0 ? (string)"AW.Off".Translate() : from.ToString();
                        string toStr = to == 0 ? (string)"AW.Off".Translate() : to.ToString();
                        Messages.Message("AW.Msg.Applied".Translate(pawn.LabelShortCap, wtLabel, fromStr, toStr), pawn, MessageTypeDefOf.SilentInput, false);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[MpCompat] AdaptiveWorkPriorities: Error in PrefixNotifyApplied: {ex}");
            }

            return false;
        }

        private static bool PrefixPawnProfileToggleLock(object __instance, WorkTypeDef wt)
        {
            if (!MP.IsInMultiplayer || MP.IsExecutingSyncCommand)
                return true;

            int pawnId = pawnProfileIdField(__instance);
            Pawn pawn = LearningStoreFindPawn(pawnId);
            if (pawn != null && wt != null)
            {
                SyncedToggleLock(pawn, wt);
            }

            return false;
        }

        private static bool PrefixApplyAllVisible(object cohort)
        {
            if (!MP.IsInMultiplayer)
                return true;

            try
            {
                int cohortInt = cohort != null ? Convert.ToInt32(cohort) : 0;
                SyncedApplyAll(Find.CurrentMap, cohortInt);
            }
            catch (Exception ex)
            {
                Log.Warning($"[MpCompat] AdaptiveWorkPriorities: Error in PrefixApplyAllVisible: {ex}");
            }

            return false;
        }

        private static void PreDrawChainEditor(object store, ref (WorkTypeDef wt, List<int> snapshot) __state)
        {
            if (!MP.IsInMultiplayer || store == null) return;
            var curSel = emergencyViewSelField();
            if (curSel != null)
            {
                var chain = learningStoreChainForMethod?.Invoke(store, new object[] { curSel }) as List<int>;
                if (chain != null)
                {
                    __state = (curSel, new List<int>(chain));
                }
            }
        }

        private static void PostDrawChainEditor(object store, (WorkTypeDef wt, List<int> snapshot) __state)
        {
            if (!MP.IsInMultiplayer || store == null || __state.wt == null || __state.snapshot == null) return;
            var chain = learningStoreChainForMethod?.Invoke(store, new object[] { __state.wt }) as List<int>;
            if (chain != null && !chain.SequenceEqual(__state.snapshot))
            {
                var newChain = new List<int>(chain);
                // Revert local mutation so that sync command applies it deterministically across all clients
                chain.Clear();
                chain.AddRange(__state.snapshot);
                SyncedSetChain(__state.wt, newChain);
            }
        }

        private static void PreDrawHeader(out (int mode, bool master) __state)
        {
            try
            {
                var settings = GetSettings();
                if (settings != null && settingsModeField != null && settingsMasterField != null)
                {
                    __state = (settingsModeField(settings), settingsMasterField(settings));
                    return;
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[MpCompat] AdaptiveWorkPriorities: Error in PreDrawHeader: {ex}");
            }
            __state = (-1, false);
        }

        private static void PostDrawHeader((int mode, bool master) __state)
        {
            if (!MP.IsInMultiplayer || __state.mode == -1) return;
            try
            {
                var settings = GetSettings();
                if (settings == null || settingsModeField == null || settingsMasterField == null) return;

                int curMode = settingsModeField(settings);
                if (curMode != __state.mode)
                {
                    Log.Message($"[MpCompat] AdaptiveWorkPriorities: Global mode changed from {__state.mode} to {curMode}, syncing.");
                    settingsModeField(settings) = __state.mode;
                    SyncedSetGlobalMode(curMode);
                }

                bool curMaster = settingsMasterField(settings);
                if (curMaster != __state.master)
                {
                    settingsMasterField(settings) = __state.master;
                    SyncedSetMaster(curMaster);
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[MpCompat] AdaptiveWorkPriorities: Error in PostDrawHeader: {ex}");
            }
        }

        private static void PreDrawIdentity(Pawn pawn, object store, out int __state)
        {
            __state = -1;
            if (!MP.IsInMultiplayer || pawn == null || store == null) return;
            var profile = learningStoreProfileForMethod?.Invoke(store, new object[] { pawn });
            if (profile != null)
            {
                __state = pawnProfileModeField(profile);
            }
        }

        private static void PostDrawIdentity(Pawn pawn, object store, int __state)
        {
            if (!MP.IsInMultiplayer || pawn == null || store == null || __state == -1) return;
            var profile = learningStoreProfileForMethod?.Invoke(store, new object[] { pawn });
            if (profile != null)
            {
                int curMode = pawnProfileModeField(profile);
                if (curMode != __state)
                {
                    pawnProfileModeField(profile) = (byte)__state;
                    SyncedSetPawnMode(pawn, curMode);
                }
            }
        }

        #endregion

        #region Helpers

        private static object GetLearningStore()
        {
            return learningStoreInstanceProp?.GetValue(null);
        }

        private static object GetSettings()
        {
            var settings = awModSettingsField?.GetValue(null);
            if (settings != null)
                return settings;

            if (awModType != null && awSettingsType != null)
            {
                var mod = LoadedModManager.GetMod(awModType);
                if (mod != null)
                {
                    var getSettingsMethod = AccessTools.Method(typeof(Mod), "GetSettings").MakeGenericMethod(awSettingsType);
                    return getSettingsMethod?.Invoke(mod, null);
                }
            }

            return null;
        }

        private static Pawn LearningStoreFindPawn(int id)
        {
            if (learningStoreFindPawnMethod != null)
                return (Pawn)learningStoreFindPawnMethod.Invoke(null, new object[] { id });

            Faction playerFaction = Faction.OfPlayerSilentFail;
            if (playerFaction == null) return null;
            var maps = Find.Maps;
            for (int i = 0; i < maps.Count; i++)
            {
                var list = maps[i].mapPawns.SpawnedPawnsInFaction(playerFaction);
                for (int j = 0; j < list.Count; j++)
                {
                    if (list[j].thingIDNumber == id)
                        return list[j];
                }
            }
            return null;
        }

        #endregion
    }
}
