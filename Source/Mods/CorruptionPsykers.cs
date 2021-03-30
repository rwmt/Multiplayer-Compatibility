using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    [MpCompatFor("CptOhu.CorruptionPsyker")]
    public class CorruptionPsykers
    {
        // DefDatabase<PsykerDisciplineDef>
        private static MethodInfo getDefByShortHash;

        // Window_Psyker
        private static Type psykerWindowType;
        private static FieldInfo psykerWindowCompField;

        // CompPsyker
        private static MethodInfo compPsykerTryLearnPowerMethod;
        private static MethodInfo compPsykerAddXpMethod;
        private static FieldInfo compPsykerMainDisciplineField;
        private static FieldInfo compPsykerMinorDisciplinesField;
        private static ISyncField compPsykerXpSyncField;

        // PsykerDisciplineDef
        private static FieldInfo psykerDisciplineDefAbilitiesField;

        // Window_PsykerDiscipline
        private static Type psykerDisciplineWindowType;
        private static FieldInfo psykerDisciplineWindowCompField;
        private static FieldInfo psykerDisciplineWindowSelectedDefField;

        // Window_PsykerDisciplineMinor
        private static Type psykerDisciplineMinorWindowType;

        public CorruptionPsykers(ModContentPack mod)
        {
            // Window_PsykerDiscipline
            psykerDisciplineWindowType = AccessTools.TypeByName("Corruption.Psykers.Window_PsykerDiscipline");
            psykerDisciplineWindowCompField = AccessTools.Field(psykerDisciplineWindowType, "comp");
            psykerDisciplineWindowSelectedDefField = AccessTools.Field(psykerDisciplineWindowType, "selectedDef");
            MP.RegisterSyncMethod(psykerDisciplineWindowType, "ChoosePower");
            MP.RegisterSyncWorker<object>(SyncPsykerDisciplineWindow, psykerDisciplineWindowType);


            psykerDisciplineMinorWindowType = AccessTools.TypeByName("Corruption.Psykers.Window_PsykerDisciplineMinor");
            MP.RegisterSyncMethod(psykerDisciplineMinorWindowType, "ChoosePower");
            MP.RegisterSyncWorker<object>(SyncPsykerDisciplineMinorWindow, psykerDisciplineMinorWindowType);


            LongEventHandler.ExecuteWhenFinished(LatePatch);
        }

        private static void LatePatch()
        {
            // Window_Psyker
            psykerWindowType = AccessTools.TypeByName("Corruption.Psykers.Window_Psyker");
            psykerWindowCompField = AccessTools.Field(psykerWindowType, "comp");
            MP.RegisterSyncMethod(typeof(CorruptionPsykers), nameof(SyncedAddMinorDiscipline));

            MpCompat.harmony.Patch(AccessTools.Method(psykerWindowType, nameof(Window.DoWindowContents)),
                prefix: new HarmonyMethod(typeof(CorruptionPsykers), nameof(PrePsykerDoWindowContents)),
                postfix: new HarmonyMethod(typeof(CorruptionPsykers), nameof(PostPsykerDoWindowContents)));

            MP.RegisterSyncMethod(typeof(CorruptionPsykers), nameof(SyncedTryLearnPower));

            var psykerLearnablePowerType = AccessTools.TypeByName("Corruption.Psykers.PsykerLearnablePower");

            var type = AccessTools.TypeByName("Corruption.Psykers.CompPsyker");
            compPsykerTryLearnPowerMethod = AccessTools.Method(type, "TryLearnPower", new Type[] { psykerLearnablePowerType });
            compPsykerAddXpMethod = AccessTools.Method(type, "AddXP");
            compPsykerMainDisciplineField = AccessTools.Field(type, "MainDiscipline");
            compPsykerMinorDisciplinesField = AccessTools.Field(type, "minorDisciplines");
            compPsykerXpSyncField = MP.RegisterSyncField(type, "PsykerXP");

            type = AccessTools.TypeByName("Corruption.Psykers.PsykerDisciplineDef");
            psykerDisciplineDefAbilitiesField = AccessTools.Field(type, "abilities");

            var database = typeof(DefDatabase<>).MakeGenericType(new Type[] { type });
            getDefByShortHash = AccessTools.Method(database, "GetByShortHash");

            MpCompat.harmony.Patch(AccessTools.Method(psykerWindowType, "DrawSelectedPower"),
                transpiler: new HarmonyMethod(typeof(CorruptionPsykers), nameof(Transpiler)));
        }

        private static void PrePsykerDoWindowContents(Window __instance, ref object[] __state)
        {
            if (MP.IsInMultiplayer)
            {
                var comp = psykerWindowCompField.GetValue(__instance);
                // SyncField
                MP.WatchBegin();
                compPsykerXpSyncField.Watch(comp);

                var list = (IList)compPsykerMinorDisciplinesField.GetValue(comp);
                __state = new object[list.Count];
                list.CopyTo(__state, 0);
            }
        }

        private static void PostPsykerDoWindowContents(Window __instance, ref object[] __state)
        {
            if (MP.IsInMultiplayer)
            {
                MP.WatchEnd();

                var comp = psykerWindowCompField.GetValue(__instance);
                var list = (IList)compPsykerMinorDisciplinesField.GetValue(comp);

                if (__state.Length != list.Count)
                {
                    foreach (var item in list)
                    {
                        if (!__state.Contains(item))
                        {
                            SyncedAddMinorDiscipline((ThingComp)comp, ((Def)item).shortHash);
                            break;
                        }
                    }
                }
            }
        }

        private static void SyncedAddMinorDiscipline(ThingComp comp, ushort hash)
            => ((IList)compPsykerMinorDisciplinesField.GetValue(comp)).Add(getDefByShortHash.Invoke(null, new object[] { hash }));

        private static void SyncPsykerDisciplineWindow(SyncWorker sync, ref object obj) => SyncPsykerDisciplineWindowAny(sync, ref obj, psykerDisciplineWindowType);

        private static void SyncPsykerDisciplineMinorWindow(SyncWorker sync, ref object obj) => SyncPsykerDisciplineWindowAny(sync, ref obj, psykerDisciplineMinorWindowType);

        private static void SyncPsykerDisciplineWindowAny(SyncWorker sync, ref object obj, Type windowType)
        {
            if (sync.isWriting)
            {
                sync.Write((ThingComp)psykerDisciplineWindowCompField.GetValue(obj));
                sync.Write(((Def)psykerDisciplineWindowSelectedDefField.GetValue(obj)).shortHash);
            }
            else
            {
                var comp = sync.Read<ThingComp>();
                var hash = sync.Read<ushort>();
                var def = getDefByShortHash.Invoke(null, new object[] { hash });

                // If the window exists, we try to find a window for the discipline field for that pawn
                obj = Find.WindowStack.Windows.FirstOrDefault(x => x.GetType() == windowType && (ThingComp)psykerDisciplineWindowCompField.GetValue(x) == comp);

                // If a specific player doesn't have the psyker menu open we'll have null here, we need to create it for our synced method
                if (obj == null)
                {
                    // The Window_Psyker is needed for constructor and the synced method
                    // It won't really do anything useful, but is needed
                    var psykerWindow = Activator.CreateInstance(psykerWindowType, comp);
                    obj = Activator.CreateInstance(windowType, comp, psykerWindow);
                }

                // Set the def to the correct one (someone might have another one selected, so make sure the same one is picked for everyone)
                psykerDisciplineWindowSelectedDefField.SetValue(obj, def);
            }
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instr)
        {
            foreach (var ci in instr)
            {
                if (ci.opcode == OpCodes.Callvirt && ci.operand is MethodInfo method && method == compPsykerTryLearnPowerMethod)
                {
                    ci.opcode = OpCodes.Call;
                    ci.operand = AccessTools.Method(typeof(CorruptionPsykers), nameof(InjectedTryLearnPower));
                }

                yield return ci;
            }
        }

        private static bool InjectedTryLearnPower(ThingComp comp, object selectedPower)
        {
            var disciplineDef = compPsykerMainDisciplineField.GetValue(comp);
            var list = (IList)psykerDisciplineDefAbilitiesField.GetValue(disciplineDef);
            var index = list.IndexOf(selectedPower);

            if (index >= 0)
            {
                SyncedTryLearnPower(comp, int.MinValue, index);
            }
            else
            {
                var defsList = (IList)compPsykerMinorDisciplinesField.GetValue(comp);
                for (int i = 0; i < defsList.Count; i++)
                {
                    list = (IList)psykerDisciplineDefAbilitiesField.GetValue(defsList[i]);
                    index = list.IndexOf(selectedPower);

                    if (index >= 0)
                    {
                        SyncedTryLearnPower(comp, i, index);
                        break;
                    }
                }
            }

            return true;
        }

        private static void SyncedTryLearnPower(ThingComp comp, int disciplineIndex, int abilityIndex)
        {
            IList abilityList;

            if (disciplineIndex >= 0)
            {
                var defsList = (IList)compPsykerMinorDisciplinesField.GetValue(comp);
                abilityList = (IList)psykerDisciplineDefAbilitiesField.GetValue(defsList[disciplineIndex]);
            }
            else
            {
                var disciplineDef = compPsykerMainDisciplineField.GetValue(comp);
                abilityList = (IList)psykerDisciplineDefAbilitiesField.GetValue(disciplineDef);
            }

            compPsykerTryLearnPowerMethod.Invoke(comp, new object[] { abilityList[abilityIndex] });
        }
    }
}
