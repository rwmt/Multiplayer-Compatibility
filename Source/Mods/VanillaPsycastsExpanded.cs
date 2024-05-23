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
    /// <summary>Vanilla Psycasts Expanded by erdelf, Oskar Potocki, legodude17, Taranchuk, xrushha, Sarg Bjornson, Sir Van, Reann Shepard</summary>
    /// <see href="https://github.com/Vanilla-Expanded/VanillaPsycastsExpanded"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2842502659"/>
    [MpCompatFor("VanillaExpanded.VPsycastsE")]
    public class VanillaPsycastsExpanded
    {
        #region Init

        public VanillaPsycastsExpanded(ModContentPack mod)
        {
            (Action patchMethod, string componentName, bool latePatch)[] patches =
            [
                (InitializeCommonData, "Shared patch data", false),
                (PatchPsysets, "Psysets", false),
                (PatchPsyringDialog, "Psyring", false),
                (RegisterSyncGizmos, "General gizmos", true),
                (PatchPsychicStatusGizmo, "Psychic status gizmo", true),
                (PatchPsycasterHediff, "Skill point usage", true),
                (PatchMotesAndFlecks, "Motes and flecks", true),
                (PatchITab, "Psychic abilities tab transpiler (psysets)", true),
            ];

            foreach (var (patchMethod, componentName, latePatch) in patches)
            {
                if (latePatch)
                    LongEventHandler.ExecuteWhenFinished(ApplyPatch);
                else
                    ApplyPatch();

                void ApplyPatch()
                {
                    try
                    {
#if DEBUG
                        Log.Message($"Patching Vanilla Psycasts Expanded - {componentName}");
#endif

                        patchMethod();
                    }
                    catch (Exception e)
                    {
                        Log.Error($"Encountered an error patching {componentName} part of Vanilla Expanded Framework - this part of the mod may not work properly!");
                        Log.Error(e.ToString());
                    }
                }
            }
        }

        #endregion

        #region Common

        private static ISyncField syncPsychicEntropyLimit;
        private static ISyncField syncPsychicEntropyTargetFocus;
        private static AccessTools.FieldRef<object, Pawn_PsychicEntropyTracker> psychicEntropyGetter;

        // Hediff_PsycastAbilities
        private static FastInvokeHandler removePsysetMethod;
        private static AccessTools.FieldRef<object, IList> hediffPsysetsList;

        private static void InitializeCommonData()
        {
            syncPsychicEntropyLimit = (ISyncField)AccessTools.Field("Multiplayer.Client.SyncFields:SyncPsychicEntropyLimit").GetValue(null);
            syncPsychicEntropyTargetFocus = (ISyncField)AccessTools.Field("Multiplayer.Client.SyncFields:SyncPsychicEntropyTargetFocus").GetValue(null);
        }

        #endregion

        #region Psychic status gizmo

        private static void PatchPsychicStatusGizmo()
        {
            var type = AccessTools.TypeByName("VanillaPsycastsExpanded.UI.PsychicStatusGizmo");
            psychicEntropyGetter = AccessTools.FieldRefAccess<Pawn_PsychicEntropyTracker>(type, "tracker");
            MpCompat.harmony.Patch(AccessTools.Method(type, "GizmoOnGUI"),
                prefix: new HarmonyMethod(typeof(VanillaPsycastsExpanded), nameof(PrePsyfocusTarget)),
                postfix: new HarmonyMethod(typeof(VanillaPsycastsExpanded), nameof(PostPsyfocusTarget)));
        }

        private static void PrePsyfocusTarget(object __instance)
        {
            if (!MP.IsInMultiplayer)
                return;

            MP.WatchBegin();

            var tracker = psychicEntropyGetter(__instance);
            if (tracker?.Pawn != null)
            {
                syncPsychicEntropyLimit.Watch(tracker.Pawn);
                syncPsychicEntropyTargetFocus.Watch(tracker.Pawn);
            }
        }

        private static void PostPsyfocusTarget()
        {
            if (MP.IsInMultiplayer)
                MP.WatchEnd();
        }

        #endregion

        #region Psysets

        // PsySet
        private static Type psysetType;
        private static AccessTools.FieldRef<object, IEnumerable> psysetAbilitiesField;
        private static AccessTools.FieldRef<object, string> psysetNameField;

        // Dialog_Psyset
        private static AccessTools.FieldRef<object, object> dialogPsysetPsysetField;
        private static AccessTools.FieldRef<object, Hediff> dialogPsysetHediffField;

        // Dialog_Psyset.<>c__DisplayClass10_0
        private static AccessTools.FieldRef<object, Window> innerClassDialogPsysetField;

        // HashSet<AbilityDef>
        private static Type abilityDefHashSetType;
        private static FastInvokeHandler abilityDefHashSetAddMethod;

        private static void PatchPsysets()
        {
            // Init
            MP.RegisterSyncMethod(typeof(VanillaPsycastsExpanded), nameof(SyncPsyset));
            MP.RegisterSyncMethod(typeof(VanillaPsycastsExpanded), nameof(SyncRemovePsyset));
            MP.RegisterSyncMethod(typeof(VanillaPsycastsExpanded), nameof(SyncEnsurePsysetExists));
            MP.RegisterSyncWorker<PsysetRenameHolder>(SyncPsysetRenameHolder);

            var abilityDefType = AccessTools.TypeByName("VFECore.Abilities.AbilityDef");
            abilityDefHashSetType = typeof(HashSet<>).MakeGenericType(abilityDefType);
            abilityDefHashSetAddMethod = MethodInvoker.GetHandler(AccessTools.Method(abilityDefHashSetType, "Add"));

            psysetType = AccessTools.TypeByName("VanillaPsycastsExpanded.PsySet");
            psysetAbilitiesField = AccessTools.FieldRefAccess<IEnumerable>(psysetType, "Abilities");
            psysetNameField = AccessTools.FieldRefAccess<string>(psysetType, "Name");

            // Psyset dialog
            var type = AccessTools.TypeByName("VanillaPsycastsExpanded.UI.Dialog_Psyset");

            dialogPsysetPsysetField = AccessTools.FieldRefAccess<object>(type, "psyset");
            dialogPsysetHediffField = AccessTools.FieldRefAccess<Hediff>(type, "hediff");

            MpCompat.harmony.Patch(AccessTools.GetDeclaredConstructors(type).FirstOrDefault(),
                postfix: new HarmonyMethod(typeof(VanillaPsycastsExpanded), nameof(PostPsysetDialogCtor)));
            MpCompat.harmony.Patch(AccessTools.Method(type, nameof(Window.DoWindowContents)),
                prefix: new HarmonyMethod(typeof(VanillaPsycastsExpanded), nameof(PrePsysetDoWindowContents)),
                postfix: new HarmonyMethod(typeof(VanillaPsycastsExpanded), nameof(PostPsysetDoWindowContents)));

            var method = MpMethodUtil.GetLambda(type, "DoWindowContents");
            type = method.DeclaringType;

            innerClassDialogPsysetField = AccessTools.FieldRefAccess<Window>(type, "<>4__this");

            MpCompat.harmony.Patch(method,
                prefix: new HarmonyMethod(typeof(VanillaPsycastsExpanded), nameof(PrePsysetInnerClassMethod)),
                postfix: new HarmonyMethod(typeof(VanillaPsycastsExpanded), nameof(PostPsysetInnerClassMethod)));
        }

        private static void ReplacedRemovePsyset(Hediff hediff, object psyset)
        {
            if (!MP.IsInMultiplayer)
            {
                removePsysetMethod(hediff, psyset);
                return;
            }

            var index = hediffPsysetsList(hediff).IndexOf(psyset);
            if (index >= 0)
                SyncRemovePsyset(hediff, index);
        }

        private static void SyncRemovePsyset(Hediff hediff, int index)
        {
            var psyset = hediffPsysetsList(hediff)[index];
            // We could technically remove it manually, but may as well call the original method in case it'll get some changes in the future
            removePsysetMethod(hediff, psyset);
        }

        private static void PostPsysetDialogCtor(object psyset, Hediff ___hediff)
        {
            var list = hediffPsysetsList(___hediff);
            // If the last psyset is the one we're editing in the dialog
            // May not always be a new one
            if (list[list.Count - 1] == psyset)
                SyncEnsurePsysetExists(___hediff, list.Count);
        }

        private static void SyncEnsurePsysetExists(Hediff hediff, int count)
        {
            var list = hediffPsysetsList(hediff);
            // Matches the expected count
            if (list.Count == count)
                return;

            var psyset = Activator.CreateInstance(psysetType);
            psysetNameField(psyset) = "VPE.Untitled".Translate();
            list.Add(psyset);
        }

        // Inside DoWindowContents for Dialog_Psyset, we can remove one of the psysets
        private static void PrePsysetDoWindowContents(object ___psyset, ref int __state)
        {
            if (!MP.IsInMultiplayer || ___psyset == null)
                return;

            __state = psysetAbilitiesField(___psyset).EnumerableCount();
        }

        private static void PostPsysetDoWindowContents(object ___psyset, Hediff ___hediff, ref int __state)
        {
            if (!MP.IsInMultiplayer || ___psyset == null || ___hediff == null)
                return;

            CheckMatch(___psyset, ___hediff, __state);
        }

        // Inside of the inner class inside of Dialog_Psyset, we can add new ones
        // It's not handled by the previous case, as it's done using DragAndDropWidget.DropArea
        private static void PrePsysetInnerClassMethod(object __instance, ref int __state)
        {
            if (!MP.IsInMultiplayer)
                return;

            var dialog = innerClassDialogPsysetField(__instance);
            var psyset = dialogPsysetPsysetField(dialog);

            __state = psysetAbilitiesField(psyset).EnumerableCount();
        }

        private static void PostPsysetInnerClassMethod(object __instance, ref int __state)
        {
            if (!MP.IsInMultiplayer)
                return;

            var dialog = innerClassDialogPsysetField(__instance);
            var psyset = dialogPsysetPsysetField(dialog);
            var hediff = dialogPsysetHediffField(dialog);

            CheckMatch(psyset, hediff, __state);
        }

        // Used by our methods handling Dialog_Psyset and its inner class to sync psyset if needed
        private static void CheckMatch(in object psyset, in Hediff hediff, in int count)
        {
            var current = psysetAbilitiesField(psyset)
                .Cast<Def>()
                .ToArray();

            if (count != current.Length)
            {
                var index = hediffPsysetsList(hediff).IndexOf(psyset);
                if (index >= 0)
                    SyncPsyset(hediff, index, current);
            }
        }

        private static void SyncPsyset(Hediff hediff, int psysetIndex, Def[] defs)
        {
            var psyset = hediffPsysetsList(hediff)[psysetIndex];

            var set = Activator.CreateInstance(abilityDefHashSetType);
            foreach (var def in defs)
                abilityDefHashSetAddMethod(set, def);
            psysetAbilitiesField(psyset) = (IEnumerable)set;
        }

        private static IEnumerable<CodeInstruction> PatchPsycastsITab(IEnumerable<CodeInstruction> instr)
        {
            var target = AccessTools.Method("VanillaPsycastsExpanded.Hediff_PsycastAbilities:RemovePsySet");
            var replacement = AccessTools.Method(typeof(VanillaPsycastsExpanded), nameof(ReplacedRemovePsyset));

            var dialogRenameType = AccessTools.TypeByName("VanillaPsycastsExpanded.UI.Dialog_RenamePsyset");
            var replacedDialogRenameType = AccessTools.DeclaredConstructor(typeof(Dialog_RenamePsysetMp),
                [typeof(IRenameable), typeof(Hediff)]);

            var originalHediffField = AccessTools.Field("VanillaPsycastsExpanded.UI.ITab_Pawn_Psycasts:hediff");

            foreach (var ci in instr)
            {
                if (ci.opcode == OpCodes.Callvirt && ci.operand is MethodInfo method && method == target)
                {
                    ci.opcode = OpCodes.Call;
                    ci.operand = replacement;
                }
                else if (ci.opcode == OpCodes.Newobj && ci.operand is ConstructorInfo ctor && ctor.DeclaringType == dialogRenameType)
                {
                    // Load the current hediff onto the stack
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldfld, originalHediffField);
                
                    // Replace the VE rename dialog with our own
                    ci.operand = replacedDialogRenameType;
                }

                yield return ci;
            }
        }

        private static void SyncPsysetRenameHolder(SyncWorker sync, ref PsysetRenameHolder holder)
        {
            if (sync.isWriting)
            {
                sync.Write(holder.hediff);
                if (holder.hediff != null)
                    sync.Write(hediffPsysetsList(holder.hediff).IndexOf(holder.psyset));
            }
            else
            {
                var hediff = sync.Read<Hediff>();
                IRenameable psyset = null;

                if (hediff != null)
                {
                    var index = sync.Read<int>();
                    var list = hediffPsysetsList(hediff);
                    if (index >= 0 && index < list.Count)
                        psyset = list[index] as IRenameable;
                }

                holder = new PsysetRenameHolder(psyset, hediff);
            }
        }

        // Our syncable holder for the psyset. We need the hediff itself to sync the psyset,
        // so we need a custom solution for syncing.
        private class PsysetRenameHolder(IRenameable psyset, Hediff hediff) : IRenameable
        {
            public readonly IRenameable psyset = psyset;
            public readonly Hediff hediff = hediff;

            // The setter should be automatically synced by MP, since the type is syncable (#443)
            public string RenamableLabel
            {
                get => psyset?.RenamableLabel ?? string.Empty;
                set
                {
                    if (psyset != null) psyset.RenamableLabel = value;
                }
            }

            public string BaseLabel => psyset?.BaseLabel ?? string.Empty;
            public string InspectLabel => psyset?.InspectLabel ?? string.Empty;
        }

        // Rename dialog for PsysetRenameHolder
        private class Dialog_RenamePsysetMp(IRenameable psyset, Hediff hediff) : Dialog_Rename<PsysetRenameHolder>(new PsysetRenameHolder(psyset, hediff));

        #endregion

        #region Psyring

        // Dialog_CreatePsyring
        private static Type createPsyringDialogType;
        private static AccessTools.FieldRef<object, Pawn> createPsyringPawnField;
        private static AccessTools.FieldRef<object, Thing> createPsyringFuelField;

        private static void PatchPsyringDialog()
        {
            createPsyringDialogType = AccessTools.TypeByName("VanillaPsycastsExpanded.Technomancer.Dialog_CreatePsyring");
            createPsyringPawnField = AccessTools.FieldRefAccess<Pawn>(createPsyringDialogType, "pawn");
            createPsyringFuelField = AccessTools.FieldRefAccess<Thing>(createPsyringDialogType, "fuel");

            MP.RegisterSyncWorker<object>(SyncDialogCreatePsyring, createPsyringDialogType);
            MP.RegisterSyncMethod(createPsyringDialogType, "Create");

            DialogUtilities.RegisterDialogCloseSync(createPsyringDialogType, true);
        }

        private static void SyncDialogCreatePsyring(SyncWorker sync, ref object dialog)
        {
            if (sync.isWriting)
            {
                sync.Write(createPsyringPawnField(dialog));
                sync.Write(createPsyringFuelField(dialog));
            }
            else
            {
                var pawn = sync.Read<Pawn>();
                var fuel = sync.Read<Thing>();

                // When Dialog_CreatePsyring.Create() is called next tick this dialog is already gone from WindowStack, this is disposable.
                // dialog = createPsyringDialogConstructor.Invoke(new object[] { pawn, fuel });
                dialog = Activator.CreateInstance(createPsyringDialogType, pawn, fuel, null);
            }
        }

        #endregion

        #region Psycaster hediff

        private static void PatchPsycasterHediff()
        {
            var type = AccessTools.TypeByName("VanillaPsycastsExpanded.Hediff_PsycastAbilities");

            removePsysetMethod = MethodInvoker.GetHandler(AccessTools.Method(type, "RemovePsySet"));
            hediffPsysetsList = AccessTools.FieldRefAccess<IList>(type, "psysets");

            MP.RegisterSyncMethod(type, "SpentPoints");
            MP.RegisterSyncMethod(type, "ImproveStats");
            MP.RegisterSyncMethod(type, "UnlockPath");
            MP.RegisterSyncMethod(type, "UnlockMeditationFocus");
            MP.RegisterSyncMethod(type, "GainExperience");
            // MP.RegisterSyncMethod(type, "RemovePsySet");

            // Active Psyset changes don't need syncing - they could, but it may end up annoying
            // MpCompat.RegisterLambdaDelegate(type, "GetPsySetGizmos", 0);
            // MpCompat.RegisterLambdaDelegate(type, "GetPsySetFloatMenuOptions", 0);
        }

        #endregion

        #region Other

        private static void RegisterSyncGizmos()
        {
            // Gizmos
            MpCompat.RegisterLambdaMethod("VanillaPsycastsExpanded.CompBreakLink", "GetGizmos", 0);
            MpCompat.RegisterLambdaDelegate("VanillaPsycastsExpanded.Ability_GuardianSkipBarrier", "GetGizmo", 0);

            // Destroy
            MpCompat.RegisterLambdaMethod("VanillaPsycastsExpanded.Skipmaster.Skipdoor", "GetDoorTeleporterGismoz", 0).SetContext(SyncContext.None);
        }

        private static void PatchMotesAndFlecks()
        {
            // Uses RNG after GenView.ShouldSpawnMotesAt, gonna cause desyncs
            PatchingUtilities.PatchPushPopRand(new[]
            {
                "VanillaPsycastsExpanded.FixedTemperatureZone:ThrowFleck",
                "VanillaPsycastsExpanded.Conflagrator.FireTornado:ThrowPuff",
            });
        }

        private static void PatchITab()
        {
            MpCompat.harmony.Patch(AccessTools.Method("VanillaPsycastsExpanded.UI.ITab_Pawn_Psycasts:DoPsysets"),
                transpiler: new HarmonyMethod(typeof(VanillaPsycastsExpanded), nameof(PatchPsycastsITab)));
        }

        #endregion
    }
}