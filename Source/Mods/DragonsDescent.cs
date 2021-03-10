using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using Verse;

namespace Multiplayer.Compat
{
    [MpCompatFor("onyxae.dragonsdescent")]
    public class DragonsDescent
    {
        // AbilityComp_AbilityControl
        private static MethodInfo abilityCompGizmoGetter;
        // CompHostileResponse
        private static FieldInfo hostilityResponseTypeField; // Skipping the getter/setter here
        // Inner class inside of CompHostileResponse
        private static FieldInfo compHostileResponseField;
        // CompRitualAltar
        private static Type compRitualAltarType;
        // RitualTracker
        private static MethodInfo ritualTrackerGetRitualMethod;
        private static FieldInfo ritualTrackerMapField;
        // MapComponent_Tracker
        private static Type trackerMapComponentType;
        private static FieldInfo trackerMapComponentRitualsField;
        // RitualReference
        private static Type ritualReferenceType;
        private static FieldInfo ritualReferenceDefField;
        // Inner class inside of RitualReference
        private static FieldInfo ritualReferenceInnerRitualField;
        private static FieldInfo ritualReferenceInnerSelfField;
        private static FieldInfo ritualReferenceInnerParentField;
        private static FieldInfo ritualReferenceInnerRitualsField;
        // CompProperties_Ritual
        private static FieldInfo ritualCompPropertiesRitualsField;

        public DragonsDescent(ModContentPack mod)
        {
            // Incubator
            {
                var type = AccessTools.TypeByName("DD.CompEggIncubator");
                var methods = MpCompat.RegisterSyncMethodsByIndex(type, "<CompGetGizmosExtra>", 1, 2, 3, 4);
                foreach (var method in methods.Skip(1)) // All but the first one are debug-only gizmos
                    method.SetDebugOnly();

                type = AccessTools.TypeByName("DD.CompProperties_EggIncubator");
                MP.RegisterSyncDelegate(type, "<>c__DisplayClass7_0", "<CreateGizmo>b__0");
            }

            // Abilities
            {
                var type = AccessTools.TypeByName("DD.AbilityComp_AbilityControl");
                var gizmoActionMethod = MpCompat.MethodByIndex(type, "<get_Gizmo>", 1);
                MP.RegisterSyncMethod(gizmoActionMethod); // Toggle active
                MpCompat.harmony.Patch(gizmoActionMethod,
                    prefix: new HarmonyMethod(typeof(DragonsDescent), nameof(PreGizmoActionCalled)));
                abilityCompGizmoGetter = AccessTools.PropertyGetter(type, "Gizmo");
            }

            // Hostility response type changing
            {
                var type = AccessTools.TypeByName("DD.CompHostileResponse");
                hostilityResponseTypeField = AccessTools.Field(type, "type");

                var inner = AccessTools.Inner(type, "<>c__DisplayClass15_0");
                MP.RegisterSyncMethod(typeof(DragonsDescent), nameof(SyncSetHostilityResponseType));
                compHostileResponseField = AccessTools.Field(inner, "<>4__this");
                MpCompat.harmony.Patch(AccessTools.Method(inner, "<get_Gizmo>b__1"),
                    prefix: new HarmonyMethod(typeof(DragonsDescent), nameof(PreSetHostilityResponseType)));
            }

            // Altar
            {
                compRitualAltarType = AccessTools.TypeByName("DD.CompRitualAltar");
                MP.RegisterSyncDelegate(compRitualAltarType, "<>c__DisplayClass11_0", "<CompGetGizmosExtra>b__0").SetDebugOnly();
                MP.RegisterSyncDelegate(compRitualAltarType, "<>c__DisplayClass11_0", "<CompGetGizmosExtra>b__1").SetDebugOnly();
                MP.RegisterSyncDelegate(compRitualAltarType, "<>c__DisplayClass11_0", "<CompGetGizmosExtra>b__2").SetDebugOnly();
                MP.RegisterSyncDelegate(compRitualAltarType, "<>c__DisplayClass11_0", "<CompGetGizmosExtra>b__3").SetDebugOnly();

                var type = AccessTools.TypeByName("DD.RitualTracker");
                MP.RegisterSyncWorker<object>(SyncRitualTracker, type);
                ritualTrackerGetRitualMethod = AccessTools.Method(type, "GetRitual");
                ritualTrackerMapField = AccessTools.Field(type, "map");

                trackerMapComponentType = AccessTools.TypeByName("DD.MapComponent_Tracker");
                trackerMapComponentRitualsField = AccessTools.Field(trackerMapComponentType, "rituals");

                ritualReferenceType = AccessTools.TypeByName("DD.RitualReference");
                ritualReferenceDefField = AccessTools.Field(ritualReferenceType, "def");

                var inner = AccessTools.Inner(ritualReferenceType, "<>c__DisplayClass12_0");
                MpCompat.RegisterSyncMethodsByIndex(inner, "<SetupAction>", 0, 1);
                MP.RegisterSyncWorker<object>(SyncRitualReferenceInnerClass, inner, shouldConstruct: true);
                ritualReferenceInnerRitualField = AccessTools.Field(inner, "ritual");
                ritualReferenceInnerSelfField = AccessTools.Field(inner, "<>4__this");
                ritualReferenceInnerParentField = AccessTools.Field(inner, "parent");
                ritualReferenceInnerRitualsField = AccessTools.Field(inner, "rituals");

                type = AccessTools.TypeByName("DD.CompProperties_Ritual");
                ritualCompPropertiesRitualsField = AccessTools.Field(type, "rituals");
            }
        }

