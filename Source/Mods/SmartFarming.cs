using System.Collections;
using System.Reflection;
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
        private static FieldInfo growZoneRegistryField;

        public SmartFarming(ModContentPack mod)
        {
            var type = AccessTools.TypeByName("SmartFarming.Mod_SmartFarming");
            compCache = AccessTools.StaticFieldRefAccess<IDictionary>(type, "compCache");

            type = AccessTools.TypeByName("SmartFarming.MapComponent_SmartFarming");
            growZoneRegistryField = AccessTools.Field(type, "growZoneRegistry");

            type = AccessTools.TypeByName("SmartFarming.ZoneData");
            MpCompat.RegisterLambdaDelegate(type, "Init", 3, 4).SetContext(SyncContext.CurrentMap); // Toggle no petty jobs, force harvest now
            MP.RegisterSyncMethod(type, "SwitchSowMode"); // Called from two places
            MP.RegisterSyncMethod(type, "SwitchPriority"); // Called from two places
            MP.RegisterSyncWorker<object>(SyncZoneData, type);
        }

        private static void SyncZoneData(SyncWorker sync, ref object zoneData)
        {
            if (sync.isWriting)
            {
                int? id = null;
                var comp = compCache[Find.CurrentMap];
                var zoneRegistry = (IDictionary)growZoneRegistryField.GetValue(comp);

                foreach (DictionaryEntry entry in zoneRegistry)
                {
                    if (entry.Value == zoneData)
                    {
                        id = (int)entry.Key;
                        break;
                    }
                }

                sync.Write(id);
            }
            else
            {
                var id = sync.Read<int?>();
                if (id != null)
                {
                    var comp = compCache[Find.CurrentMap];
                    var zoneRegistry = (IDictionary)growZoneRegistryField.GetValue(comp);
                    zoneData = zoneRegistry[id.Value];
                }
            }
        }
    }
}
