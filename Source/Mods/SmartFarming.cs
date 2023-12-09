using System.Collections;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Smart Farming by Owlchemist</summary>
    /// <see href="https://github.com/Owlchemist/smart-farming"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2619652663"/>
    [MpCompatFor("Owlchemist.SmartFarming")]
    internal class SmartFarming
    {
        private static IDictionary compCache;
        private static AccessTools.FieldRef<object, IDictionary> growZoneRegistryField;

        public SmartFarming(ModContentPack mod)
        {
            var type = AccessTools.TypeByName("SmartFarming.Mod_SmartFarming");
            compCache = AccessTools.StaticFieldRefAccess<IDictionary>(type, "compCache");

            type = AccessTools.TypeByName("SmartFarming.MapComponent_SmartFarming");
            growZoneRegistryField = AccessTools.FieldRefAccess<IDictionary>(type, "growZoneRegistry");

            type = AccessTools.TypeByName("SmartFarming.ZoneData");
            // Toggle: no petty jobs, allow harvest, harvest now, orchard alignment
            MpCompat.RegisterLambdaDelegate(type, "Init", 3, 5, 6, 8);
            MP.RegisterSyncMethod(type, "SwitchSowMode"); // Called from two places
            MP.RegisterSyncMethod(type, "SwitchPriority"); // Called from two places
            MP.RegisterSyncMethod(type, "MergeZones");
            MP.RegisterSyncWorker<object>(SyncZoneData, type);
        }

        private static void SyncZoneData(SyncWorker sync, ref object zoneData)
        {
            if (sync.isWriting)
            {
                int? zoneId = null;
                var comp = compCache[Find.CurrentMap.uniqueID];
                var zoneRegistry = growZoneRegistryField(comp);

                foreach (DictionaryEntry entry in zoneRegistry)
                {
                    if (entry.Value == zoneData)
                    {
                        zoneId = (int)entry.Key;
                        break;
                    }
                }

                sync.Write(zoneId);
                if (zoneId != null)
                    sync.Write(Find.CurrentMap.uniqueID);
            }
            else
            {
                var zoneId = sync.Read<int?>();
                if (zoneId != null)
                {
                    var comp = compCache[sync.Read<int>()];
                    var zoneRegistry = growZoneRegistryField(comp);
                    zoneData = zoneRegistry[zoneId.Value];
                }
            }
        }
    }
}