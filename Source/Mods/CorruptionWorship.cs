using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Multiplayer.API;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;

namespace Multiplayer.Compat
{
    [MpCompatFor("CptVHOhu970.CorruptionWorship")]
    public class CorruptionWorship
    {
        // DefDatabase<WonderDef>
        private static MethodInfo getDefByShortHash;

        // BuildingAltar
        public static ISyncField altarNameSyncField; // Used by Dialog_RenameTemple
        public static FieldInfo altarSermonTemplatesField; // Used for finding/syncing sermon using a BuildingAltar
        public static MethodInfo altarEndSermonMethod;
        public static MethodInfo altarTryStartSermonMethod;

        // SermonTemplate
        private static FieldInfo sermonTemplateNameField;
        private static FieldInfo sermonTemplatePreferredStartTimeField;
        private static FieldInfo sermonTemplateSermonDurationHoursField;
        private static FieldInfo sermonTemplateActiveField;

        // Dialog_AssignPreacher
        private static Type assignPreacherType;
        private static FieldInfo assignPreacherSermonField;
        private static FieldInfo assignPreacherAltarField;

        // Dialog_AssignAssistant
        private static Type assignAssistantType;

        // Inner class inside of TempleCardUtility
        private static FieldInfo templeCardUtilityInnerAltarField;
        private static FieldInfo templeCardUtilityInnerSermonField;

        // MainTabWindow_Worship
        // WonderWorker
        private static FieldInfo wonderWorkerDefField;

        // WonderWorker_Targetable
        private static FieldInfo wonderWorkerTargetableTargetField;
        private static FieldInfo wonderWorkerTargetableCanceledField;

        // WonderWorker_Targetable inner class
        private static FieldInfo winderWorkerTargetableInnerBaseField;
        private static bool shouldCallCheckCancelled = true;

        // WonderDef
        private static FieldInfo wonderDefWorkerIntField; // Not actually an int, it's just named like that

        // Dialog_ReligiousRiot
        private static Type religiousRiotType;

        // Things we don't sync as they're unfinished, and seem to not be implemented yet:
        // Dialog_StartRitual, which is created from BuildingSacrificialAltar

