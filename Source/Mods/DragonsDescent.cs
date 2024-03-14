using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Dragon's Descent by Onyxae</summary>
    /// <see href="https://github.com/Aether-Guild/Dragons-Descent/"/>
    /// <see href="https://steamcommunity.com/workshop/filedetails/?id=2026992161"/>
    [MpCompatFor("onyxae.dragonsdescent")]
    public class DragonsDescent
    {
        //// Altar and Rituals ////
        // Command_RitualEffect
        private static ConstructorInfo ritualEffectCommandCtor;
        private static AccessTools.FieldRef<object, Thing> ritualEffectCommandSourceField;
        private static AccessTools.FieldRef<object, object> ritualEffectCommandRitualField;
        private static AccessTools.FieldRef<object, object> ritualEffectCommandRitualRequestField;
        private static FastInvokeHandler ritualEffectCommandCreateSetupMethod;

        // MapComponent_Tracker
        private static Type mapComponentTrackerType;
        private static AccessTools.FieldRef<object, object> mapComponentTrackerRitualsField;

        // Ritual
        private static AccessTools.FieldRef<object, Def> ritualDefField;

        // RitualActivator
        private static FastInvokeHandler ritualActivatorInitializeMethod;

        // RitualTracker
        private static AccessTools.FieldRef<object, Map> ritualTrackerMapField;

        public DragonsDescent(ModContentPack mod)
        {
            // Incubator
            {
                // (Toggle) accelerate growth, and dev commands: reset progress, force tick, hatch now
                MpCompat.RegisterLambdaMethod("DD.CompEggIncubator", "CompGetGizmosExtra", 1, 2, 3, 4)
                    .Skip(1)
                    .SetDebugOnly();

                // Place on ground
                MpCompat.RegisterLambdaDelegate("DD.CompProperties_EggIncubator", "CreateGizmo", 0);
            }

            // AbilityComp_AbilityControl seems unused, skipping this gizmo

            // Altar and Rituals
            {
                // MapComponent_Tracker
                mapComponentTrackerType = AccessTools.TypeByName("DD.MapComponent_Tracker");
                mapComponentTrackerRitualsField = AccessTools.FieldRefAccess<object>(mapComponentTrackerType, "rituals");

                // Ritual
                ritualDefField = AccessTools.FieldRefAccess<Def>("DD.Ritual:def");

                //RitualActivator
                ritualActivatorInitializeMethod = MethodInvoker.GetHandler(AccessTools.Method("DD.RitualActivator:Initialize"));

                // RitualTracker
                var ritualTrackerType = AccessTools.TypeByName("DD.RitualTracker");
                ritualTrackerMapField = AccessTools.FieldRefAccess<Map>(ritualTrackerType, "map");
                MP.RegisterSyncWorker<object>(SyncRitualTracker, ritualTrackerType);
            }

            LongEventHandler.ExecuteWhenFinished(LatePatch);
        }

        private static void LatePatch()
        {
            // Altar and Rituals
            // Command_RitualEffect
            var ritualEffectCommand = AccessTools.TypeByName("DD.Command_RitualEffect");
            var ritualTrackerType = AccessTools.TypeByName("DD.RitualTracker");
            var ritualDefType = AccessTools.TypeByName("DD.RitualDef");

            ritualEffectCommandCtor = AccessTools.DeclaredConstructor(ritualEffectCommand, new[] { typeof(Thing), ritualTrackerType, ritualDefType });
            ritualEffectCommandSourceField = AccessTools.FieldRefAccess<Thing>(ritualEffectCommand, "source");
            ritualEffectCommandRitualField = AccessTools.FieldRefAccess<object>(ritualEffectCommand, "ritual");
            ritualEffectCommandRitualRequestField = AccessTools.FieldRefAccess<object>(ritualEffectCommand, "ritualRequest");
            ritualEffectCommandCreateSetupMethod = MethodInvoker.GetHandler(AccessTools.PropertyGetter(ritualEffectCommand, "CreateSetup"));

            MP.RegisterSyncMethod(ritualEffectCommand, "ActivateOnNoTarget").SetPreInvoke(PreActivateRitual);
            MP.RegisterSyncMethod(ritualEffectCommand, "ActivateOnLocalTarget").SetPreInvoke(PreActivateRitual);
            MP.RegisterSyncMethod(ritualEffectCommand, "ActivateOnGlobalTarget").SetPreInvoke(PreActivateRitual);
            MP.RegisterSyncWorker<Command>(SyncRitualEffectCommand, ritualEffectCommand);
        }

        private static void SyncRitualTracker(SyncWorker sync, ref object tracker)
        {
            if (sync.isWriting)
                sync.Write(ritualTrackerMapField(tracker));
            else
            {
                var map = sync.Read<Map>();
                var comp = map.GetComponent(mapComponentTrackerType);
                tracker = mapComponentTrackerRitualsField(comp);
            }
        }

        private static void PreActivateRitual(object instance, object[] _)
        {
            // Create the request
            var ritualRequest = ritualEffectCommandCreateSetupMethod(instance);
            // Get the source
            var source = ritualEffectCommandSourceField(instance);
            // Initialize the request
            ritualActivatorInitializeMethod(ritualRequest, source);
            // Set the field inside of the instance to the request we created
            ritualEffectCommandRitualRequestField(instance) = ritualRequest;
        }

        private static void SyncRitualEffectCommand(SyncWorker sync, ref Command command)
        {
            if (sync.isWriting)
            {
                sync.Write(ritualEffectCommandSourceField(command));
                sync.Write(Find.CurrentMap.GetComponent(mapComponentTrackerType));

                var ritual = ritualEffectCommandRitualField(command);
                sync.Write(ritualDefField(ritual));
            }
            else
            {
                var source = sync.Read<Thing>();
                var mapComponent = sync.Read<MapComponent>();
                var tracker = mapComponentTrackerRitualsField(mapComponent);
                var def = sync.Read<Def>();

                command = (Command)ritualEffectCommandCtor.Invoke(new[] { source, tracker, def });
            }
        }
    }
}