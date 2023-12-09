using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Cash Register by Orion</summary>
    /// <see href="https://github.com/OrionFive/CashRegister"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2506046833"/>
    [MpCompatFor("Orion.CashRegister")]
    internal class CashRegister
    {
        // Gizmos
        private static ConstructorInfo gizmoRadiusConstructor;
        private static AccessTools.FieldRef<object, Building[]> gizmoSelectionField;
        // Cash register building
        private static SyncType cashRegisterType;
        private static AccessTools.FieldRef<object, IList> shiftsListField;
        // Shift
        private static ConstructorInfo shiftConstructor;
        private static AccessTools.FieldRef<object, object> timetableField;
        private static AccessTools.FieldRef<object, List<Pawn>> assignedField;
        // TimetableBool
        private static AccessTools.FieldRef<object, List<bool>> timesField;

        public CashRegister(ModContentPack mod)
        {
            var type = AccessTools.TypeByName("CashRegister.Shifts.ITab_Register_Shifts");
            MpCompat.RegisterLambdaMethod(type, "GetGizmos", 1).SetContext(SyncContext.MapSelected);
            MP.RegisterSyncWorker<object>(NoSync, type, shouldConstruct: true);
            MP.RegisterSyncMethod(typeof(CashRegister), nameof(SyncedSetShifts)).ExposeParameter(1).ExposeParameter(2).ExposeParameter(3).ExposeParameter(4).ExposeParameter(5).MinTime(100);
            MpCompat.harmony.Patch(AccessTools.Method(type, "FillTab"),
                prefix: new HarmonyMethod(typeof(CashRegister), nameof(PreFillTab)),
                postfix: new HarmonyMethod(typeof(CashRegister), nameof(PostFillTab)));

            type = AccessTools.TypeByName("CashRegister.Gizmo_Radius");
            gizmoRadiusConstructor = AccessTools.GetDeclaredConstructors(type).First(x => x.GetParameters().Length == 1);
            gizmoSelectionField = AccessTools.FieldRefAccess<Building[]>(type, "selection");
            MP.RegisterSyncMethod(AccessTools.DeclaredMethod(type, "ButtonDown"));
            MP.RegisterSyncMethod(AccessTools.DeclaredMethod(type, "ButtonUp"));
            MP.RegisterSyncMethod(AccessTools.DeclaredMethod(type, "ButtonCenter"));
            MP.RegisterSyncWorker<Gizmo>(SyncGizmoRadius, type);

            type = AccessTools.TypeByName("CashRegister.Building_CashRegister");
            cashRegisterType = type.MakeArrayType();
            shiftsListField = AccessTools.FieldRefAccess<IList>(type, "shifts");

            type = AccessTools.TypeByName("CashRegister.Shifts.Shift");
            shiftConstructor = AccessTools.Constructor(type);
            timetableField = AccessTools.FieldRefAccess<object>(type, "timetable");
            assignedField = AccessTools.FieldRefAccess<List<Pawn>>(type, "assigned");

            type = AccessTools.TypeByName("CashRegister.Timetable.TimetableBool");
            timesField = AccessTools.FieldRefAccess<List<bool>>(type, "times");
        }

        // Just use shouldConstruct: true, it's all we need
        private static void NoSync(SyncWorker sync, ref object obj)
        { }

        private static void SyncGizmoRadius(SyncWorker sync, ref Gizmo gizmo)
        {
            if (sync.isWriting)
                sync.Write(gizmoSelectionField(gizmo), cashRegisterType);
            else
            {
                var input = sync.Read<object>(cashRegisterType);
                gizmo = (Gizmo)gizmoRadiusConstructor.Invoke(new[] { input });
            }
        }

        private static void PreFillTab(ITab __instance, ref object[] __state)
        {
            var shifts = shiftsListField(__instance.SelObject);

            __state = new object[3];

            __state[0] = shifts.Count;

            var times = new bool[shifts.Count][];
            for (var i = 0; i < shifts.Count; i++)
                times[i] = timesField(timetableField(shifts[i])).ToArray();
            __state[1] = times;

            var assigned = new Pawn[shifts.Count][];
            for (var i = 0; i < shifts.Count; i++)
                assigned[i] = assignedField(shifts[i]).ToArray();
            __state[2] = assigned;
        }

        private static void PostFillTab(ITab __instance, ref object[] __state)
        {
            var shifts = shiftsListField(__instance.SelObject);

            var times = (bool[][])__state[1];
            var pawns = (Pawn[][])__state[2];

            void Sync()
            {
                var exposable = new IExposable[5];
                for (var i = 0; i < shifts.Count; i++)
                    exposable[i] = (IExposable)shifts[i];

                SyncedSetShifts((Building)__instance.SelObject, exposable[0], exposable[1], exposable[2], exposable[3], exposable[4]);

                // Restore old values
                shifts.Clear();
                for (var i = 0; i < times.Length; i++)
                {
                    var shift = shiftConstructor.Invoke(Array.Empty<object>());
                    timesField(timetableField(shift)) = times[i].ToList();
                    assignedField(shift) = pawns[i].ToList();
                    shifts.Add(shift);
                }
            }

            if (shifts.Count != (int)__state[0])
            {
                Sync();
                return;
            }

            for (var i = 0; i < shifts.Count; i++)
            {
                var timesNew = timesField(timetableField(shifts[i]));
                var timesOld = times[i];

                for (var indexTimes = 0; indexTimes < timesNew.Count; indexTimes++)
                {
                    if (timesNew[indexTimes] != timesOld[indexTimes])
                    {
                        Sync();
                        return;
                    }
                }

                var pawnsNew = assignedField(shifts[i]);
                var pawnsOld = pawns[i];

                if (pawnsNew.Count != pawnsOld.Length)
                {
                    Sync();
                    return;
                }

                for (var indexPawns = 0; indexPawns < pawnsNew.Count; indexPawns++)
                {
                    if (pawnsNew[indexPawns] != pawnsOld[indexPawns])
                    {
                        Sync();
                        return;
                    }
                }
            }
        }

        private static void SyncedSetShifts(Building register, IExposable firstShift, IExposable secondShift, IExposable thirdShift, IExposable fourthShift, IExposable fifthShift)
        {
            var list = shiftsListField(register);
            list.Clear();
            if (firstShift == null) return;
            list.Add(firstShift);
            if (secondShift == null) return;
            list.Add(secondShift);
            if (thirdShift == null) return;
            list.Add(thirdShift);
            if (fourthShift == null) return;
            list.Add(fourthShift);
            if (fifthShift == null) return;
            list.Add(fifthShift);
        }
    }
}