        public CorruptionWorship(ModContentPack mod)
        {
            // BuildingAltar
            {
                var type = AccessTools.TypeByName("Corruption.Worship.BuildingAltar");
                altarNameSyncField = MP.RegisterSyncField(type, "RoomName");
                altarSermonTemplatesField = AccessTools.Field(type, "Templates");
                altarEndSermonMethod = AccessTools.Method(type, "EndSermon");
                altarTryStartSermonMethod = AccessTools.Method(type, "TryStartSermon");

                // The class representing a sermon, with all the required data
                type = AccessTools.TypeByName("Corruption.Worship.SermonTemplate");
                sermonTemplateNameField = AccessTools.Field(type, "Name");
                sermonTemplatePreferredStartTimeField = AccessTools.Field(type, "preferredStartTime");
                sermonTemplateSermonDurationHoursField = AccessTools.Field(type, "SermonDurationHours");
                sermonTemplateActiveField = AccessTools.Field(type, "Active");

                // Dialog for renaming the altar, created from TempleCardUtility
                type = AccessTools.TypeByName("Corruption.Worship.Dialog_RenameTemple");
                MP.RegisterSyncMethod(typeof(CorruptionWorship), nameof(SyncSermonName));
                MpCompat.harmony.Patch(AccessTools.Method(type, nameof(Window.DoWindowContents)),
                    prefix: new HarmonyMethod(typeof(CorruptionWorship), nameof(RenameTemplePrefix)),
                    postfix: new HarmonyMethod(typeof(CorruptionWorship), nameof(RenameTemplePostfix)));

                // Dialog for renaming a specific sermon, created from TempleCardUtility
                type = AccessTools.TypeByName("Corruption.Worship.Dialog_RenameSermon");
                MpCompat.harmony.Patch(AccessTools.Method(type, "DoWindowContents"),
                    prefix: new HarmonyMethod(typeof(CorruptionWorship), nameof(RenameSermonPrefix)),
                    postfix: new HarmonyMethod(typeof(CorruptionWorship), nameof(RenameSermonPostfix)));

                // Dialog used for assigning a preacher, created from TempleCardUtility
                assignPreacherType = AccessTools.TypeByName("Corruption.Worship.Dialog_AssignPreacher");
                assignPreacherAltarField = AccessTools.Field(assignPreacherType, "altar");
                assignPreacherSermonField = AccessTools.Field(assignPreacherType, "sermon");
                MP.RegisterSyncMethod(assignPreacherType, "AssignPawn");
                MP.RegisterSyncMethod(assignPreacherType, "UnassignPawn");
                MP.RegisterSyncWorker<Window>(SyncDialogAssignPreacher, assignPreacherType);

                // Dialog used for assigning a preacher, you guessed it, created from TempleCardUtility
                // This is a subclass of Dialog_AssignPreacher
                assignAssistantType = AccessTools.TypeByName("Corruption.Worship.Dialog_AssignAssistant");
                MP.RegisterSyncMethod(assignAssistantType, "AssignPawn");
                MP.RegisterSyncMethod(assignAssistantType, "UnassignPawn");
                MP.RegisterSyncWorker<Window>(SyncDialogAssignPreacher, assignAssistantType);
            }

            // MainTabWindow_Worship
            {
                // WonderWorker, does the work to execute the wonder defined in the MainTabWindow_Worship
                var wonderWorkerType = AccessTools.TypeByName("Corruption.Worship.Wonders.WonderWorker");
                var wonderWorkerTargetableType = AccessTools.TypeByName("Corruption.Worship.Wonders.WonderWorker_Targetable");
                wonderWorkerDefField = AccessTools.Field(wonderWorkerType, "Def");
                wonderWorkerTargetableTargetField = AccessTools.Field(wonderWorkerTargetableType, "target");
                wonderWorkerTargetableCanceledField = AccessTools.Field(wonderWorkerTargetableType, "cancelled");
                MP.RegisterSyncWorker<object>(SyncWonderWorker, wonderWorkerType, isImplicit: true);
                MP.RegisterSyncWorker<object>(SyncWonderTargetableWorker, wonderWorkerTargetableType, isImplicit: true);
                MP.RegisterSyncMethod(wonderWorkerTargetableType, "StartTargeting");
                MP.RegisterSyncMethod(wonderWorkerTargetableType, "CheckCancelled");

                // Sync the method for each class inheriting from TryExecuteWonder
                // If we ever want to sync the base class too, include .Concat(type) there
                // But since all it does is returning false, we'll skip it
                // We also check if it's not a subtype of WonderWorker_Targetable, as it uses TryDoEffectOnTarget method for the reward
                // (and then we pray that there's not another class doing it in a special way)
                foreach (var subtype in wonderWorkerType.AllSubclasses().Where(x => !x.IsAssignableFrom(wonderWorkerTargetableType)))
                {
                    // Include types for maximum safety
                    var method = AccessTools.Method(subtype, "TryExecuteWonder", new[] { typeof(Def), typeof(int) });
                    if (method != null)
                        MP.RegisterSyncMethod(method);
                }

                var inner = AccessTools.Inner(wonderWorkerTargetableType, "<>c__DisplayClass6_0");
                winderWorkerTargetableInnerBaseField = AccessTools.Field(inner, "<>4__this");
                MpCompat.harmony.Patch(MpCompat.MethodByIndex(inner, "<TryExecuteWonderInt>", 0),
                    prefix: new HarmonyMethod(typeof(CorruptionWorship), nameof(PreStartTargetting)));
                MpCompat.harmony.Patch(MpCompat.MethodByIndex(inner, "<TryExecuteWonderInt>", 1),
                    prefix: new HarmonyMethod(typeof(CorruptionWorship), nameof(PreCheckCancelled)));

                // WonderDef, all we want is the field storing the WonderWorker for sync worker
                var type = AccessTools.TypeByName("Corruption.Worship.Wonders.WonderDef");
                wonderDefWorkerIntField = AccessTools.Field(type, "workerInt");

                var database = typeof(DefDatabase<>).MakeGenericType(new Type[] { type });
                getDefByShortHash = AccessTools.Method(database, "GetByShortHash");

                // GlobalWorshipTracker, we sync favor usage when the user decides to purchase a wonder
                type = AccessTools.TypeByName("Corruption.Worship.GlobalWorshipTracker");
                MP.RegisterSyncMethod(type, "ConsumeFavourFor");
            }

            LongEventHandler.ExecuteWhenFinished(LatePatch);
        }

