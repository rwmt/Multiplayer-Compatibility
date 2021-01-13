using System;
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

        public VanillaExpandedFramework(ModContentPack mod)
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

            // AddHediff desyncs with Arbiter, but seems fine without it
            PatchingUtilities.PatchPushPopRand("VanillaCookingExpanded.Thought_Hediff:MoodOffset");

            type = AccessTools.TypeByName("VFECore.CompPawnDependsOn");
            MpCompat.RegisterSyncMethodByIndex(type, "<CompGetGizmosExtra>", 0).SetDebugOnly();

            type = AccessTools.TypeByName("VanillaFurnitureExpanded.CompConfigurableSpawner");
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
    }
}
