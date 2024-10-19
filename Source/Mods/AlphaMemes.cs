using System;
using System.Collections.Generic;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Alpha Memes by Sarg Bjornson, Helixien, Cassie, Luizi</summary>
    /// <see href="https://github.com/juanosarg/AlphaMemes"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2661356814"/>
    [MpCompatFor("Sarg.AlphaMemes")]
    internal class AlphaMemes
    {
        #region Fields

        private static Type thoughtCatharsisType;

        // Begin ritual dialog stuff selector
        // Last processed precept
        private static Precept_Ritual lastPrecept;

        // The type of the behavior worker
        private static Type funeralFrameworkBehaviorWorkerType;

        // Funeral's RitualBehaviorWorker field access
        private static AccessTools.FieldRef<RitualBehaviorWorker, ThingDef> funeralFrameworkBehaviorWorkerStuffToUseField;
        private static AccessTools.FieldRef<RitualBehaviorWorker, int> funeralFrameworkBehaviorWorkerStuffCountField;

        #endregion

        #region Main patch

        public AlphaMemes(ModContentPack mod)
        {
            LongEventHandler.ExecuteWhenFinished(LatePatch);
            MpCompatPatchLoader.LoadPatch(this);

            #region Gizmos

            {
                // For both of the methods, the corpse argument
                // is inaccessible. Transform the argument and
                // sync the corpse differently properly.
                var type = AccessTools.TypeByName("AlphaMemes.Comp_CorpseContainer");
                var corpseContainterCompCorpseField = AccessTools.FieldRefAccess<Corpse>(type, "innerCorpse");
                MP.RegisterSyncMethod(AccessTools.DeclaredMethod(type, "RemoveCorpse"))
                    .CancelIfAnyArgNull()
                    .TransformArgument(0, Serializer.New(
                        (Corpse _, object instance, object[] _) => (ThingComp)instance,
                        comp => corpseContainterCompCorpseField(comp)));

                type = AccessTools.TypeByName("AlphaMemes.Comp_CorpseContainerMulti");
                var multiCorpseContainerCompCorpseListField = AccessTools.FieldRefAccess<List<Corpse>>(type, "innerCorpses");
                MP.RegisterSyncMethod(AccessTools.DeclaredMethod(type, "RemoveCorpse"))
                    .CancelIfAnyArgNull()
                    .TransformArgument(0, Serializer.New(
                        (Corpse corpse, object instance, object[] _) => (comp: (ThingComp)instance, id: corpse.thingIDNumber),
                        data => multiCorpseContainerCompCorpseListField(data.comp).Find(c => c.thingIDNumber == data.id)));
            }

            #endregion

            #region Unsafe thoughts

            {
                // Hediffs added in MoodOffset, can be called during alert updates (not synced).
                thoughtCatharsisType = AccessTools.TypeByName("AlphaMemes.Thought_Catharsis");
                if (thoughtCatharsisType != null)
                    PatchingUtilities.PatchTryGainMemory(TryGainThoughtCatharsis);
                else
                    Log.Error("Trying to patch `AlphaMemes.Thought_Catharsis`, but the type is null. Did it get moved, renamed, or removed?");
            }

            #endregion

            #region Funeral ritual dialog

            {
                funeralFrameworkBehaviorWorkerType = AccessTools.TypeByName("AlphaMemes.RitualBehaviorWorker_FuneralFramework");

                var type = AccessTools.TypeByName("AlphaMemes.RitualBehaviorWorker_FuneralFramework");
                funeralFrameworkBehaviorWorkerStuffToUseField = AccessTools.FieldRefAccess<ThingDef>(type, "stuffToUse");
                funeralFrameworkBehaviorWorkerStuffCountField = AccessTools.FieldRefAccess<int>(type, "stuffCount");
            }

            #endregion
        }

        private static void LatePatch()
        {
            #region Gizmos

            {
                MpCompat.RegisterLambdaMethod("AlphaMemes.Pawn_Detonator", nameof(Pawn.GetGizmos), 0);
            }

            #endregion
        }

        #endregion

        #region Gain thought patch

        private static bool TryGainThoughtCatharsis(Thought_Memory thought)
        {
            if (!thoughtCatharsisType.IsInstanceOfType(thought))
                return false;

            // Call MoodOffset to cause the method to add hediffs, etc.
            thought.MoodOffset();
            return true;
        }

        #endregion

        #region Funeral ritual dialog

        [MpCompatSyncMethod]
        private static void SyncPreceptChanges(Precept_Ritual precept, ThingDef stuff, int count)
        {
            funeralFrameworkBehaviorWorkerStuffToUseField(precept.behavior) = stuff;
            funeralFrameworkBehaviorWorkerStuffCountField(precept.behavior) = count;
        }

        [MpCompatPrefix("AlphaMemes.Dialog_BeginRitual_DoWindowContents_Patch", "Postfix")]
        private static void PreFuneralRitualDialog(Precept_Ritual __2, ref (ThingDef stuff, int count)? __state)
        {
            // If not in MP or Ideology is disabled (classic mode),
            // clear last precept and return;
            if (!MP.IsInMultiplayer || Find.IdeoManager.classicMode)
            {
                lastPrecept = null;
                return;
            }

            // If the precept (or its behavior) are null or the
            // precept is not a subtype of funeral behavior worker,
            // clear last precept and return;
            if (__2?.behavior == null || !funeralFrameworkBehaviorWorkerType.IsInstanceOfType(__2.behavior))
            {
                lastPrecept = null;
                return;
            }

            lastPrecept = __2;

            // This postfix can modify those values to set them up,
            // so we need to watch it was well. It'll most likely
            // happen (at most) once per funeral ritual per game.
            __state =
            (
                funeralFrameworkBehaviorWorkerStuffToUseField(lastPrecept.behavior),
                funeralFrameworkBehaviorWorkerStuffCountField(lastPrecept.behavior)
            );
        }

        [MpCompatPrefix("AlphaMemes.Dialog_BeginRitual_DoWindowContents_Patch", "Postfix", 3)]
        private static void PreStuffCountChanged(ref (ThingDef stuff, int count)? __state)
        {
            if (lastPrecept == null)
                return;

            __state =
            (
                funeralFrameworkBehaviorWorkerStuffToUseField(lastPrecept.behavior),
                funeralFrameworkBehaviorWorkerStuffCountField(lastPrecept.behavior)
            );
        }

        [MpCompatFinalizer("AlphaMemes.Dialog_BeginRitual_DoWindowContents_Patch", "Postfix")]
        [MpCompatFinalizer("AlphaMemes.Dialog_BeginRitual_DoWindowContents_Patch", "Postfix", 3)]
        private static void PostStuffCountChanged((ThingDef stuff, int count)? __state)
        {
            // Last precept shouldn't be null here.
            if (__state == null || lastPrecept == null)
                return;

            // Sadly, we cannot use sync fields (with instance paths) here
            // due to one of the fields being in a subtype of
            // RitualBehaviorWorker, rather than the type itself.
            var (oldStuff, oldCount) = __state.Value;
            var stuff = funeralFrameworkBehaviorWorkerStuffToUseField(lastPrecept.behavior);
            var count = funeralFrameworkBehaviorWorkerStuffCountField(lastPrecept.behavior);

            if (stuff != oldStuff || count != oldCount)
                SyncPreceptChanges(lastPrecept, stuff, count);
        }

        [MpCompatPrefix("AlphaMemes.RitualBehaviorWorker_FuneralFramework", nameof(RitualBehaviorWorker.CanStartRitualNow))]
        private static void PreCanStartFuneral(TargetInfo target, ref int ___checkTick, ref bool __state)
        {
            if (!MP.IsInMultiplayer)
                return;

            // The code every 300 ticks will cache the result
            // the ritual is (not) possible to start. Since
            // the code is mostly called from the UI and only
            // occasionally from simulation (primarily executing
            // synced commands), make sure that the code will
            // always run during simulation to prevent desyncs.

            if (!MP.InInterface)
                ___checkTick = -1;

            // Seed the RNG using current tick and TargetInfo's
            // hash code, which will either use Thing's ID or
            // the map's hash code together with IntVec3's
            // x, y, and z coordinates.
            __state = true;
            Rand.PushState(Gen.HashCombineInt(Find.TickManager.TicksGame, target.GetHashCode()));
        }

        [MpCompatFinalizer("AlphaMemes.RitualBehaviorWorker_FuneralFramework", nameof(RitualBehaviorWorker.CanStartRitualNow))]
        private static void PostCanStartFuneral(bool __state)
        {
            if (__state)
                Rand.PopState();
        }

        #endregion
    }
}