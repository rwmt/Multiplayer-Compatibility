using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Giddy Up! Core by Roolo</summary>
    /// <see href="https://github.com/rheirman/GiddyUpCore/"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=1216999901"/>
    [MpCompatFor("Giddy-up! Core")]
    class GiddyUpCoreCompat
    {
        static Dictionary<object, Pawn> backward;
        static Dictionary<Pawn, object> forward;

        public GiddyUpCoreCompat(ModContentPack mod)
        {
            Type type;

            // Gizmos
            {
                // Release Animals
                MP.RegisterSyncDelegate(
                    AccessTools.TypeByName("GiddyUpCore.Harmony.Pawn_PlayerSettings_GetGizmos"),
                    "<>c__DisplayClass1_0",
                    "<helpIterator>b__2"
                );

                // Mount
                MP.RegisterSyncDelegate(
                    AccessTools.TypeByName("GiddyUpCore.Utilities.GUC_FloatMenuUtility"),
                    "<>c__DisplayClass0_0",
                    "<AddMountingOptions>b__0"
                );

                // Dismount
                MP.RegisterSyncDelegate(
                    AccessTools.TypeByName("GiddyUpCore.Utilities.GUC_FloatMenuUtility"),
                    "<>c__DisplayClass0_0",
                    "<AddMountingOptions>b__1"
                );
            }

            // Sync ExtendedPawnData
            {
                type = AccessTools.TypeByName("GiddyUpCore.Storage.ExtendedDataStorage");

                MpCompat.harmony.Patch(AccessTools.Constructor(type),
                    postfix: new HarmonyMethod(typeof(GiddyUpCoreCompat), nameof(ExtendedDataStoragePostfix)));

                MpCompat.harmony.Patch(AccessTools.Method(type, "GetExtendedDataFor", new Type[] { typeof(Pawn) }),
                    postfix: new HarmonyMethod(typeof(GiddyUpCoreCompat), nameof(GetExtendedDataForPostfix)));

                MpCompat.harmony.Patch(AccessTools.Method(type, "DeleteExtendedDataFor", new Type[] { typeof(Pawn) }),
                    postfix: new HarmonyMethod(typeof(GiddyUpCoreCompat), nameof(DeleteExtendedDataForPostfix)));
            }
            {
                type = AccessTools.TypeByName("GiddyUpCore.Storage.ExtendedPawnData");

                MP.RegisterSyncWorker<object>(ExtendedPawnData, type);
            }

            // Remove Random
            // 2020-04-20: disabled as CodeMatcher was removed from Harmony
            /*
            {
                MpCompat.harmony.Patch(AccessTools.Method("GiddyUpCore.Utilities.NPCMountUtility:generateMounts"),
                    prefix: new HarmonyMethod(typeof(GiddyUpCoreCompat), nameof(GenerateMountsPrefix)),
                    postfix: new HarmonyMethod(typeof(GiddyUpCoreCompat), nameof(GenerateMountsPostfix)),
                    transpiler: new HarmonyMethod(typeof(GiddyUpCoreCompat), nameof(GenerateMountsTranspiler)));
            }
            */
        }

        #region Sync ExtendedPawnData

        static void ExtendedDataStoragePostfix()
        {
            forward = new Dictionary<Pawn, object>();
            backward = new Dictionary<object, Pawn>();
        }

        static void GetExtendedDataForPostfix(Pawn pawn, object __result)
        {
            if (forward.ContainsKey(pawn)) {
                return;
            }
            forward.Add(pawn, __result);
            backward.Add(__result, pawn);
        }

        static void DeleteExtendedDataForPostfix(Pawn pawn)
        {
            if (!forward.ContainsKey(pawn)) {
                return;
            }
            backward.Remove(forward[pawn]);
            forward.Remove(pawn);
        }

        static void ExtendedPawnData(SyncWorker sync, ref object obj)
        {
            Pawn pawn = null;

            if (sync.isWriting) {
                pawn = backward[obj];

                if (pawn != null) {
                    sync.Write(pawn);
                }

            } else {
                pawn = sync.Read<Pawn>();

                obj = forward[pawn];
            }
        }

        #endregion

        #region Remove the Random

        static void GenerateMountsPrefix()
        {
            if (MP.IsInMultiplayer) {
                Rand.PushState(MakeMeARandom());
            }
        }
        static void GenerateMountsPostfix()
        {
            if (MP.IsInMultiplayer) {
                Rand.PopState();
            }
        }
        /*
        static IEnumerable<CodeInstruction> GenerateMountsTranspiler(IEnumerable<CodeInstruction> e, ILGenerator generator)
        {
            var myRandom = AccessTools.Method(typeof(GiddyUpCoreCompat), nameof(MakeMeARandom));
            var target = AccessTools.Property(typeof(DateTime), nameof(DateTime.Millisecond)).GetGetMethod();

            return new CodeMatcher(e, generator)
                .MatchForward(false, new CodeMatch(OpCodes.Call, target))
                .RemoveInstruction()
                .Insert(new CodeInstruction(OpCodes.Call, myRandom))
                .Instructions();
        }
        */

        static int MakeMeARandom()
        {
            return Find.TickManager.TicksAbs;
        }
        #endregion
    }
}