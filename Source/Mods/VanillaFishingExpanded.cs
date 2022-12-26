using System;
using System.Linq;
using System.Runtime.Serialization;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Vanilla Fishing Expanded by Oskar Potocki, Sarg Bjornson</summary>
    /// <see href="https://github.com/Vanilla-Expanded/VanillaFishingExpanded"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=1914064942"/>
    /// contribution to Multiplayer Compatibility by Cody Spring
    [MpCompatFor("VanillaExpanded.VCEF")]
    class VanillaFishingExpanded
    {
        private static Type commandType;
        private static AccessTools.FieldRef<object, Map> mapField;
        private static AccessTools.FieldRef<object, Zone> fishingZoneField;

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
                mapField = AccessTools.FieldRefAccess<Map>(commandType, "map");
                fishingZoneField = AccessTools.FieldRefAccess<Zone>(commandType, "zone");

                MpCompat.RegisterLambdaMethod(commandType, "ProcessInput", Enumerable.Range(0, 3).ToArray());
                MP.RegisterSyncWorker<Command>(SyncFishingZoneChange, commandType, shouldConstruct: false);

                MpCompat.RegisterLambdaMethod(AccessTools.TypeByName("VCE_Fishing.Zone_Fishing"), "GetGizmos", 1);
            }
        }

        private static void SyncFishingZoneChange(SyncWorker sync, ref Command command)
        {
            if (sync.isWriting)
            {
                sync.Write(mapField(command));
                sync.Write(fishingZoneField(command));
            }
            else
            {
                command = (Command)FormatterServices.GetUninitializedObject(commandType);
                mapField(command) = sync.Read<Map>();
                fishingZoneField(command) = sync.Read<Zone>();
            }
        }
    }
}
