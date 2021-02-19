using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>VanillaCuisineExpanded-Fishing by juanosarg</summary>
    /// <see href="https://github.com/juanosarg/VanillaCuisineExpanded-Fishing"/>
    /// contribution to Multiplayer Compatibility by Cody Spring
    [MpCompatFor("VanillaExpanded.VCEF")]
    class VanillaFishingExpanded
    {
        private static Type commandType;
        private static FieldInfo mapField;
        private static FieldInfo fishingZoneField;

        public VanillaFishingExpanded(ModContentPack mod)
        {
            // RNG fix
            {
                PatchingUtilities.PatchSystemRandCtor("VCE_Fishing.JobDriver_Fish", false);
                PatchingUtilities.PatchPushPopRand("VCE_Fishing.JobDriver_Fish:SelectFishToCatch");
            }

            // Gizmo (select fish size to catch)
            {
                commandType = AccessTools.TypeByName("VCE_Fishing.Command_SetFishList");
                mapField = AccessTools.Field(commandType, "map");
                fishingZoneField = AccessTools.Field(commandType, "zone");

                MpCompat.RegisterSyncMethodsByIndex(commandType, "<ProcessInput>", Enumerable.Range(0, 3).ToArray());
                MP.RegisterSyncWorker<Command>(SyncFishingZoneChange, commandType, shouldConstruct: false);

                MpCompat.RegisterSyncMethodByIndex(AccessTools.TypeByName("VCE_Fishing.Zone_Fishing"), "<GetGizmos>", 1);
            }
        }

        private static void SyncFishingZoneChange(SyncWorker sync, ref Command command)
        {
            if (sync.isWriting)
            {
                sync.Write(((Map)mapField.GetValue(command)).Index);
                sync.Write((Zone)fishingZoneField.GetValue(command));
            }
            else
            {
                command = (Command)FormatterServices.GetUninitializedObject(commandType);
                mapField.SetValue(command, Find.Maps[sync.Read<int>()]);
                fishingZoneField.SetValue(command, sync.Read<Zone>());
            }
        }
    }
}
