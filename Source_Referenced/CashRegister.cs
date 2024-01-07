using System.Collections.Generic;
using System.Linq;
using CashRegister;
using CashRegister.Shifts;
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
        #region Fields

        private const int CheckInterval = 60;

        #endregion

        #region Main patch

        public CashRegister(ModContentPack mod)
        {
            // Input
            {
                MpCompat.RegisterLambdaMethod(typeof(ITab_Register_Shifts), nameof(ITab_Register_Shifts.GetGizmos), 1).SetContext(SyncContext.MapSelected);
                MP.RegisterSyncWorker<object>(NoSync, typeof(ITab_Register_Shifts), shouldConstruct: true);
                MP.RegisterSyncMethod(typeof(CashRegister), nameof(SyncedSetShifts)).MinTime(100);
                MpCompat.harmony.Patch(AccessTools.Method(typeof(ITab_Register_Shifts), nameof(ITab_Register_Shifts.FillTab)),
                    prefix: new HarmonyMethod(typeof(CashRegister), nameof(PreFillTab)),
                    postfix: new HarmonyMethod(typeof(CashRegister), nameof(PostFillTab)));

                MP.RegisterSyncMethod(AccessTools.DeclaredMethod(typeof(Gizmo_Radius), "ButtonDown"));
                MP.RegisterSyncMethod(AccessTools.DeclaredMethod(typeof(Gizmo_Radius), "ButtonUp"));
                MP.RegisterSyncMethod(AccessTools.DeclaredMethod(typeof(Gizmo_Radius), "ButtonCenter"));
                MP.RegisterSyncWorker<Gizmo_Radius>(SyncGizmoRadius);

                MP.RegisterSyncWorker<Shift>(SyncShift);

                MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(CompAssignableToPawn), nameof(CompAssignableToPawn.TryAssignPawn)),
                    prefix: new HarmonyMethod(typeof(CashRegister), nameof(PreTryAssignPawn)));
                MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(CompAssignableToPawn), nameof(CompAssignableToPawn.TryUnassignPawn)),
                    prefix: new HarmonyMethod(typeof(CashRegister), nameof(PreTryUnassignPawn)));
                MP.RegisterSyncMethod(typeof(CashRegister), nameof(SyncedTryAssignPawnShifts));
                MP.RegisterSyncMethod(typeof(CashRegister), nameof(SyncedTryUnassignPawnShifts));
            }

            // Real time usage
            {
                MpCompat.harmony.Patch(AccessTools.DeclaredPropertyGetter(typeof(Building_CashRegister), nameof(Building_CashRegister.IsActive)),
                    prefix: new HarmonyMethod(typeof(CashRegister), nameof(PreIsActive)),
                    postfix: new HarmonyMethod(typeof(CashRegister), nameof(PostIsActive)));

                MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(Building_CashRegister), nameof(Building_CashRegister.HasToWork)),
                    prefix: new HarmonyMethod(typeof(CashRegister), nameof(PreHasToWork)),
                    postfix: new HarmonyMethod(typeof(CashRegister), nameof(PostHasToWork)));

                MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(Building_CashRegister), nameof(Building_CashRegister.ExposeData)),
                    prefix: new HarmonyMethod(typeof(CashRegister), nameof(InitFieldsExposeData)));
                MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(Building_CashRegister), nameof(Building_CashRegister.PostMake)),
                    prefix: new HarmonyMethod(typeof(CashRegister), nameof(InitFieldsPostMake)));
            }
        }

        #endregion

        #region Input

        // Just use shouldConstruct: true, it's all we need
        private static void NoSync(SyncWorker sync, ref object obj)
        {
        }

        private static void SyncGizmoRadius(SyncWorker sync, ref Gizmo_Radius gizmo)
        {
            if (sync.isWriting)
                sync.Write(gizmo.selection);
            else
            {
                var input = sync.Read<Building_CashRegister[]>();
                gizmo = new Gizmo_Radius(input);
            }
        }

        private static void PreFillTab(ITab_Register_Shifts __instance, ref object[] __state)
        {
            var shifts = __instance.Register.shifts;

            __state = new object[3];

            __state[0] = shifts.Count;

            var times = new bool[shifts.Count][];
            for (var i = 0; i < shifts.Count; i++)
                times[i] = shifts[i].timetable.times.ToArray();
            __state[1] = times;

            var assigned = new Pawn[shifts.Count][];
            for (var i = 0; i < shifts.Count; i++)
                assigned[i] = shifts[i].assigned.ToArray();
            __state[2] = assigned;
        }

        private static void PostFillTab(ITab_Register_Shifts __instance, object[] __state)
        {
            var shifts = __instance.Register.shifts;

            var times = (bool[][])__state[1];
            var pawns = (Pawn[][])__state[2];

            void Sync()
            {
                var newShifts = new Shift[shifts.Count];
                for (var i = 0; i < shifts.Count; i++)
                    newShifts[i] = shifts[i];

                SyncedSetShifts(__instance.Register, newShifts);

                // Restore old values
                shifts.Clear();
                for (var i = 0; i < times.Length; i++)
                {
                    var shift = new Shift
                    {
                        timetable =
                        {
                            times = times[i].ToList()
                        },
                        assigned = pawns[i].ToList(),
                        map = Find.CurrentMap,
                    };
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
                var timesNew = shifts[i].timetable.times;
                var timesOld = times[i];

                for (var indexTimes = 0; indexTimes < timesNew.Count; indexTimes++)
                {
                    if (timesNew[indexTimes] != timesOld[indexTimes])
                    {
                        Sync();
                        return;
                    }
                }

                var pawnsNew = shifts[i].assigned;
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

        private static void SyncedSetShifts(Building_CashRegister register, Shift[] shifts)
        {
            register.shifts.Clear();
            register.shifts.AddRange(shifts);
        }

        private static void SyncShift(SyncWorker sync, ref Shift shift)
        {
            // We could just sync it as IExposable, but it spams A LOT with errors about map not being deep saved.
            if (sync.isWriting)
            {
                sync.Write(shift.map);
                sync.Write(shift.assigned);
                sync.Write(shift.timetable.times);
            }
            else
            {
                shift = new Shift
                {
                    map = sync.Read<Map>(),
                    assigned = sync.Read<List<Pawn>>(),
                    timetable =
                    {
                        times = sync.Read<List<bool>>(),
                    },
                };
            }
        }

        private static bool PreTryAssignPawn(CompAssignableToPawn __instance, Pawn pawn)
        {
            // Only catch the comp from CashRegister mod
            if (!MP.IsInMultiplayer || !MP.InInterface || __instance is not CompAssignableToPawn_Shifts shift)
                return true;

            SyncedTryAssignPawnShifts(shift, pawn, GetRegisterShiftToIndex(shift));
            // Stop the vanilla method from running, blocking the method from being synced.
            return false;
        }

        // Uses the default values of sort = true, uninstall = false - skip grabbing those
        private static bool PreTryUnassignPawn(CompAssignableToPawn __instance, Pawn pawn)
        {
            // Only catch the comp from CashRegister mod
            if (!MP.IsInMultiplayer || !MP.InInterface || __instance is not CompAssignableToPawn_Shifts shift)
                return true;

            SyncedTryUnassignPawnShifts(shift, pawn, GetRegisterShiftToIndex(shift));
            // Stop the vanilla method from running, blocking the method from being synced.
            return false;
        }

        private static void SyncedTryAssignPawnShifts(CompAssignableToPawn_Shifts comp, Pawn pawn, byte shiftIndex)
        {
            var shift = GetRegisterIndexToShift(comp, shiftIndex);
            if (shift == null)
                return;
            var current = comp.assignedPawns;

            try
            {
                comp.assignedPawns = shift;
                comp.TryAssignPawn(pawn);
            }
            finally
            {
                comp.assignedPawns = current;
            }
        }

        private static void SyncedTryUnassignPawnShifts(CompAssignableToPawn_Shifts comp, Pawn pawn, byte shiftIndex)
        {
            var shift = GetRegisterIndexToShift(comp, shiftIndex);
            if (shift == null)
                return;
            var current = comp.assignedPawns;

            try
            {
                comp.assignedPawns = shift;
                comp.TryUnassignPawn(pawn);
            }
            finally
            {
                comp.assignedPawns = current;
            }
        }

        private static byte GetRegisterShiftToIndex(CompAssignableToPawn_Shifts comp)
        {
            var shiftIndex = comp.Register.shifts.FirstIndexOf(s => s.assigned == comp.assignedPawns);
            if (shiftIndex >= 0)
                return (byte)shiftIndex;

            Log.Error($"Trying to send shift data for {nameof(CompAssignableToPawn_Shifts)} for {comp.Register}, but there is no matching shift.");
            return byte.MaxValue;
        }

        private static List<Pawn> GetRegisterIndexToShift(CompAssignableToPawn_Shifts comp, byte shiftIndex)
        {
            if (shiftIndex < byte.MaxValue && shiftIndex < comp.Register.shifts.Count)
                return comp.Register.shifts[shiftIndex].assigned;

            Log.Error($"Trying to read shift data for {nameof(CompAssignableToPawn_Shifts)} for {comp.Register}, but received a data for a nonexistent shift.");
            return null;
        }

        #endregion

        #region Real time usage

        private static bool PreIsActive(ref float ___lastActiveCheck, bool ___isActive, ref bool __result, out bool __state)
        {
            // Let it run normally in vanilla
            if (!MP.IsInMultiplayer)
            {
                __state = false;
                return true;
            }

            if (!MP.InInterface && ___lastActiveCheck <= Find.TickManager.TicksGame)
            {
                // Force the code to re-check isActive
                ___lastActiveCheck = float.MinValue;
                // Force postfix to change lastActiveCheck to use ticks instead of real time
                __state = true;
                return true;
            }

            __state = false;
            // Disable the method and just return the result ourselves, as the original code would re-calculate the value
            __result = ___isActive;
            return false;
        }

        private static void PostIsActive(ref float ___lastActiveCheck, bool __state)
        {
            // If prefix forced the original code to run, we need to change it to use ticks
            if (__state)
                ___lastActiveCheck = Find.TickManager.TicksGame + CheckInterval;
        }

        private static bool PreHasToWork(Pawn pawn, ref float ___lastCheckActivePawns, HashSet<Pawn> ___activePawns, ref bool __result, out bool __state)
        {
            // Let it run normally in vanilla
            if (!MP.IsInMultiplayer)
            {
                __state = false;
                return true;
            }

            if (!MP.InInterface && ___lastCheckActivePawns <= Find.TickManager.TicksGame)
            {
                // Force the code to re-check isActive
                ___lastCheckActivePawns = float.MinValue;
                // Force postfix to change lastActiveCheck to use ticks instead of real time
                __state = true;
                return true;
            }

            __state = false;
            // Disable the method and just return the result ourselves, as the original code would re-calculate the value
            __result = ___activePawns.Contains(pawn);
            return false;
        }

        private static void PostHasToWork(ref float ___lastCheckActivePawns, bool __state)
        {
            // If prefix forced the original code to run, we need to change it to use ticks
            if (__state)
                ___lastCheckActivePawns = Find.TickManager.TicksGame + CheckInterval;
        }

        private static void InitFieldsPostMake(Building_CashRegister __instance, ref float ___lastActiveCheck, ref float ___lastCheckActivePawns)
        {
            if (MP.IsInMultiplayer)
                InitCheckTicks(__instance, out ___lastActiveCheck, out ___lastCheckActivePawns);
        }

        private static void InitFieldsExposeData(Building_CashRegister __instance, ref float ___lastActiveCheck, ref float ___lastCheckActivePawns)
        {
            if (MP.IsInMultiplayer && Scribe.mode == LoadSaveMode.PostLoadInit)
                InitCheckTicks(__instance, out ___lastActiveCheck, out ___lastCheckActivePawns);
        }

        private static void InitCheckTicks(Building_CashRegister instance, out float lastActiveCheck, out float lastCheckActivePawns)
        {
            // Ensure the tick at which the checks happen will be slightly staggered
            // so they all aren't done at the same tick for every single register.
            // It won't happen every 60 ticks anyway, as the checks tend to be done
            // much less frequently.
            var offset = instance.HashOffset() % CheckInterval;
            lastActiveCheck = Find.TickManager.TicksGame + offset;
            lastCheckActivePawns = Find.TickManager.TicksGame + offset + 7;
        }

        #endregion
    }
}