        private static void LatePatch()
        {
            // BuildingAltar continuation
            {
                // TempleCardUtility is used for UI drawing, called from BuildingAltar
                var type = AccessTools.TypeByName("Corruption.Worship.TempleCardUtility");
                MP.RegisterSyncDelegate(type, "<>c__DisplayClass4_1", "<OpenDedicationSelectMenu>b__1");
                // We patch the modded method to intercept some of their calls that will need syncing and call them through our methods,
                // as syncing the actual methods themselves will sync way too much (methods might/will be called often)
                MpCompat.harmony.Patch(AccessTools.Method(type, "DrawSermonTemplate"),
                    prefix: new HarmonyMethod(typeof(CorruptionWorship), nameof(DrawSermonTemplatePrefix)),
                    postfix: new HarmonyMethod(typeof(CorruptionWorship), nameof(DrawSermonTemplatePostfix)),
                    transpiler: new HarmonyMethod(typeof(CorruptionWorship), nameof(PatchDrawSermonTemplate)));

                // The previous inner class (<>c__DisplayClass4_1) needs the following one to be synced too, but this one uses sermon
                // which requires a bit more data to sync, so we make SyncWorker manually instead of using RegisterSyncDelegate
                var inner = AccessTools.Inner(type, "<>c__DisplayClass4_0");
                templeCardUtilityInnerAltarField = AccessTools.Field(inner, "altar");
                templeCardUtilityInnerSermonField = AccessTools.Field(inner, "template");

                MP.RegisterSyncMethod(typeof(CorruptionWorship), nameof(SyncedInterceptedReceiveMemo));
                MP.RegisterSyncMethod(typeof(CorruptionWorship), nameof(SyncedInterceptedEndSermon));
                MP.RegisterSyncMethod(typeof(CorruptionWorship), nameof(SyncedTryStartSermon)).SetDebugOnly();
                MP.RegisterSyncMethod(typeof(CorruptionWorship), nameof(SyncSimpleSermonData));
            }

            // Dialog_ReligiousRiot, opened when there's enough pawns of different religion
            // Should open for all players, so we assume we can get it/sync it using Find.WindowStack
            {
                religiousRiotType = AccessTools.TypeByName("Corruption.Worship.Dialog_ReligiousRiot");
                MP.RegisterSyncMethod(religiousRiotType, "ChooseNewReligion");
                MP.RegisterSyncMethod(religiousRiotType, "PreserveReligion");
                MP.RegisterSyncWorker<Window>(SyncReligiousRiotDialog, religiousRiotType);
            }

            // Other stuff
            {
                // Rotating worship statue
                var type = AccessTools.TypeByName("Corruption.Worship.Building_WorshipStatue");
                MpCompat.RegisterSyncMethodByIndex(type, "<GetGizmos>", 0);

                // Debug ring the bell
                type = AccessTools.TypeByName("Corruption.Worship.CompBellTower");
                MpCompat.RegisterSyncMethodByIndex(type, "<CompGetGizmosExtra>", 0).SetDebugOnly();

                // Drop effigy
                type = AccessTools.TypeByName("Corruption.Worship.CompShrine");
                MP.RegisterSyncMethod(type, "DropEffigy");
            }
        }

        // Helper method for finding the sermon index inside of the (currently selected) altar
        private static bool TryGetDataForSermon(object sermon, out Building altar, out int index)
        {
            if (Find.Selector.SingleSelectedThing is Building a)
            {
                altar = a;
                return TryGetDataForSermon(sermon, altar, out index);
            }
            else
            {
                altar = null;
                index = -1;
                return false;
            }
        }

        // Helper method for finding the sermon index inside of the altar
        private static bool TryGetDataForSermon(object sermon, Building altar, out int index)
        {
            var list = (IList)altarSermonTemplatesField.GetValue(altar);
            index = list.IndexOf(sermon);
            return index >= 0;
        }

        // Helper method for getting the sermon out of an altar with specific index
        private static object GetSermon(Building altar, int index)
        {
            var list = (IList)altarSermonTemplatesField.GetValue(altar);
            return list[index];
        }

        // Used to watch for changes to the temple name
        private static void RenameTemplePrefix(Building ___Altar)
        {
            if (MP.enabled)
            {
                MP.WatchBegin();
                altarNameSyncField.Watch(___Altar);
            }
        }

        private static void RenameTemplePostfix()
        {
            if (MP.enabled) MP.WatchEnd();
        }

