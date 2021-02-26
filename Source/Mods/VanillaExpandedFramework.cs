using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Multiplayer.API;
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
        private static FieldInfo setStoneBuildingField;

        // MVCF
        private static MethodInfo mvcfGetWorldCompMethod;
        private static FieldInfo mvcfVerbsField;
        private static FieldInfo mvcfAllManagersField;

        // System
        private static MethodInfo weakReferenceTryGetMethod;

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
                mvcfAllManagersField = AccessTools.Field(type, "allManagers");

                type = AccessTools.TypeByName("MVCF.VerbManager");
                MP.RegisterSyncWorker<object>(SyncVerbManager, type, isImplicit: true);
                mvcfVerbsField = AccessTools.Field(type, "verbs");

                var weakReferenceType = typeof(System.WeakReference<>).MakeGenericType(new[] { type });
                weakReferenceTryGetMethod = AccessTools.Method(weakReferenceType, "TryGetTarget");

                type = AccessTools.TypeByName("MVCF.ManagedVerb");
                MP.RegisterSyncWorker<object>(SyncManagedVerb, type, isImplicit: true);
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
            var allManagers = mvcfAllManagersField.GetValue(comp) as IList;

            if (sync.isWriting)
            {
                var foundManager = false;

                for (int managerIndex = 0; managerIndex < allManagers.Count; managerIndex++)
                {
                    var weakReference = allManagers[managerIndex];
                    var outParam = new object[] { null };
                    if ((bool)weakReferenceTryGetMethod.Invoke(weakReference, outParam) && outParam[0] == obj)
                    {
                        sync.Write(managerIndex);
                        foundManager = true;
                        break;
                    }
                }

                if (!foundManager) sync.Write(-1);
            }
            else
            {
                var managerIndex = sync.Read<int>();

                if (managerIndex >= 0)
                {
                    var weakReference = allManagers[managerIndex];
                    var outParam = new object[] { null };
                    if ((bool)weakReferenceTryGetMethod.Invoke(weakReference, outParam))
                        obj = outParam[0];
                }
            }
        }

        private static void SyncManagedVerb(SyncWorker sync, ref object obj)
        {
            var comp = mvcfGetWorldCompMethod.Invoke(null, Array.Empty<object>());
            var allManagers = mvcfAllManagersField.GetValue(comp) as IList;

            if (sync.isWriting)
            {
                var foundVerb = false;

                for (int managerIndex = 0; managerIndex < allManagers.Count; managerIndex++)
                {
                    var weakReference = allManagers[managerIndex];
                    var outParam = new object[] { null };
                    if ((bool)weakReferenceTryGetMethod.Invoke(weakReference, outParam))
                    {
                        var managedVerbs = mvcfVerbsField.GetValue(outParam[0]) as IList;

                        for (int verbIndex = 0; verbIndex < managedVerbs.Count; verbIndex++)
                        {
                            if (managedVerbs[verbIndex] == obj)
                            {
                                sync.Write(managerIndex);
                                sync.Write(verbIndex);
                                foundVerb = true;
                            }
                        }
                    }
                }

                if (!foundVerb) sync.Write(-1);
            }
            else
            {
                var managerIndex = sync.Read<int>();
                if (managerIndex >= 0)
                {
                    var verbIndex = sync.Read<int>();

                    var weakReference = allManagers[managerIndex];
                    var outParam = new object[] { null };
                    if ((bool)weakReferenceTryGetMethod.Invoke(weakReference, outParam))
                    {
                        var managedVerbs = mvcfVerbsField.GetValue(outParam[0]) as IList;
                        obj = managedVerbs[verbIndex];
                    }
                }
            }
        }
    }
}
