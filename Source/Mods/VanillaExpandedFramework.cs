using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
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
        // VFECore
        private static FieldInfo learnedAbilitiesField;

        // Vanilla Furniture Expanded
        private static FieldInfo setStoneBuildingField;

        // MVCF
        // VerbManager
        private static ConstructorInfo mvcfVerbManagerCtor;
        private static MethodInfo mvcfInitializeManagerMethod;
        private static MethodInfo mvcfPawnGetter;
        private static FieldInfo mvcfVerbsField;
        // WorldComponent_MVCF
        private static MethodInfo mvcfGetWorldCompMethod;
        private static FieldInfo mvcfAllManagersListField;
        private static FieldInfo mvcfManagersTableField;
        // ManagedVerb
        private static FieldInfo mvcfManagerVerbManagerField;

        // System
        // WeakReference
        private static ConstructorInfo weakReferenceCtor;
        // ConditionalWeakTable
        private static MethodInfo conditionalWeakTableAddMethod;
        private static MethodInfo conditionalWeakTableTryGetValueMethod;

        public VanillaExpandedFramework(ModContentPack mod)
        {
            // ItemProcessor
            {
                var type = AccessTools.TypeByName("ItemProcessor.Building_ItemProcessor");
                // _1, _5 and _7 are used to check if gizmo should be enabled, so we don't sync them
                MpCompat.RegisterLambdaMethod(type, "GetGizmos", 0, 2, 3, 4, 6, 8, 9, 10);

                type = AccessTools.TypeByName("ItemProcessor.Command_SetQualityList");
                MP.RegisterSyncWorker<Command>(SyncCommandWithBuilding, type, shouldConstruct: true);
                MpCompat.RegisterLambdaMethod(type, "ProcessInput", Enumerable.Range(0, 8).ToArray());

                type = AccessTools.TypeByName("ItemProcessor.Command_SetOutputList");
                MP.RegisterSyncWorker<Command>(SyncCommandWithBuilding, type, shouldConstruct: true);
                MP.RegisterSyncMethod(type, "TryConfigureIngredientsByOutput");

                // Keep an eye on this in the future, seems like something the devs could combine into a single class at some point
                foreach (var ingredientNumber in new[] { "First", "Second", "Third", "Fourth" })
                {
                    type = AccessTools.TypeByName($"ItemProcessor.Command_Set{ingredientNumber}ItemList");
                    MP.RegisterSyncWorker<Command>(SyncSetIngredientCommand, type, shouldConstruct: true);
                    MP.RegisterSyncMethod(type, $"TryInsert{ingredientNumber}Thing");
                    MpCompat.RegisterLambdaMethod(type, "ProcessInput", 0);
                }
            }

            // Vanilla Cooking Expanded
            {
                // AddHediff desyncs with Arbiter, but seems fine without it
                PatchingUtilities.PatchPushPopRand("VanillaCookingExpanded.Thought_Hediff:MoodOffset");
            }

            // VFE Core
            {
                MpCompat.RegisterLambdaMethod("VFECore.CompPawnDependsOn", "CompGetGizmosExtra", 0).SetDebugOnly();

                learnedAbilitiesField = AccessTools.Field(AccessTools.TypeByName("VFECore.Abilities.CompAbilities"), "learnedAbilities");
                MP.RegisterSyncWorker<ITargetingSource>(SyncVEFAbility, AccessTools.TypeByName("VFECore.Abilities.Ability"), true);

                MP.RegisterSyncMethod(AccessTools.TypeByName("VFECore.Abilities.Ability"), "CreateCastJob");
            }

            // Vanilla Furniture Expanded
            {
                MpCompat.RegisterLambdaMethod("VanillaFurnitureExpanded.CompConfigurableSpawner", "CompGetGizmosExtra", 0).SetDebugOnly();

                var type = AccessTools.TypeByName("VanillaFurnitureExpanded.Command_SetItemsToSpawn");
                MpCompat.RegisterLambdaDelegate(type, "ProcessInput", 1);
                MP.RegisterSyncWorker<Command>(SyncCommandWithBuilding, type, shouldConstruct: true);

                MpCompat.RegisterLambdaMethod("VanillaFurnitureExpanded.CompRockSpawner", "CompGetGizmosExtra", 0);

                type = AccessTools.TypeByName("VanillaFurnitureExpanded.Command_SetStoneType");
                setStoneBuildingField = AccessTools.Field(type, "building");
                MpCompat.RegisterLambdaMethod(type, "ProcessInput", 0);
                MP.RegisterSyncWorker<Command>(SyncSetStoneTypeCommand, type, shouldConstruct: true);
                MpCompat.RegisterLambdaDelegate(type, "ProcessInput", 1);

                type = AccessTools.TypeByName("VanillaFurnitureExpanded.CompRandomBuildingGraphic");
                MpCompat.RegisterLambdaMethod(type, "CompGetGizmosExtra", 0);
            }

            // Vanilla Faction Mechanoids
            {
                var type = AccessTools.TypeByName("VFE.Mechanoids.CompMachineChargingStation");
                MpCompat.RegisterLambdaDelegate(type, "CompGetGizmosExtra", 1, 6).SetContext(SyncContext.MapSelected);
                MpCompat.RegisterLambdaDelegate(type, "CompGetGizmosExtra", 4);
            }

            // AnimalBehaviours
            {
                // RNG
                PatchingUtilities.PatchSystemRand("AnimalBehaviours.DamageWorker_ExtraInfecter:ApplySpecialEffectsToPart", false);
                var rngFixConstructors = new[]
                {
                    "AnimalBehaviours.CompAnimalProduct",
                    "AnimalBehaviours.CompFilthProducer",
                    "AnimalBehaviours.CompGasProducer",
                    "AnimalBehaviours.CompInitialHediff",
                    "AnimalBehaviours.DeathActionWorker_DropOnDeath",
                };
                PatchingUtilities.PatchSystemRandCtor(rngFixConstructors, false);

                // Gizmos
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
                MP.RegisterSyncMethod(typeof(VanillaExpandedFramework), nameof(SyncedInitVerbManager));
                MpCompat.harmony.Patch(AccessTools.Method(type, "GetManagerFor"),
                    prefix: new HarmonyMethod(typeof(VanillaExpandedFramework), nameof(GetManagerForPrefix)));

                type = AccessTools.TypeByName("MVCF.VerbManager");
                MP.RegisterSyncWorker<object>(SyncVerbManager, type, isImplicit: true);
                mvcfVerbManagerCtor = AccessTools.Constructor(type);
                mvcfInitializeManagerMethod = AccessTools.Method(type, "Initialize");
                mvcfPawnGetter = AccessTools.PropertyGetter(type, "Pawn");
                mvcfVerbsField = AccessTools.Field(type, "verbs");

                var weakReferenceType = typeof(System.WeakReference<>).MakeGenericType(new[] { type });
                weakReferenceCtor = AccessTools.FirstConstructor(weakReferenceType, ctor => ctor.GetParameters().Count() == 1);

                var conditionalWeakTableType = typeof(System.Runtime.CompilerServices.ConditionalWeakTable<,>).MakeGenericType(new[] { typeof(Pawn), type });
                conditionalWeakTableAddMethod = AccessTools.Method(conditionalWeakTableType, "Add");
                conditionalWeakTableTryGetValueMethod = AccessTools.Method(conditionalWeakTableType, "TryGetValue");

                type = AccessTools.TypeByName("MVCF.ManagedVerb");
                mvcfManagerVerbManagerField = AccessTools.Field(type, "man");
                MP.RegisterSyncWorker<object>(SyncManagedVerb, type, isImplicit: true);
                // Seems like selecting the Thing that holds the verb inits some stuff, so we need to set the context
                MP.RegisterSyncMethod(type, "Toggle");

                type = AccessTools.TypeByName("MVCF.Harmony.Gizmos");
                MpCompat.RegisterLambdaDelegate(type, "GetGizmos_Postfix", 1); // Fire at will
                MpCompat.RegisterLambdaDelegate(type, "GetAttackGizmos_Postfix", 4); // Interrupt Attack
                MpCompat.RegisterLambdaDelegate(type, "Pawn_GetGizmos_Postfix", 0); // Also interrupt Attack
            }

            // Explosive Trails Effect
            {
                // RNG
                PatchingUtilities.PatchPushPopRand("ExplosiveTrailsEffect.SmokeThrowher:ThrowSmokeTrail");
            }

            // KCSG (Custom Structure Generation)
            {
                // RNG
                var methods = new[]
                {
                    "KCSG.SymbolResolver_AddFields:Resolve",
                    "KCSG.SymbolResolver_Settlement:GenerateRooms",
                };

                PatchingUtilities.PatchSystemRand(methods, false);
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
                var ingredientListValue = ingredientList.GetValue();
                if (ingredientListValue == null)
                {
                    sync.Write(false);
                }
                else
                {
                    sync.Write(true);
                    sync.Write(ingredientList.GetValue() as List<Thing>);
                }
            }
            else
            {
                building.SetValue(sync.Read<Thing>());
                if (sync.Read<bool>()) ingredientList.SetValue(sync.Read<List<Thing>>());
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
            if (sync.isWriting)
                // Sync the pawn that has the VerbManager
                sync.Write((Pawn)mvcfPawnGetter.Invoke(obj, Array.Empty<object>()));
            else
            {
                var pawn = sync.Read<Pawn>();

                var comp = mvcfGetWorldCompMethod.Invoke(null, Array.Empty<object>());
                var weakTable = mvcfManagersTableField.GetValue(comp);

                var outParam = new object[] { pawn, null };

                // Either try getting the VerbManager from the comp, or create it if it's missing
                if ((bool)conditionalWeakTableTryGetValueMethod.Invoke(weakTable, outParam))
                    obj = outParam[1];
                else
                    obj = InitVerbManager(pawn, (WorldComponent)comp, table: weakTable);
            }
        }

        private static void SyncManagedVerb(SyncWorker sync, ref object obj)
        {
            if (sync.isWriting)
            {
                // Get the VerbManager from inside of the ManagedVerb itself
                var verbManager = mvcfManagerVerbManagerField.GetValue(obj);
                // Find the ManagedVerb inside of list of all verbs
                var managedVerbsList = mvcfVerbsField.GetValue(verbManager) as IList;
                var index = managedVerbsList.IndexOf(obj);

                // Sync the index of the verb as well as the manager (if it's valid)
                sync.Write(index);
                if (index >= 0)
                    SyncVerbManager(sync, ref verbManager);
            }
            else
            {
                // Read and check if the index is valid
                var index = sync.Read<int>();

                if (index >= 0)
                {
                    // Read the verb manager
                    object verbManager = null;
                    SyncVerbManager(sync, ref verbManager);

                    // Find the ManagedVerb with specific index inside of list of all verbs
                    var managedVerbsList = mvcfVerbsField.GetValue(verbManager) as IList;
                    obj = managedVerbsList[index];
                }
            }
        }

        private static void SyncVEFAbility(SyncWorker sync, ref ITargetingSource source)
        {
            Log.ResetMessageCount();
            Log.Message("0");
            if (sync.isWriting)
            {
                Log.Message("0.1");
                sync.Write(source.Caster);
                Log.Message(source.CasterPawn.NameFullColored);
                Log.Message((learnedAbilitiesField.GetValue(source.CasterPawn.AllComps.First(c => c.GetType() == learnedAbilitiesField.DeclaringType)) as IEnumerable)?.EnumerableCount().ToString() ?? "-1");
                Log.Message("0.2");
                sync.Write(source.GetVerb.GetUniqueLoadID());
                Log.Message("0.3");
            }
            else
            {
                Log.Message("1.1");
                var caster = sync.Read<Thing>();
                var uid = sync.Read<string>();
                Log.Message("1.2");
                if (caster is ThingWithComps thing)
                {
                    Log.Message("1.3");
                    Log.Message((thing as Pawn).NameFullColored);
                    Log.Message(string.Join(" | ", thing.AllComps.Select(tc => tc.GetType().FullName)));
                    var compAbilities = thing.AllComps.First(c => c.GetType() == learnedAbilitiesField.DeclaringType);
                    Log.Message((compAbilities != null).ToString());
                    Log.Message("1.4");
                    var list = learnedAbilitiesField.GetValue(compAbilities) as IEnumerable;
                    Log.Message("1.5");
                    Log.Message(uid);

                    foreach (object o in list)
                    {
                        ITargetingSource its = o as ITargetingSource;
                        Log.Message(its?.GetVerb?.GetUniqueLoadID() ?? "none");
                        if (its.GetVerb.GetUniqueLoadID() == uid)
                        {
                            source = its;
                            break;
                        }
                    }
                    Log.Message("1.6");
                }
                else
                {
                    Log.Error("MultiplayerCompat :: SyncVEFAbility : Caster isn't a ThingWithComps");
                }
            }
            Log.Message("2");
        }

        private static bool GetManagerForPrefix(Pawn pawn, bool createIfMissing, WorldComponent __instance, ref object __result)
        {
            if (MP.IsInMultiplayer || !createIfMissing) return true; // We don't care and let the method run, we only care if we might need to creat a VerbManager

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
            }

            // Ensure VerbManager is initialized for all players, as it might not be
            SyncedInitVerbManager(pawn);

            return false;
        }

        // Synced method for initializing the verb manager for all players, used in sitations where the moment of creation of the verb might not be synced
        private static void SyncedInitVerbManager(Pawn pawn) => InitVerbManager(pawn);

        private static object InitVerbManager(Pawn pawn, WorldComponent comp = null, object list = null, object table = null)
        {
            if (comp == null) comp = (WorldComponent)mvcfGetWorldCompMethod.Invoke(null, Array.Empty<object>());
            if (comp == null) return null;
            if (table == null) table = mvcfManagersTableField.GetValue(comp);
            var parameters = new object[] { pawn, null };
            object verbManager;

            // Try to find the verb manager first, as it might exist (and it will definitely exist for at least one player)
            if ((bool)conditionalWeakTableTryGetValueMethod.Invoke(table, parameters))
            {
                verbManager = parameters[1];
                // If the manager has the pawn assigned, it means it's initialized, if it's not - we initialize it
                if (mvcfPawnGetter.Invoke(verbManager, Array.Empty<object>()) == null)
                    mvcfInitializeManagerMethod.Invoke(verbManager, new object[] { pawn });
            }
            // If the verb manager doesn't exist, we create an empty one here and add it to the verb manager list and table, and then initialize it
            else
            {
                verbManager = CreateAndAddVerbManagerToCollections(pawn, comp, list, table);
                mvcfInitializeManagerMethod.Invoke(verbManager, new object[] { pawn });
            }

            return verbManager;
        }

        // Helper method for creating an empty verb manager for a pawn
        private static object CreateAndAddVerbManagerToCollections(Pawn pawn, WorldComponent worldComponent, object list = null, object table = null)
        {
            var verbManager = mvcfVerbManagerCtor.Invoke(Array.Empty<object>());

            if (list == null) list = mvcfAllManagersListField.GetValue(worldComponent);
            if (table == null) table = mvcfManagersTableField.GetValue(worldComponent);

            conditionalWeakTableAddMethod.Invoke(table, new object[] { pawn, verbManager });
            ((IList)list).Add(weakReferenceCtor.Invoke(new object[] { verbManager }));

            return verbManager;
        }
    }
}