        // Check for changes inside using widgets like checkboxes and sliders
        // Everything else is changed using dialogs, windows, etc.
        private static void DrawSermonTemplatePrefix(object sermon, ref object[] __state)
        {
            if (!MP.IsInMultiplayer) return;

            __state = new object[]
            {
                sermonTemplatePreferredStartTimeField.GetValue(sermon),
                sermonTemplateSermonDurationHoursField.GetValue(sermon),
                sermonTemplateActiveField.GetValue(sermon),
            };
        }

        private static void DrawSermonTemplatePostfix(Building altar, object sermon, ref object[] __state)
        {
            if (!MP.IsInMultiplayer) return;

            if (TryGetDataForSermon(sermon, altar, out var index))
            {
                var startTime = (int)sermonTemplatePreferredStartTimeField.GetValue(sermon);
                var duration = (float)sermonTemplateSermonDurationHoursField.GetValue(sermon);
                var active = (bool)sermonTemplateActiveField.GetValue(sermon);

                if (startTime != (int)__state[0] || duration != (float)__state[1] || active != (bool)__state[2])
                {
                    sermonTemplatePreferredStartTimeField.SetValue(sermon, __state[0]);
                    sermonTemplateSermonDurationHoursField.SetValue(sermon, __state[1]);
                    sermonTemplateActiveField.SetValue(sermon, __state[2]);

                    SyncSimpleSermonData(altar, index, startTime, duration, active);
                }
            }
        }

        private static void SyncSimpleSermonData(Building altar, int index, int startTime, float duration, bool active)
        {
            var sermon = GetSermon(altar, index);

            sermonTemplatePreferredStartTimeField.SetValue(sermon, startTime);
            sermonTemplateSermonDurationHoursField.SetValue(sermon, duration);
            sermonTemplateActiveField.SetValue(sermon, active);
        }

        // Used to watch for changes to the name of a specific sermon
        private static void RenameSermonPrefix(object ___Sermon, ref string __state)
        {
            if (!MP.IsInMultiplayer) return;

            __state = (string)sermonTemplateNameField.GetValue(___Sermon);
        }

        private static void RenameSermonPostfix(object ___Sermon, string __state)
        {
            if (!MP.IsInMultiplayer) return;

            var name = (string)sermonTemplateNameField.GetValue(___Sermon);

            if (name != __state && TryGetDataForSermon(___Sermon, out var altar, out var index))
            {
                sermonTemplateNameField.SetValue(___Sermon, __state);
                SyncSermonName(altar, index, name);
            }
        }

        // Used to sync the sermon name to all clients
        private static void SyncSermonName(Building altar, int sermonIndex, string newSermonName) => sermonTemplateNameField.SetValue(GetSermon(altar, sermonIndex), newSermonName);

        // Sync worker for the Dialog_AssignPreacher (and Dialog_AssignAssistant, as it's implicit worker)
        private static void SyncDialogAssignPreacher(SyncWorker sync, ref Window obj)
        {
            if (sync.isWriting)
            {
                var sermon = assignPreacherSermonField.GetValue(obj);
                var altar = assignPreacherAltarField.GetValue(obj) as Building;

                if (TryGetDataForSermon(sermon, altar, out var index))
                {
                    sync.Write(index);
                    sync.Write(altar);
                    sync.Write(obj.GetType() == assignPreacherType);
                }
                else sync.Write(-1);
            }
            else
            {
                var index = sync.Read<int>();

                if (index >= 0)
                {
                    var altar = sync.Read<Building>();
                    var sermon = GetSermon(altar, index);
                    var type = sync.Read<bool>() ? assignPreacherType : assignAssistantType;

                    obj = Activator.CreateInstance(type, altar, sermon) as Window;
                }
            }
        }

        // Used for syncing inner, compiler-generated class inside of TempleCardUtility
        private static void SyncTemplaCardUtilityInnerClass(SyncWorker sync, object obj)
        {
            if (sync.isWriting)
            {
                var altar = templeCardUtilityInnerAltarField.GetValue(obj) as Building;
                var sermon = templeCardUtilityInnerSermonField.GetValue(obj);

                if (TryGetDataForSermon(sermon, altar, out var index))
                {
                    sync.Write(index);
                    sync.Write(altar);
                }
                else sync.Write(-1);
            }
            else
            {
                var index = sync.Read<int>();

                if (index >= 0)
                {
                    var altar = sync.Read<Building>();
                    templeCardUtilityInnerAltarField.SetValue(obj, altar);
                    templeCardUtilityInnerSermonField.SetValue(obj, GetSermon(altar, index));
                }
            }
        }

