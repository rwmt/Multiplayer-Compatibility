using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Vanilla Cooking Expanded by Oskar Potocki, Sarg Bjornson, Chowder</summary>
    /// <see href="https://github.com/juanosarg/ItemProcessor"/>
    /// <see href="https://github.com/juanosarg/VanillaCookingExpanded"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2134308519"/>
    [MpCompatFor("VanillaExpanded.VCookE")]
    class VanillaCookingExpanded
    {
        private static FieldInfo itemProcessorField;

        public VanillaCookingExpanded(ModContentPack mod)
        {
            var type = AccessTools.TypeByName("ItemProcessor.Building_ItemProcessor");
            // _1 and _5 are used to check if gizmo should be enabled, so we don't sync them
            MpCompat.RegisterSyncMethodsByIndex(type, "<GetGizmos>", 0, 2, 3, 4, 6, 7);

            type = AccessTools.TypeByName("ItemProcessor.Command_SetQualityList");
            itemProcessorField = AccessTools.Field(type, "building");
            MP.RegisterSyncWorker<Command>(SyncSetTargetQuality, type, shouldConstruct: true);
            MpCompat.RegisterSyncMethodsByIndex(type, "<ProcessInput>", Enumerable.Range(0, 7).ToArray());

            // Keep an eye on this in the future, seems like something the devs could combine into a single class at some point
            foreach (var ingredientNumber in new[] { "First", "Second", "Third" })
            {
                type = AccessTools.TypeByName($"ItemProcessor.Command_Set{ingredientNumber}ItemList");
                MP.RegisterSyncWorker<Command>(SyncSetIngredientCommand, type, shouldConstruct: true);
                MP.RegisterSyncMethod(type, $"TryInsert{ingredientNumber}Thing");
                MpCompat.RegisterSyncMethodsByIndex(type, "<ProcessInput>", 0);
            }

            // AddHediff desyncs with Arbiter, but seems fine without it
            PatchingUtilities.PatchPushPopRand("VanillaCookingExpanded.Thought_Hediff:MoodOffset");
        }

        private static void SyncSetTargetQuality(SyncWorker sync, ref Command command)
        {
            if (sync.isWriting)
                sync.Write(itemProcessorField.GetValue(command) as Thing);
            else
                itemProcessorField.SetValue(command, sync.Read<Thing>());
        }

        private static void SyncSetIngredientCommand(SyncWorker sync, ref Command command)
        {
            var traverse = Traverse.Create(command);
            var building = traverse.Field("building");
            var ingredientList = traverse.Field("things");

            if (sync.isWriting)
            {
                sync.Write(building.GetValue() as Thing);
                var list = ingredientList.GetValue() as List<Thing>;
                sync.Write(list.Count);
                foreach (var item in list)
                    sync.Write(item as Thing);
            }
            else
            {
                building.SetValue(sync.Read<Thing>());
                int count = sync.Read<int>();
                var list = new List<Thing>(count);
                for (int i = 0; i < count; i++)
                    list.Add(sync.Read<Thing>());
                ingredientList.SetValue(list);
            }
        }
    }
}
