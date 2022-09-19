using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>PokéWorld by Gargamiel</summary>
    /// <see href="https://github.com/Gargamiel/PokeWorld"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2652029657"/>
    [MpCompatFor("Gargamiel.PokeWorld")]
    internal class PokeWorld
    {
        // CompPokemon
        private static AccessTools.FieldRef<object, object> pokemonFormTrackerField;
        private static AccessTools.FieldRef<object, object> pokemonLevelTrackerField;
        private static AccessTools.FieldRef<object, object> pokemonMoveTrackerField;
        // Trackers for CompPokemon
        private static AccessTools.FieldRef<object, ThingComp> formTrackerCompField;
        private static AccessTools.FieldRef<object, ThingComp> levelTrackerCompField;
        private static AccessTools.FieldRef<object, ThingComp> moveTrackerCompField;
        private static AccessTools.FieldRef<object, ThingComp> shinyTrackerCompField;
        // CompProperties_Pokemon
        private static AccessTools.FieldRef<object, IList> propsFormsListField;

        // Inner class inside of FormTracker
        private static AccessTools.FieldRef<object, object> innerClassFormField;
        private static AccessTools.FieldRef<object, object> innerClassParentField;

        // ITab_ContentsStorageSystem
        private static ConstructorInfo storageTabConstructor;
        private static FastInvokeHandler interfaceDropMethod;
        // StorageSystem
        private static Type storageSystemType;

        // I was allowed to use PokéWorld as class name.
        // However, it caused issues with auto completion.
        public PokeWorld(ModContentPack mod)
        {
            var type = AccessTools.TypeByName("PokeWorld.CompPokemon");
            pokemonFormTrackerField = AccessTools.FieldRefAccess<object>(type, "formTracker");
            pokemonLevelTrackerField = AccessTools.FieldRefAccess<object>(type, "levelTracker");
            pokemonMoveTrackerField = AccessTools.FieldRefAccess<object>(type, "moveTracker");

            type = AccessTools.TypeByName("PokeWorld.CompProperties_Pokemon");
            propsFormsListField = AccessTools.FieldRefAccess<IList>(type, "forms");

            // Gizmos
            {
                type = AccessTools.TypeByName("PokeWorld.PutInBallUtility");
                MP.RegisterSyncMethod(type, "UpdatePutInBallDesignation"); // Only called from CompPokemon gizmo

                type = AccessTools.TypeByName("PokeWorld.PutInPortableComputerUtility");
                MP.RegisterSyncMethod(type, "UpdatePutInPortableComputerDesignation"); // Only called from CryptosleepBall gizmo

                type = AccessTools.TypeByName("PokeWorld.FormTracker");
                formTrackerCompField = AccessTools.FieldRefAccess<ThingComp>(type, "comp");

                type = AccessTools.Inner(type, "<>c__DisplayClass17_0");
                innerClassFormField = AccessTools.FieldRefAccess<object>(type, "form");
                innerClassParentField = AccessTools.FieldRefAccess<object>(type, "<>4__this");
                MP.RegisterSyncMethod(type, "<ProcessInput>b__0");
                MP.RegisterSyncWorker<object>(SyncFormTrackerInnerClass, type, shouldConstruct: true);

                type = AccessTools.TypeByName("PokeWorld.LevelTracker");
                MpCompat.RegisterLambdaMethod(type, "GetGizmos", 1, 2);
                levelTrackerCompField = AccessTools.FieldRefAccess<ThingComp>(type, "comp");
                MP.RegisterSyncWorker<object>(SyncLevelTracker, type);

                // There's a bunch of gizmos in PokemonAttackGizmoUtility, but they don't seem like they need syncing.
                // In my testing they seemed fine. The way they're made I believe those should be handled by MP itself.
            }

            // ITab
            {
                type = AccessTools.TypeByName("PokeWorld.ITab_ContentsPokeball");
                MP.RegisterSyncMethod(type, "OnDropThing").SetContext(SyncContext.MapSelected);
                MP.RegisterSyncWorker<object>(NoSync, type, shouldConstruct: true);

                type = AccessTools.TypeByName("PokeWorld.ITab_ContentsStorageSystem");
                MP.RegisterSyncMethod(type, "InterfaceDrop").SetContext(SyncContext.MapSelected);
                var method = AccessTools.Method(type, "InterfaceDrop");
                storageTabConstructor = AccessTools.DeclaredConstructor(type);
                interfaceDropMethod = MethodInvoker.GetHandler(method);
                MpCompat.harmony.Patch(method,
                    prefix: new HarmonyMethod(typeof(PokeWorld), nameof(PreInterfaceDrop)));
                MP.RegisterSyncMethod(typeof(PokeWorld), nameof(SyncedInterfaceDrop)).SetContext(SyncContext.MapSelected);

                storageSystemType = AccessTools.TypeByName("PokeWorld.StorageSystem");

                type = AccessTools.TypeByName("PokeWorld.MoveTracker");
                MP.RegisterSyncMethod(type, "SetWanted"); // Only called from ITab_Pawn_Moves/MoveCardUtility checkbox
                moveTrackerCompField = AccessTools.FieldRefAccess<ThingComp>(type, "comp");
                MP.RegisterSyncWorker<object>(SyncMoveTracker, type);
            }

            // RNG
            {
                type = AccessTools.TypeByName("PokeWorld.ShinyTracker");
                shinyTrackerCompField = AccessTools.FieldRefAccess<ThingComp>(type, "comp");
                MpCompat.harmony.Patch(AccessTools.Method(type, "TryMakeShinyMote"),
                    prefix: new HarmonyMethod(typeof(PokeWorld), nameof(PreTryMakeShinyMote)),
                    postfix: new HarmonyMethod(typeof(PokeWorld), nameof(PostTryMakeShinyMote)));
            }
        }

        private static bool PreInterfaceDrop(Thing t)
        {
            if (!MP.IsInMultiplayer || MP.IsExecutingSyncCommand)
                return true;

            SyncedInterfaceDrop(t.thingIDNumber);
            return false;
        }

        private static void PreTryMakeShinyMote(object __instance)
        {
            if (MP.IsInMultiplayer)
            {
                var comp = shinyTrackerCompField(__instance);
                Rand.PushState(comp.parent.GetHashCode() ^ Find.TickManager.TicksGame);
            }
        }

        private static void PostTryMakeShinyMote()
        {
            if (MP.IsInMultiplayer) 
                Rand.PopState();
        }

        private static void SyncedInterfaceDrop(int id)
        {
            var comp = (IThingHolder)Find.World.GetComponent(storageSystemType);
            var thing = comp.GetDirectlyHeldThings().FirstOrDefault(x => x.thingIDNumber == id);
            interfaceDropMethod(storageTabConstructor.Invoke(Array.Empty<object>()), thing);
        }

        // Only needed in cases object needs to be created (shouldConstruct), but we don't care about any data inside of it
        private static void NoSync(SyncWorker sync, ref object obj)
        { }

        private static void SyncLevelTracker(SyncWorker sync, ref object tracker)
        {
            if (sync.isWriting)
                sync.Write(levelTrackerCompField(tracker));
            else
                tracker = pokemonLevelTrackerField(sync.Read<ThingComp>());
        }

        private static void SyncMoveTracker(SyncWorker sync, ref object tracker)
        {
            if (sync.isWriting)
                sync.Write(moveTrackerCompField(tracker));
            else
                tracker = pokemonMoveTrackerField(sync.Read<ThingComp>());
        }

        private static void SyncFormTrackerInnerClass(SyncWorker sync, ref object inner)
        {
            if (sync.isWriting)
            {
                var tracker = innerClassParentField(inner);
                var comp = formTrackerCompField(tracker);
                sync.Write(comp);

                var formList = propsFormsListField(comp.props);
                var index = formList.IndexOf(innerClassFormField(inner));
                sync.Write(index);
            }
            else
            {
                var comp = sync.Read<ThingComp>();
                var tracker = pokemonFormTrackerField(comp);
                innerClassParentField(inner) = tracker;

                var index = sync.Read<int>();
                if (index >= 0)
                {
                    var formList = propsFormsListField(comp.props);
                    innerClassFormField(inner) = formList[index];
                }
            }
        }
    }
}
