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
    /// <summary>Corruption: Psykers by Cpt. Ohu, Updated by Ogliss</summary>
    /// <see href="https://github.com/Ogliss/Corruption.Psykers"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2547886449"/>
    [MpCompatFor("CptOhu.CorruptionPsyker")]
    public class CorruptionPsykers
    {
        // DefDatabase<PsykerDisciplineDef>
        private static FastInvokeHandler getDefByShortHash;

        // Window_Psyker
        private static Type psykerWindowType;
        private static AccessTools.FieldRef<object, ThingComp> psykerWindowCompField;

        // CompPsyker
        private static FastInvokeHandler compPsykerTryLearnAbilityMethod;
        private static AccessTools.FieldRef<object, Def> compPsykerMainDisciplineField;
        private static AccessTools.FieldRef<object, IList> compPsykerMinorDisciplinesField;
        private static ISyncField compPsykerXpSyncField;

        // PsykerDisciplineDef
        private static AccessTools.FieldRef<object, IList> psykerDisciplineDefAbilitiesField;

        // Window_PsykerDiscipline
        private static Type psykerDisciplineWindowType;
        private static AccessTools.FieldRef<object, ThingComp> psykerDisciplineWindowCompField;
        private static AccessTools.FieldRef<object, Def> psykerDisciplineWindowSelectedDefField;

        // Window_PsykerDisciplineMinor
        private static Type psykerDisciplineMinorWindowType;

        public CorruptionPsykers(ModContentPack mod)
        {
            // Window_PsykerDiscipline
            psykerDisciplineWindowType = AccessTools.TypeByName("Corruption.Psykers.Window_PsykerDiscipline");
            psykerDisciplineWindowCompField = AccessTools.FieldRefAccess<ThingComp>(psykerDisciplineWindowType, "comp");
            psykerDisciplineWindowSelectedDefField = AccessTools.FieldRefAccess<Def>(psykerDisciplineWindowType, "selectedDef");
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
            psykerWindowCompField = AccessTools.FieldRefAccess<ThingComp>(psykerWindowType, "comp");
            MP.RegisterSyncMethod(typeof(CorruptionPsykers), nameof(SyncedAddMinorDiscipline));

            MpCompat.harmony.Patch(AccessTools.Method(psykerWindowType, nameof(Window.DoWindowContents)),
                prefix: new HarmonyMethod(typeof(CorruptionPsykers), nameof(PrePsykerDoWindowContents)),
                postfix: new HarmonyMethod(typeof(CorruptionPsykers), nameof(PostPsykerDoWindowContents)));

            MP.RegisterSyncMethod(typeof(CorruptionPsykers), nameof(SyncedTryLearnPower));

            var psykerLearnablePowerType = AccessTools.TypeByName("Corruption.Core.Abilities.LearnableAbility");

            var type = AccessTools.TypeByName("Corruption.Psykers.CompPsyker");
            compPsykerTryLearnAbilityMethod = MethodInvoker.GetHandler(AccessTools.Method(type, "TryLearnAbility", new[] { psykerLearnablePowerType }));
            compPsykerMainDisciplineField = AccessTools.FieldRefAccess<Def>(type, "MainDiscipline");
            compPsykerMinorDisciplinesField = AccessTools.FieldRefAccess<IList>(type, "minorDisciplines");
            compPsykerXpSyncField = MP.RegisterSyncField(type, "PsykerXP");

            type = AccessTools.TypeByName("Corruption.Psykers.PsykerDisciplineDef");
            psykerDisciplineDefAbilitiesField = AccessTools.FieldRefAccess<IList>(type, "abilities");

            var database = typeof(DefDatabase<>).MakeGenericType(type);
            getDefByShortHash = MethodInvoker.GetHandler(AccessTools.Method(database, "GetByShortHash"));

            MpCompat.harmony.Patch(AccessTools.Method(psykerWindowType, "DrawSelectedPower"),
                transpiler: new HarmonyMethod(typeof(CorruptionPsykers), nameof(Transpiler)));
        }

        private static void PrePsykerDoWindowContents(Window __instance, ref object[] __state)
        {
            if (MP.IsInMultiplayer)
            {
                var comp = psykerWindowCompField(__instance);
                // SyncField
                MP.WatchBegin();
                compPsykerXpSyncField.Watch(comp);

                // Copy all the currently learned minor disciplines, we'll check later if there were any changes
                var list = compPsykerMinorDisciplinesField(comp);
                __state = new object[list.Count];
                list.CopyTo(__state, 0);
            }
        }

        private static void PostPsykerDoWindowContents(Window __instance, ref object[] __state)
        {
            if (MP.IsInMultiplayer)
            {
                MP.WatchEnd();

                var comp = psykerWindowCompField(__instance);
                var list = compPsykerMinorDisciplinesField(comp);

                // Check through all learned minor disciplines, look for any changes
                if (__state.Length != list.Count)
                {
                    foreach (var item in list)
                    {
                        if (!__state.Contains(item))
                        {
                            SyncedAddMinorDiscipline(comp, ((Def)item).shortHash);
                            break;
                        }
                    }
                }
            }
        }

        private static void SyncedAddMinorDiscipline(ThingComp comp, ushort hash)
            => compPsykerMinorDisciplinesField(comp).Add(getDefByShortHash.Invoke(null, new object[] { hash }));

        private static void SyncPsykerDisciplineWindow(SyncWorker sync, ref object obj) => SyncPsykerDisciplineWindowAny(sync, ref obj, psykerDisciplineWindowType);

        private static void SyncPsykerDisciplineMinorWindow(SyncWorker sync, ref object obj) => SyncPsykerDisciplineWindowAny(sync, ref obj, psykerDisciplineMinorWindowType);

        private static void SyncPsykerDisciplineWindowAny(SyncWorker sync, ref object obj, Type windowType)
        {
            if (sync.isWriting)
            {
                sync.Write(psykerDisciplineWindowCompField(obj));
                sync.Write(psykerDisciplineWindowSelectedDefField(obj).shortHash);
            }
            else
            {
                var comp = sync.Read<ThingComp>();
                var hash = sync.Read<ushort>();
                var def = getDefByShortHash.Invoke(null, new object[] { hash });

                // If the window exists, we try to find a window for the discipline field for that pawn
                obj = Find.WindowStack.Windows.FirstOrDefault(x => x.GetType() == windowType && psykerDisciplineWindowCompField(x) == comp);

                // If a specific player doesn't have the psyker menu open we'll have null here, we need to create it for our synced method
                if (obj == null)
                {
                    // The Window_Psyker is needed for constructor and the synced method
                    // It won't really do anything useful, but is needed
                    var psykerWindow = Activator.CreateInstance(psykerWindowType, comp);
                    obj = Activator.CreateInstance(windowType, comp, psykerWindow);
                }

                // Set the def to the correct one (someone might have another one selected, so make sure the same one is picked for everyone)
                psykerDisciplineWindowSelectedDefField(obj) = (Def)def;
            }
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instr)
        {
            var argumentType = AccessTools.TypeByName("Corruption.Psykers.PsykerLearnablePower");
            var type = AccessTools.TypeByName("Corruption.Psykers.CompPsyker");
            var tryLearnAbilityMethod = AccessTools.Method(type, "TryLearnAbility", new[] { argumentType });

            foreach (var ci in instr)
            {
                if (ci.opcode == OpCodes.Callvirt && ci.operand is MethodInfo method && method == tryLearnAbilityMethod)
                {
                    // Try replacing TryLearnPower from the mod with the one we have here
                    // Our method is synced (we shouldn't really sync the original method, as it's called from other places)
                    ci.opcode = OpCodes.Call;
                    ci.operand = AccessTools.Method(typeof(CorruptionPsykers), nameof(InjectedTryLearnPower));
                }

                yield return ci;
            }
        }

        private static bool InjectedTryLearnPower(ThingComp comp, object selectedPower)
        {
            var disciplineDef = compPsykerMainDisciplineField(comp);
            var list = psykerDisciplineDefAbilitiesField(disciplineDef);
            var index = list.IndexOf(selectedPower);

            // Main discipline, we have an index to it in the list
            if (index >= 0)
            {
                SyncedTryLearnPower(comp, int.MinValue, index);
            }
            // We don't have a discipline index, so it's a minor discipline - find which one and sync it
            else
            {
                var defsList = compPsykerMinorDisciplinesField(comp);
                for (int i = 0; i < defsList.Count; i++)
                {
                    list = psykerDisciplineDefAbilitiesField(defsList[i]);
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

            // Minor discipline
            if (disciplineIndex >= 0)
            {
                var defsList = compPsykerMinorDisciplinesField(comp);
                abilityList = psykerDisciplineDefAbilitiesField(defsList[disciplineIndex]);
            }
            // Main discipline
            else
            {
                var disciplineDef = compPsykerMainDisciplineField(comp);
                abilityList = psykerDisciplineDefAbilitiesField(disciplineDef);
            }

            compPsykerTryLearnAbilityMethod.Invoke(comp, abilityList[abilityIndex]);
        }
    }
}