        private static void PreGizmoActionCalled(AbilityComp __instance, Command ___gizmo)
        {
            if (MP.IsInMultiplayer && ___gizmo == null)
                abilityCompGizmoGetter.Invoke(__instance, Array.Empty<object>());
        }

        private static bool PreSetHostilityResponseType(object __instance, IList ___options, int ___index)
        {
            if (!MP.IsInMultiplayer) return true;

            // Skip actually changing the value for now, do it in a synced method
            SyncSetHostilityResponseType((ThingComp)compHostileResponseField.GetValue(__instance), (___index + 1) % ___options.Count);
            return false;
        }

        private static void SyncSetHostilityResponseType(ThingComp thing, int value) => hostilityResponseTypeField.SetValue(thing, value);

        private static void SyncRitualTracker(SyncWorker sync, ref object obj)
        {
            if (sync.isWriting)
                sync.Write((Map)ritualTrackerMapField.GetValue(obj));
            else
            {
                var map = sync.Read<Map>();
                var component = map.GetComponent(trackerMapComponentType);
                obj = trackerMapComponentRitualsField.GetValue(component);
            }
        }

        private static void SyncRitualReferenceInnerClass(SyncWorker sync, ref object obj)
        {
            if (sync.isWriting)
            {
                var ritualReference = ritualReferenceInnerSelfField.GetValue(obj);
                var parent = (ThingWithComps)ritualReferenceInnerParentField.GetValue(obj);
                var rituals = ritualReferenceInnerRitualsField.GetValue(obj);

                // Get the index of our ritual reference inside of the comp props
                var comp = parent.AllComps.First(x => x.GetType() == compRitualAltarType);
                var ritualsList = (IList)ritualCompPropertiesRitualsField.GetValue(comp.props);
                var index = ritualsList.IndexOf(ritualReference);

                // We need to be able to retrieve the RitualReference, otherwise we won't really be able to sync
                // (We could technically sync the Def inside of this object, but it ended up not syncing when I tried)
                sync.Write(index);
                if (index >= 0)
                {
                    sync.Write<Thing>(parent);
                    SyncRitualTracker(sync, ref rituals);
                }
            }
            else
            {
                var index = sync.Read<int>();

                if (index >= 0)
                {
                    var parent = sync.Read<Thing>();
                    object rituals = null;
                    SyncRitualTracker(sync, ref rituals);

                    ritualReferenceInnerParentField.SetValue(obj, parent);
                    ritualReferenceInnerRitualsField.SetValue(obj, rituals);

                    if (index >= 0)
                    {
                        var comp = ((ThingWithComps)parent).AllComps.First(x => x.GetType() == compRitualAltarType);
                        var ritualsList = (IList)ritualCompPropertiesRitualsField.GetValue(comp.props);
                        var ritualReference = ritualsList[index];
                        ritualReferenceInnerSelfField.SetValue(obj, ritualReference);

                        var def = ritualReferenceDefField.GetValue(ritualReference);
                        var ritualList = ritualTrackerGetRitualMethod.Invoke(rituals, new object[] { def });
                        ritualReferenceInnerRitualField.SetValue(obj, ritualList);
                    }
                }
            }
        }
    }
}
