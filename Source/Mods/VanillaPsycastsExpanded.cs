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
        private static Dictionary<int, Thing> thingsById;

        private static ISyncField syncPsychicEntropyLimit;
        private static ISyncField syncPsychicEntropyTargetFocus;
        private static AccessTools.FieldRef<object, Pawn_PsychicEntropyTracker> psychicEntropyGetter;
        private static Hediff currentHediff;

        // Hediff_PsycastAbilities
        private static FastInvokeHandler removePsysetMethod;
        private static AccessTools.FieldRef<object, IList> hediffPsysetsList;

        // PsySet
        private static Type psysetType;
        private static AccessTools.FieldRef<object, IEnumerable> psysetAbilitiesField;
        private static AccessTools.FieldRef<object, string> psysetNameField;

        // Dialog_Psyset
        private static AccessTools.FieldRef<object, object> dialogPsysetPsysetField;
        private static AccessTools.FieldRef<object, Hediff> dialogPsysetHediffField;

        // Dialog_Psyset.<>c__DisplayClass10_0
        private static AccessTools.FieldRef<object, Window> innerClassDialogPsysetField;

        // Dialog_RenameSkipdoor
        private static Type renameSkipdoorDialogType;
        private static ConstructorInfo renameSkipdoorDialogConstructor;
        private static AccessTools.FieldRef<object, ThingWithComps> renameSkipdoorDialogSkipdoorField;

        // Dialog_CreatePsyring
        private static Type createPsyringDialogType;
        private static ConstructorInfo createPsyringDialogConstructor;
        private static AccessTools.FieldRef<object, Pawn> createPsyringPawnField;
        private static AccessTools.FieldRef<object, Thing> createPsyringFuelField;
        
        // Skipdoor.<>c__DisplayClass35_0
        private static Type innerClassSkipdoorLocalsType;
        private static AccessTools.FieldRef<object, ThingWithComps> innerClassSkipdoorThisField;
        private static AccessTools.FieldRef<object, Pawn> innerClassSkipdoorPawnField;

        // Skipdoor.<>c__DisplayClass35_1
        private static AccessTools.FieldRef<object, object> innerClassSkipdoorLocalsField;
        private static AccessTools.FieldRef<object, Thing> innerClassSkipdoorTargetField;

        // HashSet<AbilityDef>
        private static Type abilityDefHashSetType;
        private static FastInvokeHandler abilityDefHashSetAddMethod;

        public VanillaPsycastsExpanded(ModContentPack mod)
        {
            thingsById = (Dictionary<int, Thing>)AccessTools.Field(AccessTools.TypeByName("Multiplayer.Client.ThingsById"), "thingsById").GetValue(null);
            
            syncPsychicEntropyLimit = (ISyncField)AccessTools.Field("Multiplayer.Client.SyncFields:SyncPsychicEntropyLimit").GetValue(null);
            syncPsychicEntropyTargetFocus = (ISyncField)AccessTools.Field("Multiplayer.Client.SyncFields:SyncPsychicEntropyTargetFocus").GetValue(null);

            psysetType = AccessTools.TypeByName("VanillaPsycastsExpanded.PsySet");
            psysetAbilitiesField = AccessTools.FieldRefAccess<IEnumerable>(psysetType, "Abilities");
            psysetNameField = AccessTools.FieldRefAccess<string>(psysetType, "Name");

            var abilityDefType = AccessTools.TypeByName("VFECore.Abilities.AbilityDef");
            abilityDefHashSetType = typeof(HashSet<>).MakeGenericType(abilityDefType);
            abilityDefHashSetAddMethod = MethodInvoker.GetHandler(AccessTools.Method(abilityDefHashSetType, "Add"));

            // Psyset dialog
            {
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

            // Set name dialog
            {
                MpCompat.harmony.Patch(AccessTools.Method("VanillaPsycastsExpanded.UI.Dialog_RenamePsyset:SetName"),
                    prefix: new HarmonyMethod(typeof(VanillaPsycastsExpanded), nameof(PreSetPsysetName)));
            }

            // CreatePsyring dialog
            {
                createPsyringDialogType = AccessTools.TypeByName("VanillaPsycastsExpanded.Technomancer.Dialog_CreatePsyring");
                createPsyringDialogConstructor = AccessTools.DeclaredConstructor(createPsyringDialogType, new [] { typeof(Pawn), typeof(Thing) });
                createPsyringPawnField = AccessTools.FieldRefAccess<Pawn>(createPsyringDialogType, "pawn");
                createPsyringFuelField = AccessTools.FieldRefAccess<Thing>(createPsyringDialogType, "fuel");

                MP.RegisterSyncWorker<object>(SyncDialogCreatePsyring, createPsyringDialogType);
                MP.RegisterSyncMethod(createPsyringDialogType, "Create");

                DialogUtilities.RegisterDialogCloseSync(createPsyringDialogType, true);
            }

            // Sync methods
            {
                MP.RegisterSyncMethod(typeof(VanillaPsycastsExpanded), nameof(SyncPsyset));
                MP.RegisterSyncMethod(typeof(VanillaPsycastsExpanded), nameof(SyncRemovePsyset));
                MP.RegisterSyncMethod(typeof(VanillaPsycastsExpanded), nameof(SyncEnsurePsysetExists));
                MP.RegisterSyncMethod(typeof(VanillaPsycastsExpanded), nameof(SyncRenamePsyset));
            }

            // Motes/Flecks
            {
                // Uses RNG after GenView.ShouldSpawnMotesAt, gonna cause desyncs
                PatchingUtilities.PatchPushPopRand("VanillaPsycastsExpanded.FixedTemperatureZone:ThrowFleck");
            }

            // Gizmos
            {
                MpCompat.RegisterLambdaMethod("VanillaPsycastsExpanded.CompBreakLink", "GetGizmos", 0);
                MpCompat.RegisterLambdaDelegate("VanillaPsycastsExpanded.Ability_GuardianSkipBarrier", "GetGizmo", 0);
            }

            LongEventHandler.ExecuteWhenFinished(LatePatch);
        }

        private static void LatePatch()
        {
            // Status gizmo
            {
                var type = AccessTools.TypeByName("VanillaPsycastsExpanded.UI.PsychicStatusGizmo");
                psychicEntropyGetter = AccessTools.FieldRefAccess<Pawn_PsychicEntropyTracker>(type, "tracker");
                MpCompat.harmony.Patch(AccessTools.Method(type, "GizmoOnGUI"),
                    prefix: new HarmonyMethod(typeof(VanillaPsycastsExpanded), nameof(PrePsyfocusTarget)),
                    postfix: new HarmonyMethod(typeof(VanillaPsycastsExpanded), nameof(PostPsyfocusTarget)));
            }

            // Hediff
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

            // ITab
            {
                MpCompat.harmony.Patch(AccessTools.Method("VanillaPsycastsExpanded.UI.ITab_Pawn_Psycasts:DoPsysets"),
                    transpiler: new HarmonyMethod(typeof(VanillaPsycastsExpanded), nameof(Transpiler)));
            }

            // Motes/Flecks
            {
                // Uses RNG after GenView.ShouldSpawnMotesAt, gonna cause desyncs
                PatchingUtilities.PatchPushPopRand("VanillaPsycastsExpanded.Conflagrator.FireTornado:ThrowPuff");
            }

            // Skipdoor 
            {
                var type = AccessTools.TypeByName("VanillaPsycastsExpanded.Skipmaster.Skipdoor");

                // Destroy
                MpCompat.RegisterLambdaMethod(type, nameof(ThingWithComps.GetGizmos), 0).SetContext(SyncContext.None);
                // Teleport to x
                MpCompat.RegisterLambdaDelegate(type, nameof(ThingWithComps.GetFloatMenuOptions), 0);

                renameSkipdoorDialogType = AccessTools.TypeByName("VanillaPsycastsExpanded.Skipmaster.Dialog_RenameSkipdoor");
                renameSkipdoorDialogConstructor = AccessTools.DeclaredConstructor(renameSkipdoorDialogType, new[] { type });
                renameSkipdoorDialogSkipdoorField = AccessTools.FieldRefAccess<ThingWithComps>(renameSkipdoorDialogType, "Skipdoor");

                PatchingUtilities.PatchPushPopRand(renameSkipdoorDialogConstructor);
                MP.RegisterSyncWorker<Dialog_Rename>(SyncDialogRenameSkipdoor, renameSkipdoorDialogType);
                MP.RegisterSyncMethod(renameSkipdoorDialogType, nameof(Dialog_Rename.SetName))
                    // Since we sync the "SetName" method and nothing else, it'll leave the dialog open for
                    // players who didn't click the button to rename it - we need to manually close it.
                    .SetPostInvoke((dialog, _) =>
                    {
                        if (dialog is Window w)
                            Find.WindowStack.TryRemove(w);
                    });

                var innerClassMethod = MpMethodUtil.GetLambda(type, nameof(ThingWithComps.GetFloatMenuOptions));

                if (innerClassMethod == null)
                    Log.Error("Couldn't find inner class 1 for skipdoor, skipdoors won't work.");
                else
                {
                    var fields = AccessTools.GetDeclaredFields(innerClassMethod.DeclaringType);
                    if (fields.Count != 2)
                        Log.Error($"Found incorrect amount of fields while trying to register skipdoor (inner class 1) - found: {fields.Count}, expected: 2.");
                    
                    foreach (var field in fields)
                    {
                        if (field.FieldType == type)
                            innerClassSkipdoorTargetField = AccessTools.FieldRefAccess<object, Thing>(field);
                        else
                        {
                            innerClassSkipdoorLocalsType = field.FieldType;
                            innerClassSkipdoorLocalsField = AccessTools.FieldRefAccess<object, object>(field);
                        }
                    }

                    if (innerClassSkipdoorLocalsType == null)
                    {
                        Log.Error("Couldn't find inner class 0 for skipdoor, skipdoors won't work.");
                    }
                    else
                    {
                        fields = AccessTools.GetDeclaredFields(innerClassSkipdoorLocalsType);
                        if (fields.Count != 2)
                            Log.Error($"Found incorrect amount of fields while trying to register skipdoor (inner class 0) - found: {fields.Count}, expected: 2.");

                        foreach (var field in fields)
                        {
                            if (field.FieldType == type)
                                innerClassSkipdoorThisField = AccessTools.FieldRefAccess<object, ThingWithComps>(field);
                            else if (field.FieldType == typeof(Pawn))
                                innerClassSkipdoorPawnField = AccessTools.FieldRefAccess<object, Pawn>(field);
                        }
                        
                        MP.RegisterSyncWorker<object>(SyncInnerSkipdoorClass, innerClassMethod.DeclaringType, shouldConstruct: true);
                        MP.RegisterSyncMethod(innerClassMethod);
                    }
                }
            }
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

        private static bool PreSetPsysetName(string name, object ___psyset)
        {
            if (!MP.IsInMultiplayer || currentHediff == null)
                return true;

            // We use currentHediff to sync the psyset being renamed
            // No way to access hediff/pawn/anything useful from this dialog or psyset itself
            var hediff = currentHediff;
            currentHediff = null;
            var index = hediffPsysetsList(hediff).IndexOf(___psyset);
            if (index < 0)
                return true;

            SyncRenamePsyset(hediff, index, name);

            return false;
        }

        private static void SyncRenamePsyset(Hediff hediff, int index, string name)
        {
            var psyset = hediffPsysetsList(hediff)[index];
            psysetNameField(psyset) = name;
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
                dialog = createPsyringDialogConstructor.Invoke(new object[] { pawn, fuel });
            }
        }

        private static void SyncDialogRenameSkipdoor(SyncWorker sync, ref Dialog_Rename dialog)
        {
            if (sync.isWriting)
            {
                sync.Write(renameSkipdoorDialogSkipdoorField(dialog));
                sync.Write(dialog.curName);
            }
            else
            {
                var skipdoor = sync.Read<ThingWithComps>();
                var name = sync.Read<string>();

                // The dialog may be already open
                dialog = Find.WindowStack.Windows.FirstOrDefault(x => x.GetType() == renameSkipdoorDialogType) as Dialog_Rename;
                // If the dialog is not open, or the open dialog is for a different skipdoor - create a new dialog instead
                if (dialog == null || renameSkipdoorDialogSkipdoorField(dialog) != skipdoor)
                    dialog = (Dialog_Rename)renameSkipdoorDialogConstructor.Invoke(new object[] { skipdoor });

                dialog.curName = name;
            }
        }

        private static void SyncInnerSkipdoorClass(SyncWorker sync, ref object obj)
        {
            if (sync.isWriting)
            {
                var locals = innerClassSkipdoorLocalsField(obj);
                var target = innerClassSkipdoorTargetField(obj);
                
                // The target is on a different map, so we can't just sync it as MP does not allow it.
                // We need to sync the ID number and manually get the target by ID instead.
                sync.Write(target.thingIDNumber);
                sync.Write(innerClassSkipdoorThisField(locals));
                sync.Write(innerClassSkipdoorPawnField(locals));
            }
            else
            {
                // shouldConstruct: true, so obj is constructed
                // but we need to construct the other object used for locals
                var locals = Activator.CreateInstance(innerClassSkipdoorLocalsType);
                innerClassSkipdoorLocalsField(obj) = locals;

                // Get the target by ID.
                innerClassSkipdoorTargetField(obj) = thingsById.GetValueSafe(sync.Read<int>());
                innerClassSkipdoorThisField(locals) = sync.Read<ThingWithComps>();
                innerClassSkipdoorPawnField(locals) = sync.Read<Pawn>();
            }
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instr)
        {
            var codeInstructions = instr as CodeInstruction[] ?? instr.ToArray();

            var target = AccessTools.Method("VanillaPsycastsExpanded.Hediff_PsycastAbilities:RemovePsySet");
            var replacement = AccessTools.Method(typeof(VanillaPsycastsExpanded), nameof(ReplacedRemovePsyset));

            var dialogRenameType = AccessTools.TypeByName("VanillaPsycastsExpanded.UI.Dialog_RenamePsyset");

            var originalHediffField = AccessTools.Field("VanillaPsycastsExpanded.UI.ITab_Pawn_Psycasts:hediff");
            var ourHediffField = AccessTools.Field(typeof(VanillaPsycastsExpanded), nameof(currentHediff));

            for (var i = 0; i < codeInstructions.Length; i++)
            {
                var ci = codeInstructions[i];

                if (ci.opcode == OpCodes.Callvirt && ci.operand is MethodInfo method && method == target)
                {
                    ci.opcode = OpCodes.Call;
                    ci.operand = replacement;
                }
                else if (ci.opcode == OpCodes.Newobj && ci.operand is ConstructorInfo ctor && ctor.DeclaringType == dialogRenameType)
                {
                    // Skip the current and next instruction
                    yield return ci;
                    yield return codeInstructions[++i];

                    // Get the hediff field and set our static one to it.
                    // We use it for syncing, as we need it for syncing PsySet,
                    // and it has no way to reference it directly.
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldfld, originalHediffField);
                    ci = new CodeInstruction(OpCodes.Stsfld, ourHediffField);
                }

                yield return ci;
            }
        }
    }
}