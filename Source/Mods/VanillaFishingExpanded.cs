using System;
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
                PatchingUtilities.PatchSystemRand(AccessTools.Method("VCE_Fishing.JobDriver_Fish:SelectFishToCatch"));
            }

            // Gizmo (select fish size to catch)
            {
                commandType = AccessTools.TypeByName("VCE_Fishing.Command_SetFishList");
                mapField = AccessTools.Field(commandType, "map");
                fishingZoneField = AccessTools.Field(commandType, "zone");

                MP.RegisterSyncMethod(commandType, "<ProcessInput>b__4_0");
                MP.RegisterSyncMethod(commandType, "<ProcessInput>b__4_1");
                MP.RegisterSyncMethod(commandType, "<ProcessInput>b__4_2");

                MP.RegisterSyncWorker<Command>(SyncFishingZoneChange, commandType, shouldConstruct: false);
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
