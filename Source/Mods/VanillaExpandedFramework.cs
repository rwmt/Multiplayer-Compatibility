using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Multiplayer.API;
using RimWorld.Planet;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Vanilla Expanded Framework and other Vanilla Expanded mods by Oskar Potocki, Sarg Bjornson, Chowder, XeoNovaDan, Orion, Kikohi, erdelf, Taranchuk, and more</summary>
    /// <see href="https://github.com/juanosarg/ItemProcessor"/>
    /// <see href="https://github.com/juanosarg/VanillaCookingExpanded"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2023507013"/>
    [MpCompatFor("OskarPotocki.VanillaFactionsExpanded.Core")]
    class VanillaExpandedFramework
    {
        // Vanilla Furniture Expanded
        private static FieldInfo setStoneBuildingField;

        // MVCF
        // VerbManager
        private static ConstructorInfo mvcfVerbManagerCtor;
        private static MethodInfo mvcfInitializeManagerMethod;
        private static MethodInfo mvcfUniqueVerbOwnerIDMethod;
        private static MethodInfo mvcfPawnSetter;
        private static FieldInfo mvcfVerbsField;
        // WorldComponent_MVCF
        private static MethodInfo mvcfGetWorldCompMethod;
        private static FieldInfo mvcfAllManagersListField;
        private static FieldInfo mvcfManagersTableField;

        // System
        // WeakReference
        private static ConstructorInfo weakReferenceCtor;
        private static MethodInfo weakReferenceTryGetMethod;
        // ConditionalWeakTable
        private static MethodInfo conditionalWeakTableAddMethod;
        private static MethodInfo conditionalWeakTableTryGetValueMethod;

        public VanillaExpandedFramework(ModContentPack mod)
        {
            // ItemProcessor
            {
                var type = AccessTools.TypeByName("ItemProcessor.Building_ItemProcessor");
                // _1, _5 and _7 are used to check if gizmo should be enabled, so we don't sync them
                MpCompat.RegisterSyncMethodsByIndex(type, "<GetGizmos>", 0, 2, 3, 4, 6, 8, 9, 10);

                type = AccessTools.TypeByName("ItemProcessor.Command_SetQualityList");
                MP.RegisterSyncWorker<Command>(SyncCommandWithBuilding, type, shouldConstruct: true);
                MpCompat.RegisterSyncMethodsByIndex(type, "<ProcessInput>", Enumerable.Range(0, 8).ToArray());

                type = AccessTools.TypeByName("ItemProcessor.Command_SetOutputList");
                MP.RegisterSyncWorker<Command>(SyncCommandWithBuilding, type, shouldConstruct: true);
                MP.RegisterSyncMethod(type, "TryConfigureIngredientsByOutput");

                // Keep an eye on this in the future, seems like something the devs could combine into a single class at some point
                foreach (var ingredientNumber in new[] { "First", "Second", "Third", "Fourth" })
                {
                    type = AccessTools.TypeByName($"ItemProcessor.Command_Set{ingredientNumber}ItemList");
                    MP.RegisterSyncWorker<Command>(SyncSetIngredientCommand, type, shouldConstruct: true);
                    MP.RegisterSyncMethod(type, $"TryInsert{ingredientNumber}Thing");
                    MpCompat.RegisterSyncMethodsByIndex(type, "<ProcessInput>", 0);
                }
            }

            // Vanilla Cooking Expanded
            {
                // AddHediff desyncs with Arbiter, but seems fine without it
                PatchingUtilities.PatchPushPopRand("VanillaCookingExpanded.Thought_Hediff:MoodOffset");
            }

            // VFE Core
            {
                var type = AccessTools.TypeByName("VFECore.CompPawnDependsOn");
                MpCompat.RegisterSyncMethodByIndex(type, "<CompGetGizmosExtra>", 0).SetDebugOnly();
            }

            // Vanilla Furniture Expanded
            {
                var type = AccessTools.TypeByName("VanillaFurnitureExpanded.CompConfigurableSpawner");
                MpCompat.RegisterSyncMethodByIndex(type, "<CompGetGizmosExtra>", 0).SetDebugOnly();

                type = AccessTools.TypeByName("VanillaFurnitureExpanded.Command_SetItemsToSpawn");
                MP.RegisterSyncDelegate(type, "<>c__DisplayClass2_0", "<ProcessInput>b__1");

                type = AccessTools.TypeByName("VanillaFurnitureExpanded.CompRockSpawner");
                MpCompat.RegisterSyncMethodByIndex(type, "<CompGetGizmosExtra>", 0);

                type = AccessTools.TypeByName("VanillaFurnitureExpanded.Command_SetStoneType");
                setStoneBuildingField = AccessTools.Field(type, "building");
                MpCompat.RegisterSyncMethodByIndex(type, "<ProcessInput>", 0);
                MP.RegisterSyncWorker<Command>(SyncSetStoneTypeCommand, type, shouldConstruct: true);
                MP.RegisterSyncDelegate(type, "<>c__DisplayClass2_0", "<ProcessInput>b__1");
            }

            // Vanilla Faction Mechanoids
            {
                var type = AccessTools.TypeByName("VFE.Mechanoids.CompMachineChargingStation");
                MP.RegisterSyncDelegate(type, "<>c", "<CompGetGizmosExtra>b__21_1", Array.Empty<string>()).SetContext(SyncContext.MapSelected);
                MP.RegisterSyncDelegate(type, "<>c", "<CompGetGizmosExtra>b__21_6", Array.Empty<string>()).SetContext(SyncContext.MapSelected);
                MP.RegisterSyncDelegate(type, "<>c__DisplayClass21_0", "<CompGetGizmosExtra>b__4");
            }

            // AnimalBehaviours
            {
                // RNG
                PatchingUtilities.PatchSystemRand("AnimalBehaviours.DamageWorker_ExtraInfecter:ApplySpecialEffectsToPart", false);
                PatchingUtilities.PatchSystemRandCtor(new[] { "AnimalBehaviours.CompAnimalProduct", "AnimalBehaviours.CompGasProducer" }, false);

                // Gizmos
                // Might not work, as I could not find a mod that uses this to test this
                var type = AccessTools.TypeByName("AnimalBehaviours.CompDestroyThisItem");
                MP.RegisterSyncMethod(type, "SetObjectForDestruction");
                MP.RegisterSyncMethod(type, "CancelObjectForDestruction");
            }

            // MVCF (Multi Verb Combat Framework)
            {
                var type = AccessTools.TypeByName("MVCF.WorldComponent_MVCF");
                mvcfGetWorldCompMethod = AccessTools.Method(type, "GetComp");
                mvcfAllManagersListField = AccessTools.Field(type, "allManagers");
                mvcfManagersTableField = AccessTools.Field(type, "managers");
                MP.RegisterSyncMethod(typeof(VanillaExpandedFramework), nameof(InitVerbManager));
                MpCompat.harmony.Patch(AccessTools.Method(type, "GetManagerFor"),
                    prefix: new HarmonyMethod(typeof(VanillaExpandedFramework), nameof(GetManagerForPrefix)));

                type = AccessTools.TypeByName("MVCF.VerbManager");
                MP.RegisterSyncWorker<object>(SyncVerbManager, type, isImplicit: true);
                mvcfVerbManagerCtor = AccessTools.Constructor(type);
                mvcfInitializeManagerMethod = AccessTools.Method(type, "Initialize");
                mvcfUniqueVerbOwnerIDMethod = AccessTools.Method(type, "UniqueVerbOwnerID");
                mvcfPawnSetter = AccessTools.PropertySetter(type, "Pawn");
                mvcfVerbsField = AccessTools.Field(type, "verbs");

                var weakReferenceType = typeof(System.WeakReference<>).MakeGenericType(new[] { type });
                weakReferenceCtor = AccessTools.FirstConstructor(weakReferenceType, ctor => ctor.GetParameters().Count() == 1);
                weakReferenceTryGetMethod = AccessTools.Method(weakReferenceType, "TryGetTarget");

                var conditionalWeakTableType = typeof(System.Runtime.CompilerServices.ConditionalWeakTable<,>).MakeGenericType(new[] { typeof(Pawn), type });
                conditionalWeakTableAddMethod = AccessTools.Method(conditionalWeakTableType, "Add");
                conditionalWeakTableTryGetValueMethod = AccessTools.Method(conditionalWeakTableType, "TryGetValue");

                type = AccessTools.TypeByName("MVCF.ManagedVerb");
                MP.RegisterSyncWorker<object>(SyncManagedVerb, type, isImplicit: true);
                // Seems like selecting the Thing that holds the verb inits some stuff, so we need to set the context
                MP.RegisterSyncMethod(type, "Toggle");

                type = AccessTools.TypeByName("MVCF.Harmony.Gizmos");
                MP.RegisterSyncDelegate(type, "<>c__DisplayClass4_0", "<GetGizmos_Postfix>b__1");
                MP.RegisterSyncDelegate(type, "<>c__DisplayClass5_0", "<GetAttackGizmos_Postfix>b__4");
                MP.RegisterSyncDelegate(type, "<>c__DisplayClass6_0", "<Pawn_GetGizmos_Postfix>b__0");
            }
        }

        private static void SyncCommandWithBuilding(SyncWorker sync, ref Command command)
        {
            var traverse = Traverse.Create(command);
            var building = traverse.Field("building");

            if (sync.isWriting)
                sync.Write(building.GetValue() as Thing);
            else
                building.SetValue(sync.Read<Thing>());
        }

        private static void SyncSetIngredientCommand(SyncWorker sync, ref Command command)
        {
            var traverse = Traverse.Create(command);
            var building = traverse.Field("building");
            var ingredientList = traverse.Field("things");

            if (sync.isWriting)
            {
                sync.Write(building.GetValue() as Thing);
                sync.Write(ingredientList.GetValue() as List<Thing>);
            }
            else
            {
                building.SetValue(sync.Read<Thing>());
                ingredientList.SetValue(sync.Read<List<Thing>>());
            }
        }

        private static void SyncSetStoneTypeCommand(SyncWorker sync, ref Command obj)
        {
            if (sync.isWriting)
                sync.Write(setStoneBuildingField.GetValue(obj) as ThingComp);
            else
                setStoneBuildingField.SetValue(obj, sync.Read<ThingComp>());
        }

        private static void SyncVerbManager(SyncWorker sync, ref object obj)
        {
            var comp = mvcfGetWorldCompMethod.Invoke(null, Array.Empty<object>());
            var allManagers = mvcfAllManagersListField.GetValue(comp) as IList;

            if (sync.isWriting)
            {
                var foundManager = false;

                // Try to find our VerbManager (obj) inside of the list of weak references to VerManager
                // If we find it we sync the unique ID (string), so we can easily find it
                // The manager should be there somehwere, but in case we can't find it we sync null (as string)
                // Trying to use an index is not a good idea, as the list of weak references is too volatile for that
                foreach (var weakReference in allManagers)
                {
                    var outParam = new object[] { null };
                    if ((bool)weakReferenceTryGetMethod.Invoke(weakReference, outParam) && outParam[0] == obj)
                    {
                        sync.Write((string)mvcfUniqueVerbOwnerIDMethod.Invoke(outParam[0], Array.Empty<object>()));
                        foundManager = true;
                        break;
                    }
                }

                if (!foundManager) sync.Write((string)null);
            }
            else
            {
                // If the ID we got isn't null then we try to find the VerbManager with the ID we got
                var managerId = sync.Read<string>();
                if (managerId != null)
                {
                    foreach (var weakReference in allManagers)
                    {
                        var outParam = new object[] { null };
                        if ((bool)weakReferenceTryGetMethod.Invoke(weakReference, outParam) && (string)mvcfUniqueVerbOwnerIDMethod.Invoke(outParam[0], Array.Empty<object>()) == managerId)
                        {
                            obj = outParam[0];
                            break;
                        }
                    }
                }
            }
        }

        private static void SyncManagedVerb(SyncWorker sync, ref object obj)
        {
            var comp = mvcfGetWorldCompMethod.Invoke(null, Array.Empty<object>());
            var allManagers = mvcfAllManagersListField.GetValue(comp) as IList;

            if (sync.isWriting)
            {
                var foundVerb = false;

                // Try to find the VerbManager which holds our ManagedVerb (obj)
                // Once we find both the verb inside of a verb manager, we sync both the index of the
                // verb inside of the manager, as well as the unique ID of the manager (string)
                // If we can't find it we only sync -1 (we omit the string),
                // as negative value for index is a neat way here to display that we don't have a value
                // Trying to use an index for the verb manager itself is not a good idea, as the list of weak references is too volatile for that
                foreach (var weakReference in allManagers)
                {
                    var outParam = new object[] { null };
                    if ((bool)weakReferenceTryGetMethod.Invoke(weakReference, outParam))
                    {
                        var managedVerbs = mvcfVerbsField.GetValue(outParam[0]) as IList;

                        for (int verbIndex = 0; verbIndex < managedVerbs.Count; verbIndex++)
                        {
                            if (managedVerbs[verbIndex] == obj)
                            {
                                sync.Write(verbIndex);
                                sync.Write((string)mvcfUniqueVerbOwnerIDMethod.Invoke(outParam[0], Array.Empty<object>()));
                                foundVerb = true;
                            }
                        }
                    }
                }

                if (!foundVerb) sync.Write(-1);
            }
            else
            {
                // If the index we got is bigger to or equal to zero, then we also read the ID of verb manager.
                // Then, we try to find the manager with the ID and find the verb with specific index inside of it
                var verbIndex = sync.Read<int>();
                if (verbIndex >= 0)
                {
                    var managerId = sync.Read<string>();

                    foreach (var weakReference in allManagers)
                    {
                        var outParam = new object[] { null };
                        if ((bool)weakReferenceTryGetMethod.Invoke(weakReference, outParam) && (string)mvcfUniqueVerbOwnerIDMethod.Invoke(outParam[0], Array.Empty<object>()) == managerId)
                        {
                            var managedVerbs = mvcfVerbsField.GetValue(outParam[0]) as IList;
                            obj = managedVerbs[verbIndex];
                        }
                    }
                }
            }
        }

        private static bool GetManagerForPrefix(Pawn pawn, bool createIfMissing, WorldComponent __instance, ref object __result)
        {
            if (!createIfMissing) return true; // We don't care and let the method run, we only care if we might need to creat a VerbManager

            var table = mvcfManagersTableField.GetValue(__instance);
            var parameters = new object[] { pawn, null };

            if ((bool)conditionalWeakTableTryGetValueMethod.Invoke(table, parameters))
            {
                // Might as well give the result back instead of continuing the normal execution of the method,
                // as it would just do the same stuff as we do here again
                __result = parameters[1];
            }
            else
            {
                // We basically setup an empty reference, but we'll initialize it in the synced method.
                // We just return the reference for it so other objects can use it now. The data they
                // have will be updated after the sync, so the gizmos related to verbs might not be
                // shown immediately for players who selected specific pawns.
                __result = CreateAndAddVerbManagerToCollections(pawn, __instance, table: table);
                // Delay the initialization of the VerbManager to a synced method
                InitVerbManager(pawn);
            }

            return false;
        }

        // Synced method for initializing the verb manager for all players, used in sitations where the moment of creation of the verb might not be synced
        private static void InitVerbManager(Pawn pawn)
        {
            var comp = (WorldComponent)mvcfGetWorldCompMethod.Invoke(null, Array.Empty<object>());
            var table = mvcfManagersTableField.GetValue(comp);
            var parameters = new object[] { pawn, null };
            object verbManager;

            // Try to find the verb manager first, as it might exist (and it will definitely exist for at least one player)
            if ((bool)conditionalWeakTableTryGetValueMethod.Invoke(table, parameters))
                verbManager = parameters[1];
            // If the verb manager doesn't exist, we create an empty one here and add it to the verb manager list and table
            else
                verbManager = CreateAndAddVerbManagerToCollections(pawn, comp);

            mvcfInitializeManagerMethod.Invoke(verbManager, new object[] { pawn });
        }

        // Helper method for creating an empty verb manager for a pawn
        private static object CreateAndAddVerbManagerToCollections(Pawn pawn, WorldComponent worldComponent, object list = null, object table = null)
        {
            var verbManager = mvcfVerbManagerCtor.Invoke(Array.Empty<object>());
            // The pawn is used to get ID, so we might need it to exist for the purpose of syncing it (if the manager wasn't initialized yet)
            mvcfPawnSetter.Invoke(verbManager, new object[] { pawn });

            if (list == null) list = mvcfAllManagersListField.GetValue(worldComponent);
            if (table == null) table = mvcfManagersTableField.GetValue(worldComponent);

            conditionalWeakTableAddMethod.Invoke(table, new object[] { pawn, verbManager });
            ((IList)list).Add(weakReferenceCtor.Invoke(new object[] { verbManager }));

            return verbManager;
        }
    }
}
