using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Inventory;
using Multiplayer.API;
using RimWorld;
using UnityEngine;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Compositable Loadouts by Wiri</summary>
    /// <see href="https://github.com/simplyWiri/Loadout-Compositing"/>
    /// <see href="https://steamcommunity.com/workshop/filedetails/?id=2679126859"/>
    [MpCompatFor("Wiri.compositableloadouts")]
    public class CompositableLoadouts
    {
        #region Fields

        // RitualData Write/Read Delegate
        private static FastInvokeHandler writeDelegateMethod;
        private static FastInvokeHandler readDelegateMethod;
        private static FastInvokeHandler checkMethodAllowedMethod;

        // WritingSyncWorker:writer/ReadingSyncWorker:reader
        private static AccessTools.FieldRef<SyncWorker, object> syncWorkerWriterField;
        private static AccessTools.FieldRef<SyncWorker, object> syncWorkerReaderField;

        private static Tag currentTag = null;
        private static readonly List<ItemStateCopy> stateCopies = new();

        private static float nextCallAllowedTime = float.NegativeInfinity;

        #endregion

        #region Init

        public CompositableLoadouts(ModContentPack mod)
        {
            LongEventHandler.ExecuteWhenFinished(LatePatch);

            // MP stuff
            writeDelegateMethod = MethodInvoker.GetHandler(AccessTools.DeclaredMethod("Multiplayer.Client.DelegateSerialization:WriteDelegate"));
            readDelegateMethod = MethodInvoker.GetHandler(AccessTools.DeclaredMethod("Multiplayer.Client.DelegateSerialization:ReadDelegate"));
            checkMethodAllowedMethod = MethodInvoker.GetHandler(AccessTools.DeclaredMethod("Multiplayer.Client.DelegateSerialization:CheckMethodAllowed"));

            var field = AccessTools.DeclaredField("Multiplayer.Client.DelegateSerialization:allowedDeclaringTypes");
            var arr = (Type[])field.GetValue(null);
            arr = arr.Concat(new[]
            {
                typeof(Dialog_TagEditor),
                typeof(Dialog_LoadoutEditor),
                typeof(LoadoutComponent),
                typeof(Panel_BillConfig),
                typeof(PawnColumnWorker_LoadoutState),
            }).ToArray();
            field.SetValue(null, arr);

            syncWorkerWriterField = AccessTools.FieldRefAccess<object>("Multiplayer.Client.WritingSyncWorker:writer");
            syncWorkerReaderField = AccessTools.FieldRefAccess<object>("Multiplayer.Client.ReadingSyncWorker:reader");

            // Loadout manager
            MP.RegisterSyncMethod(typeof(LoadoutManager), nameof(LoadoutManager.SetPanicState));
            MP.RegisterSyncMethod(typeof(LoadoutManager), nameof(LoadoutManager.RemoveState)).SetPostInvoke(RemoveEmptyStateFromStateEditor);
            MP.RegisterSyncMethod(typeof(LoadoutManager), nameof(LoadoutManager.TogglePanicMode));
            MP.RegisterSyncMethod(typeof(LoadoutManager), nameof(LoadoutManager.SetTagForBill));
            MP.RegisterSyncMethod(typeof(LoadoutManager), nameof(LoadoutManager.AddTag));
            MP.RegisterSyncMethod(typeof(LoadoutManager), nameof(LoadoutManager.RemoveTag));
            MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(LoadoutManager), nameof(LoadoutManager.AddTag)),
                prefix: new HarmonyMethod(typeof(CompositableLoadouts), nameof(StopDuplicateTagAdditionToManager)));

            // LoadoutState
            MP.RegisterSyncMethod(typeof(CompositableLoadouts), nameof(SyncedSetLoadoutStateName));
            MP.RegisterSyncMethod(typeof(CompositableLoadouts), nameof(SyncedCreateNewLoadoutState));
            MP.RegisterSyncWorker<LoadoutState>(SyncLoadoutState);

            MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(Dialog_LoadoutStateEditor), nameof(Dialog_LoadoutStateEditor.DoWindowContents)),
                prefix: new HarmonyMethod(typeof(CompositableLoadouts), nameof(PreLoadoutStateEditorDraw)),
                postfix: new HarmonyMethod(typeof(CompositableLoadouts), nameof(PostLoadoutStateEditorDraw)));

            // Tag
            MP.RegisterSyncMethod(typeof(Tag), nameof(Tag.Add));
            MP.RegisterSyncWorker<Tag>(SyncTag);
            MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(Tag), nameof(Tag.Add)),
                prefix: new HarmonyMethod(typeof(CompositableLoadouts), nameof(StopDuplicateDefAdditionToTag)));

            MP.RegisterSyncWorker<Item>(SyncItem, shouldConstruct: true);
            MP.RegisterSyncWorker<SafeDef>(SyncSafeDef, shouldConstruct: true);
            MP.RegisterSyncWorker<Filter>(SyncFilter, shouldConstruct: true);

            MP.RegisterSyncWorker<HashSet<Item>>(SyncHashSet);
            MP.RegisterSyncWorker<HashSet<SafeDef>>(SyncHashSet);

            // Tag editor
            MP.RegisterSyncMethod(typeof(CompositableLoadouts), nameof(SyncSetTagRequiredItemsList));
            MP.RegisterSyncMethod(typeof(CompositableLoadouts), nameof(SyncSetTagName));
            var removeLocalTagFromDialog = new HarmonyMethod(typeof(CompositableLoadouts), nameof(RemoveLocalTagFromDialog));
            MpCompat.harmony.Patch(AccessTools.DeclaredConstructor(typeof(Dialog_TagEditor), new[] { typeof(Tag) }),
                postfix: removeLocalTagFromDialog);
            MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(Dialog_TagEditor), nameof(Dialog_TagEditor.DoWindowContents)),
                postfix: removeLocalTagFromDialog);
            MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(Dialog_TagEditor), nameof(Dialog_TagEditor.Draw)),
                prefix: new HarmonyMethod(typeof(CompositableLoadouts), nameof(PreTagEditorDraw)),
                postfix: new HarmonyMethod(typeof(CompositableLoadouts), nameof(PostTagEditorDraw)));

            // Tag selector
            MP.RegisterSyncMethod(typeof(CompositableLoadouts), nameof(SyncedCloseAndSelect));
            MP.RegisterSyncWorker<Action<Tag>>(SyncActionTag);
            MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(Dialog_TagSelector), nameof(Dialog_TagSelector.OnAcceptKeyPressed)),
                prefix: new HarmonyMethod(typeof(CompositableLoadouts), nameof(PreTagSelectorAcceptKeyPressed)));
            MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(Dialog_TagSelector), nameof(Dialog_TagSelector.DrawCreateNewTag)),
                transpiler: new HarmonyMethod(typeof(CompositableLoadouts), nameof(ReplaceTagSelectorButtons)));

            // CopiedTags
            MP.RegisterSyncMethod(typeof(CopiedTags), nameof(CopiedTags.CopyTo));
            MP.RegisterSyncWorker<CopiedTags>(SyncCopiedTags);

            // LoadoutComponent
            MP.RegisterSyncMethod(typeof(LoadoutComponent), nameof(LoadoutComponent.AddTag));
            MP.RegisterSyncMethod(typeof(LoadoutComponent), nameof(LoadoutComponent.RemoveTag));
            MpCompat.RegisterLambdaMethod(typeof(LoadoutComponent), nameof(LoadoutComponent.CompGetGizmosExtra), 0);
            MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(LoadoutComponent), nameof(LoadoutComponent.AddTag)),
                prefix: new HarmonyMethod(typeof(CompositableLoadouts), nameof(StopDuplicateTagAdditionToComp)));
            MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(LoadoutComponent), nameof(LoadoutComponent.RemoveTag)),
                prefix: new HarmonyMethod(typeof(CompositableLoadouts), nameof(StopDuplicateTagRemovalToComp)));

            // Loadout Element
            MP.RegisterSyncMethod(typeof(LoadoutElement), nameof(LoadoutElement.SetTo));
            MP.RegisterSyncWorker<LoadoutElement>(SyncLoadoutElement);

            // Loadout
            MP.RegisterSyncWorker<Loadout>(SyncLoadout);

            // Loadout Editor
            MP.RegisterSyncMethod(typeof(CompositableLoadouts), nameof(SyncLoadoutElementsOrderChange));

            // Float menus
            // Set to repeat per tag mode
            MpCompat.RegisterLambdaDelegate(typeof(MakeConfigFloatMenu_Patch), nameof(MakeConfigFloatMenu_Patch.GetOptions), 0);

            // Loading saved tags into current game
            var method = MpMethodUtil.GetLambda(typeof(Panel_InterGameSettingsPanel), nameof(Panel_InterGameSettingsPanel.DrawLoadButton), MethodType.Normal, null, 1);
            MP.RegisterSyncDelegate(typeof(Panel_InterGameSettingsPanel), method.DeclaringType!.Name, method.Name, new[] { "tag", "<>4__this", });
            MP.RegisterSyncWorker<Panel_InterGameSettingsPanel>(SyncInterGameSettingsPanel);
            MpCompat.harmony.Patch(method, prefix: new HarmonyMethod(typeof(CompositableLoadouts), nameof(PreLoadSavedTag)));
        }

        private static void LatePatch()
        {
            // Same as above
            MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(Dialog_LoadoutEditor), nameof(Dialog_LoadoutEditor.DrawTags)),
                prefix: new HarmonyMethod(typeof(CompositableLoadouts), nameof(PreLoadoutEditorDraggableTags)),
                postfix: new HarmonyMethod(typeof(CompositableLoadouts), nameof(PostLoadoutEditorDraggableTags)));
            // Same as above
            MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(Dialog_TagEditor), nameof(Dialog_TagEditor.DrawTagEditor)),
                transpiler: new HarmonyMethod(typeof(CompositableLoadouts), nameof(ReplaceButtonsWithTimedButtons)));

            // Utility
            // Works on defs, will need to do late as they're not loaded otherwise
            MP.RegisterSyncMethod(typeof(Utility), nameof(Utility.SetActiveState));

            // Uses DefOfs in static constructor, will be null if we accessed the class earlier
            // Item specifier, used from tag editor
            MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(Dialog_ItemSpecifier), nameof(Dialog_ItemSpecifier.DoWindowContents)),
                postfix: new HarmonyMethod(typeof(CompositableLoadouts), nameof(CheckCurrentTagForChanges)));
        }

        #endregion

        #region Loadout State Editor Patches

        private static void PreLoadoutStateEditorDraw(List<Inventory.Pair<LoadoutState, bool>> ___states, ref (int id, string currentName) __state)
        {
            if (!MP.IsInMultiplayer)
                return;

            var state = ___states.Find(pair => pair.second)?.first;
            if (state != null)
                __state = (state.id, state.name);
        }

        private static void PostLoadoutStateEditorDraw(List<Inventory.Pair<LoadoutState, bool>> ___states, (int id, string currentName) __state)
        {
            if (!MP.IsInMultiplayer)
                return;

            var indexToHandle = -1;
            LoadoutState stateToHandle = null;

            for (var index = 0; index < ___states.Count; index++)
            {
                var (state, active) = ___states[index];

                if (state.id < 0)
                {
                    indexToHandle = index;
                    stateToHandle = state;
                }
                else if (state.id == __state.id && __state.currentName != null && active)
                {
                    if (state.name != __state.currentName)
                    {
                        SyncedSetLoadoutStateName(state.id, state.name);
                        state.name = __state.currentName;
                    }
                }
            }

            if (indexToHandle >= 0 && stateToHandle != null)
            {
                LoadoutManager.States.Remove(stateToHandle);
                ___states.RemoveAt(indexToHandle);
                SyncedCreateNewLoadoutState(stateToHandle.name);
            }
        }

        private static void SyncedSetLoadoutStateName(int id, string name)
        {
            var state = LoadoutManager.States.Find(state => state.id == id);
            if (state != null)
                state.name = name;
        }

        private static void SyncedCreateNewLoadoutState(string name)
        {
            var state = new LoadoutState(name);
            LoadoutManager.States.Add(state);

            if (!MP.IsExecutingSyncCommandIssuedBySelf)
                return;

            var dialog = Find.WindowStack.WindowOfType<Dialog_LoadoutStateEditor>();
            if (dialog?.states == null)
                return;

            dialog.states.Add(new Inventory.Pair<LoadoutState, bool>(state, false));
            // The mod uses states = states.OrderByDescending(x => x.first.name).ToList() for sorting
            dialog.states.SortByDescending(pair => pair.first.name);
        }

        private static void RemoveEmptyStateFromStateEditor(object instance, object[] args)
        {
            if (args.Length == 0 || args[0] is not LoadoutState state)
                return;

            var dialog = Find.WindowStack.WindowOfType<Dialog_LoadoutStateEditor>();
            dialog?.states.RemoveAll(pair => pair.first.id == state.id);
        }

        #endregion

        #region Loadout Editor Patches

        private static void PreLoadoutEditorDraggableTags(Dialog_LoadoutEditor __instance, ref int __state)
        {
            if (MP.IsInMultiplayer) 
                __state = __instance.curTagIdx;
        }

        private static void PostLoadoutEditorDraggableTags(Dialog_LoadoutEditor __instance, int __state)
        {
            if (!MP.IsInMultiplayer || __state < 0 || __instance.curTagIdx < 0 || __state == __instance.curTagIdx)
                return;

            // Order changed, sync it
            var elements = __instance.component.Loadout.elements;
            // Swap the elements back to unsynced state
            (elements[__instance.curTagIdx], elements[__state]) = (elements[__state], elements[__instance.curTagIdx]);
            SyncLoadoutElementsOrderChange(__instance.component, __state, __instance.curTagIdx > __state);
        }

        private static void SyncLoadoutElementsOrderChange(LoadoutComponent comp, int originalIndex, bool increment)
        {
            var elements = comp.Loadout.elements;
            if (originalIndex < 0 || elements.Count < originalIndex)
                return;

            if (increment)
            {
                if (elements.Count < originalIndex + 1)
                    return;
            }
            else if (originalIndex == 0)
                return;

            var swapIndex = increment
                ? originalIndex + 1
                : originalIndex - 1;

            (elements[originalIndex], elements[swapIndex]) = (elements[swapIndex], elements[originalIndex]);
        }

        #endregion

        #region Tag Editor and Item Specifier Patches

        private static void RemoveLocalTagFromDialog(ref Tag ___curTag)
        {
            // If we have negative (unitialized/local) id, remove selection.
            // After calling LoadoutManager.AddTag, the sync worker for Tag will set this value to what it would be.
            if (MP.IsInMultiplayer && ___curTag is { uniqueId: < 0 })
                ___curTag = null;
        }

        private static void PreTagEditorDraw(Tag ___curTag, ref string __state)
        {
            stateCopies.Clear();

            if (!MP.IsInMultiplayer || ___curTag == null || ___curTag.uniqueId < 0)
            {
                currentTag = null;
                return;
            }

            if (___curTag.uniqueId >= 0)
                __state = ___curTag.name;
            currentTag = ___curTag;
            stateCopies.AddRange(___curTag.requiredItems.Select(tag => new ItemStateCopy(tag)));
        }

        private static void PostTagEditorDraw(Tag ___curTag, string __state)
        {
            if (!MP.IsInMultiplayer)
                return;

            if (__state != null && __state != ___curTag.name && ___curTag.uniqueId >= 0)
                SyncSetTagName(___curTag, ___curTag.name);
            CheckCurrentTagForChanges();
        }

        private static void CheckCurrentTagForChanges()
        {
            // No need to check for MP, as currentTag will always be null in SP
            if (currentTag == null || currentTag.uniqueId < 0)
                return;

            if (currentTag.requiredItems.Count == stateCopies.Count &&
                currentTag.requiredItems.All(item =>
                {
                    var other = stateCopies.FirstOrDefault(state => state.itemReference == item);
                    return other != null && ItemsEqual(item, other);
                }))
            {
                return;
            }

            SyncSetTagRequiredItemsList(currentTag, currentTag.requiredItems);
            currentTag.requiredItems = new List<Item>(stateCopies.Select(stateCopy => stateCopy.ResetItemState()));
        }

        private static void SyncSetTagRequiredItemsList(Tag tag, List<Item> newList)
        {
            var dialog = Find.WindowStack.WindowOfType<Dialog_ItemSpecifier>();
            if (currentTag == tag && dialog?.filter != null)
            {
                dialog.filter = newList.FirstOrDefault(filter => filter.def.Def == dialog.filter.Thing)?.filter;
                // Invalid filter/something broke, close dialog
                if (dialog.filter == null)
                    dialog.Close();
                else
                {
                    stateCopies.Clear();
                    stateCopies.AddRange(newList.Select(item => new ItemStateCopy(item)));
                }
            }

            tag.requiredItems = newList;
        }

        private static void SyncSetTagName(Tag tag, string name) => tag.name = name;

        #endregion

        #region Tag Selector Patches

        private static bool PreTagSelectorAcceptKeyPressed(Dialog_TagSelector __instance)
        {
            if (!MP.IsInMultiplayer)
                return true;

            if (__instance.selectedTags.Any())
                SyncedCloseAndSelect(null, __instance.selectedTags, false, __instance.onSelect);
            else
                SyncedCloseAndSelect(new Tag(__instance.searchString), __instance.selectedTags, true, __instance.onSelect);

            return false;
        }

        private static bool ReplacedTagSelectorButton(Rect rect, string label, bool drawBackground, bool doMouseoverSound, bool active, TextAnchor? overrideTextAnchor, Dialog_TagSelector instance)
        {
            if (!MP.IsInMultiplayer)
                return Widgets.ButtonText(rect, label, drawBackground, doMouseoverSound, active, overrideTextAnchor);

            var callAllowed = Time.realtimeSinceStartup > nextCallAllowedTime;
            var result = Widgets.ButtonText(rect, label, drawBackground, doMouseoverSound, active && callAllowed, overrideTextAnchor);

            if (!result || !callAllowed)
                return false;

            nextCallAllowedTime = Time.realtimeSinceStartup + 1;
            if (label == Strings.SelectTags)
                SyncedCloseAndSelect(null, instance.selectedTags, false, instance.onSelect);
            else if (label == Strings.CreateNewTag)
                SyncedCloseAndSelect(new Tag(instance.selectedTags.Any() ? string.Empty : instance.searchString), instance.selectedTags, true, instance.onSelect);
            // Unsupported button, just return true (since we handled false before)
            else return true;

            return false;
        }

        private static void SyncedCloseAndSelect(Tag tag, List<Tag> selectedTags, bool openTagEditorForSelf, Action<Tag> onSelect)
        {
            foreach (var t in selectedTags)
                onSelect(t);

            if (tag != null)
                onSelect(tag);

            if (!MP.IsExecutingSyncCommandIssuedBySelf)
                return;

            Find.WindowStack.RemoveWindowsOfType(typeof(Dialog_TagSelector));
            if (openTagEditorForSelf)
                Find.WindowStack.Add(new Dialog_TagEditor(tag));
        }

        private static IEnumerable<CodeInstruction> ReplaceTagSelectorButtons(IEnumerable<CodeInstruction> instr, MethodBase baseMethod)
        {
            var targetMethod = AccessTools.DeclaredMethod(typeof(Widgets), nameof(Widgets.ButtonText),
                new[] { typeof(Rect), typeof(string), typeof(bool), typeof(bool), typeof(bool), typeof(TextAnchor?) });
            var replacementMethod = AccessTools.DeclaredMethod(typeof(CompositableLoadouts), nameof(ReplacedTagSelectorButton));
            var replacements = 0;

            foreach (var ci in instr)
            {
                if (ci.opcode == OpCodes.Call && ci.operand is MethodInfo method && method == targetMethod)
                {
                    ci.operand = replacementMethod;
                    // Load self (instance) as a final argument
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    replacements++;
                }

                yield return ci;
            }

            const int expected = 3;
            if (replacements != expected)
            {
                var name = (baseMethod.DeclaringType?.Namespace).NullOrEmpty() ? baseMethod.Name : $"{baseMethod.DeclaringType!.Name}:{baseMethod.Name}";
                Log.Warning($"Patching {name} - patched {replacements} methods, expected: {expected}");
            }
        }

        #endregion

        #region Duplicate Operation Prevention Patches

        // Due to possibility of the sync method being called multiple times before actually being executed, this will prevent
        // the methods from executing multiple times and performing unwanted operations.
        private static bool StopDuplicateTagAdditionToComp(LoadoutComponent __instance, Tag tag) 
            => !MP.IsInMultiplayer || !__instance.Loadout.elements.Any(element => element.tag.uniqueId == tag.uniqueId);

        private static bool StopDuplicateTagRemovalToComp(LoadoutComponent __instance, LoadoutElement element)
            => !MP.IsInMultiplayer || __instance.Loadout.elements.Contains(element);

        private static bool StopDuplicateDefAdditionToTag(Tag __instance, ThingDef thing)
            => !MP.IsInMultiplayer || __instance.requiredItems.All(item => item.Def != thing);

        private static bool StopDuplicateTagAdditionToManager(Tag tag)
            => !MP.IsInMultiplayer || !LoadoutManager.instance.tags.Any(t => t.uniqueId == tag.uniqueId);

        // Calling methods which sync local tag can create multiple tags if clicked repeatedly (possibly by accident).
        // Issue with this is that we cannot easily check if the tag that was just created already exists, as there's nothing
        // that'll be unique for them (besides the ID, which we get during syncing local ones).
        // This replacement for buttons is here to prevent spam-clicking buttons, which would result in creation of high amount of duplicate tags.
        private static bool PreventDuplicateButtonClicks(Rect rect, string label, bool drawBackground, bool doMouseoverSound, bool active, TextAnchor? overrideTextAnchor)
        {
            if (!MP.IsInMultiplayer || label != Strings.CreateNewTag)
                return Widgets.ButtonText(rect, label, drawBackground, doMouseoverSound, active, overrideTextAnchor);

            var callAllowed = Time.realtimeSinceStartup > nextCallAllowedTime;
            var result = Widgets.ButtonText(rect, label, drawBackground, doMouseoverSound, active && callAllowed, overrideTextAnchor);

            if (!result || !callAllowed)
                return false;

            nextCallAllowedTime = Time.realtimeSinceStartup + 1;
            return true;
        }

        private static IEnumerable<CodeInstruction> ReplaceButtonsWithTimedButtons(IEnumerable<CodeInstruction> instr)
        {
            var targetMethod = AccessTools.DeclaredMethod(typeof(Widgets), nameof(Widgets.ButtonText), 
                new[] { typeof(Rect), typeof(string), typeof(bool), typeof(bool), typeof(bool), typeof(TextAnchor?) });
            var replacementMethod = AccessTools.DeclaredMethod(typeof(CompositableLoadouts), nameof(PreventDuplicateButtonClicks));

            foreach (var ci in instr)
            {
                if (ci.opcode == OpCodes.Call && ci.operand is MethodInfo method && method == targetMethod)
                    ci.operand = replacementMethod;
                
                yield return ci;
            }
        }

        #endregion

        #region Sync Workers

        private static void SyncLoadoutState(SyncWorker sync, ref LoadoutState state)
        {
            if (sync.isWriting)
            {
                var id = state?.id ?? -1;
                sync.Write(id);
                if (id < -1)
                    sync.Write(state!.name);
            }
            else
            {
                var id = sync.Read<int>();
                var states = LoadoutManager.States;

                if (id >= 0)
                {
                    state = states.Find(x => x.id == id);
                    return;
                }

                if (id == -1)
                    return;

                // With current patches we should never enter this part, but let's put here for safety/future-proofing
                state = new LoadoutState(sync.Read<string>());
                states.Add(state);

                if (!MP.IsExecutingSyncCommandIssuedBySelf)
                    return;

                var index = states.FindIndex(state => state.id == id);
                if (index >= 0)
                    states.RemoveAt(index);
            }
        }

        private static void SyncTag(SyncWorker sync, ref Tag tag)
        {
            if (sync.isWriting)
            {
                var id = tag?.uniqueId ?? -1;
                sync.Write(id);
                if (id >= -1)
                    return;

                sync.Write(tag!.name);
                sync.Write(tag!.idType);
                sync.Write(tag!.requiredItems);
            }
            else
            {
                var id = sync.Read<int>();
                var tags = LoadoutManager.Tags;

                if (id >= 0)
                {
                    tag = tags.Find(t => t.uniqueId == id);
                    return;
                }

                if (id == -1)
                    return;

                // As opposed to LoadoutState sync worker, this one can enter here quite often
                tag = new Tag(sync.Read<string>())
                {
                    idType = sync.Read<string>(),
                    requiredItems = sync.Read<List<Item>>(),
                };
                tags.Add(tag);
                LoadoutManager.PawnsWithTags.Add(tag, new SerializablePawnList(new List<Pawn>()));

                if (!MP.IsExecutingSyncCommandIssuedBySelf)
                    return;

                var index = tags.FindIndex(t => t.uniqueId == id);
                if (index >= 0)
                    tags.RemoveAt(index);

                var editorDialog = Find.WindowStack.WindowOfType<Dialog_TagEditor>();
                if (editorDialog != null && (editorDialog.curTag == null || editorDialog.curTag.uniqueId < 0))
                    editorDialog.curTag = tag;
            }
        }

        private static void SyncItem(SyncWorker sync, ref Item item)
        {
            sync.Bind(ref item.def);
            sync.Bind(ref item.filter);
            sync.Bind(ref item.quantity);
        }

        private static void SyncSafeDef(SyncWorker sync, ref SafeDef safeDef)
            => sync.Bind(ref safeDef.defName);

        private static void SyncFilter(SyncWorker sync, ref Filter filter)
        {
            sync.Bind(ref filter.forThing);
            sync.Bind(ref filter.stuffs);
            sync.Bind(ref filter.allowedHpRange);
            sync.Bind(ref filter.allowedQualities);
        }

        private static void SyncCopiedTags(SyncWorker sync, ref CopiedTags copiedTags)
        {
            if (sync.isWriting)
            {
                sync.Write(copiedTags.fromPawn);
                sync.Write(copiedTags.tagsToCopy);
                sync.Write(copiedTags.replace);
            }
            else
            {
                var pawn = sync.Read<Pawn>();
                var tags = sync.Read<List<Tag>>();
                var replace = sync.Read<bool>();

                copiedTags = new CopiedTags(pawn, tags, replace);
            }
        }

        private static void SyncActionTag(SyncWorker sync, ref Action<Tag> action)
        {
            // Somewhat based on WriteDelegate and ReadDelegate from Multiplayer.Client.Persistent
            // Although we let MP actually handle it in case of compiler-generated classes

            if (sync.isWriting)
            {
                // If it's a static method or the target is compiler generated - let MP handle it
                if (action?.Target == null || IsCompilerGenerated(action.Target.GetType()))
                {
                    sync.Write(true);
                    writeDelegateMethod(null, syncWorkerWriterField(sync), action);
                    return;
                }

                // If the delegate is not null and it's non-static method that's not compiler generated - we handle it ourselves, as MP syncs all fields separately.
                // Plus, I don't think it can handle targets that have constructors with arguments.
                sync.Write(false);

                sync.Write(action.GetType());
                sync.Write(action.Method.DeclaringType);
                sync.Write(action.Method.Name); // Doesn't support signature with ambiguous matches

                // Use a sync worker for instance
                var targetType = action.Target.GetType();
                sync.Write(targetType);
                sync.Write(action.Target, action.Target.GetType());
            }
            else
            {
                if (sync.Read<bool>())
                {
                    action = (Action<Tag>)readDelegateMethod(syncWorkerReaderField(sync));
                    return;
                }

                var delegateType = sync.Read<Type>();
                var type = sync.Read<Type>();
                var methodName = sync.Read<string>();

                var targetType = sync.Read<Type>();
                var target = sync.Read<object>(targetType);

                action = (Action<Tag>)Delegate.CreateDelegate(
                    delegateType,
                    target,
                    (MethodInfo)checkMethodAllowedMethod(null, AccessTools.DeclaredMethod(type, methodName)));
            }
        }

        private static bool IsCompilerGenerated(Type type)
        {
            while (type != null)
            {
                if (type.HasAttribute<CompilerGeneratedAttribute>()) return true;
                type = type.DeclaringType;
            }

            return false;
        }

        private static void SyncInterGameSettingsPanel(SyncWorker sync, ref Panel_InterGameSettingsPanel panel)
        {
            if (sync.isWriting)
                sync.Write(panel.currentTagNextName);
            else
            {
                panel = new Panel_InterGameSettingsPanel
                {
                    currentTagNextName = sync.Read<string>(),
                };
            }
        }

        private static void SyncHashSet<T>(SyncWorker sync, ref HashSet<T> set)
        {
            if (sync.isWriting)
                sync.Write(set.ToArray());
            else
                set = new HashSet<T>(sync.Read<T[]>());
        }

        private static void SyncLoadoutElement(SyncWorker sync, ref LoadoutElement element)
        {
            if (sync.isWriting)
            {
                var comp = GetLoadoutComp(element);
                sync.Write(comp != null);
                if (comp == null)
                    return;

                sync.Write(comp);
                sync.Write(element.Tag);
            }
            else
            {
                if (!sync.Read<bool>())
                    return;

                var comp = sync.Read<LoadoutComponent>();
                var tag = sync.Read<Tag>();

                element = comp.Loadout.AllElements.FirstOrDefault(element => element.Tag.uniqueId == tag.uniqueId);
            }
        }

        private static void SyncLoadout(SyncWorker sync, ref Loadout loadout)
        {
            if (sync.isWriting)
            {
                var comp = GetLoadoutComp(loadout.elements.FirstOrDefault());
                sync.Write(comp != null);
                if (comp != null)
                    sync.Write(comp);
            }
            else
            {
                if (sync.Read<bool>())
                    loadout = sync.Read<LoadoutComponent>()?.Loadout;
            }
        }

        private static LoadoutComponent GetLoadoutComp(LoadoutElement element)
        {
            if (element == null || !LoadoutManager.PawnsWithTags.TryGetValue(element.Tag, out var pawnList))
                return null;

            return pawnList.Pawns
                .Select(pawn => pawn.GetComp<LoadoutComponent>())
                .Where(comp => comp != null)
                .FirstOrDefault(comp => comp.Loadout.AllElements.Contains(element));
        }

        #endregion

        #region Other

        private static void PreLoadSavedTag(ref List<Tag> ___loadedTags, ref Tag ___tag)
        {
            if (MP.IsExecutingSyncCommand)
                ___loadedTags = LoadoutManager.Tags;
            else
            {
                // Prefixed are executed before the transpiled method, so it'll run before MP can do its stuff
                ___tag = ___tag.MakeCopy();
                // Let MP generate a new ID for it, since this one is loaded from settings will have positive id
                // but the ones generates in an unsynced context should generate with negative ids
                ___tag.uniqueId = LoadoutManager.GetNextTagId();
            }
        }

        private static bool ItemsEqual(Item first, ItemStateCopy second)
            => first.def.Def == second.def.Def &&
               first.quantity == second.quantity &&
               first.filter.allowedQualities == second.allowedQualities &&
               first.filter.allowedHpRange == second.allowedHpRange &&
               first.filter.stuffs.Select(def => def.Def).ToList().SetsEqual(second.stuffs.Select(def => def.Def).ToList());

        [MpCompatRequireMod("Wiri.compositableloadouts")]
        private class ItemStateCopy
        {
            internal readonly Item itemReference;
            internal readonly SafeDef def;
            internal readonly int quantity;
            internal readonly HashSet<SafeDef> stuffs;
            internal readonly FloatRange allowedHpRange;
            internal readonly QualityRange allowedQualities;

            public ItemStateCopy(Item item)
            {
                itemReference = item;
                def = item.def;
                quantity = item.quantity;
                stuffs = new HashSet<SafeDef>(item.filter.stuffs);
                allowedHpRange = item.filter.allowedHpRange;
                allowedQualities = item.filter.allowedQualities;
            }

            public Item ResetItemState()
            {
                itemReference.quantity = quantity;
                itemReference.filter = GetDefaultFilter();
                return itemReference;
            }

            private Filter GetDefaultFilter()
                => new Filter
                {
                    forThing = def,
                    stuffs = new HashSet<SafeDef>(stuffs),
                    allowedHpRange = allowedHpRange,
                    allowedQualities = allowedQualities,
                };
        }

        #endregion
    }
}