        // Used for syncing WonderWorker
        // Pretty easy, as WonderWorker has a field storing the def that uses it, and the def stores the worker
        private static void SyncWonderWorker(SyncWorker sync, ref object obj)
        {
            if (sync.isWriting)
                sync.Write(((Def)wonderWorkerDefField.GetValue(obj)).shortHash);
            else
                obj = wonderDefWorkerIntField.GetValue(getDefByShortHash.Invoke(null, new object[] { sync.Read<ushort>() }));
        }

        // Besides syncing a normal WonderWorker, we also need the info for target and if it was cancelled
        private static void SyncWonderTargetableWorker(SyncWorker sync, ref object obj)
        {
            SyncWonderWorker(sync, ref obj);

            if (sync.isWriting)
            {
                sync.Write((bool)wonderWorkerTargetableCanceledField.GetValue(obj));
                sync.Write((TargetInfo)wonderWorkerTargetableTargetField.GetValue(obj));
            }
            else
            {
                wonderWorkerTargetableCanceledField.SetValue(obj, sync.Read<bool>());
                wonderWorkerTargetableTargetField.SetValue(obj, sync.Read<TargetInfo>());
            }
        }

        // Sync the religious riot dialog
        // It should open up for all players, so we just get it from the WindowStack
        private static void SyncReligiousRiotDialog(SyncWorker sync, ref Window obj)
        {
            if (!sync.isWriting)
                obj = Find.WindowStack.Windows.First(x => x.GetType() == religiousRiotType);
        }

        // Patch out methods used by mod to our own, which will be synced
        private static IEnumerable<CodeInstruction> PatchDrawSermonTemplate(IEnumerable<CodeInstruction> instr)
        {
            var receiveMemo = AccessTools.Method(typeof(Lord), nameof(Lord.ReceiveMemo));

            foreach (var ci in instr)
            {
                if (ci.opcode == OpCodes.Callvirt)
                {
                    var operand = (MethodInfo)ci.operand;

                    if (operand == receiveMemo)
                    {
                        ci.opcode = OpCodes.Call;
                        ci.operand = AccessTools.Method(typeof(CorruptionWorship), nameof(SyncedInterceptedReceiveMemo));
                    }
                    else if (operand == altarEndSermonMethod)
                    {
                        ci.opcode = OpCodes.Call;
                        ci.operand = AccessTools.Method(typeof(CorruptionWorship), nameof(SyncedInterceptedEndSermon));
                    }
                    else if (operand == altarTryStartSermonMethod)
                    {
                        ci.opcode = OpCodes.Call;
                        ci.operand = AccessTools.Method(typeof(CorruptionWorship), nameof(InterceptedTryStartSermon));
                    }
                }

                yield return ci;
            }
        }

        private static void SyncedInterceptedReceiveMemo(Lord lord, string memo) => lord?.ReceiveMemo(memo);

        private static void SyncedInterceptedEndSermon(Building altar)
        {
            if (altar != null)
                altarEndSermonMethod.Invoke(altar, Array.Empty<object>());
        }

        // Don't sync this one as we can't easily sync the sermon
        // Instead try getting it and then sync the other data required to get it
        private static bool InterceptedTryStartSermon(Building altar, object sermon)
        {
            if (TryGetDataForSermon(sermon, altar, out var index))
                SyncedTryStartSermon(altar, index);

            // The place where we use this method doesn't care about the result
            // So, we just return true
            return true;
        }

        private static void SyncedTryStartSermon(Building altar, int index)
        {
            if (altarTryStartSermonMethod != null)
                altarTryStartSermonMethod.Invoke(altar, new object[] { GetSermon(altar, index) });
        }

        private static void PreStartTargetting(object __instance)
        {
            if (!MP.IsInMultiplayer) return;

            var targetableWorker = winderWorkerTargetableInnerBaseField.GetValue(__instance);
            wonderWorkerTargetableCanceledField.SetValue(targetableWorker, false);
            shouldCallCheckCancelled = false;
        }

        private static bool PreCheckCancelled()
        {
            var value = shouldCallCheckCancelled;
            shouldCallCheckCancelled = true;

            return !MP.IsInMultiplayer || value;
        }
    }
}
