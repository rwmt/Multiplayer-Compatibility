using System.Collections;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Almost There! by Roolo</summary>
    /// <see href="https://github.com/rheirman/AlmostThere"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2372543327"/>
    [MpCompatFor("roolo.AlmostThere")]
    public class AlmostThere
    {
        // Base class
        private static FastInvokeHandler almostThereInstance;
        private static AccessTools.FieldRef<object, object> extendedDataStorageField;

        // ExtendedDataStorage class
        private static AccessTools.FieldRef<object, IDictionary> storeField;

        public AlmostThere(ModContentPack mod)
        {
            var type = AccessTools.TypeByName("AlmostThere.Harmony.Caravan_GetGizmos");

            MpCompat.RegisterLambdaDelegate(type, "CreateIgnoreRestCommand", 1);
            MpCompat.RegisterLambdaDelegate(type, "CreateAlmostThereCommand", 1);
            MpCompat.RegisterLambdaDelegate(type, "CreateForceRestCommand", 1);

            type = AccessTools.TypeByName("AlmostThere.Base");
            almostThereInstance = MethodInvoker.GetHandler(AccessTools.PropertyGetter(type, "Instance"));
            extendedDataStorageField = AccessTools.FieldRefAccess<object>(type, "_extendedDataStorage");

            type = AccessTools.TypeByName("AlmostThere.Storage.ExtendedCaravanData");
            MP.RegisterSyncWorker<object>(SyncExtendedCaravanData, type);

            storeField = AccessTools.FieldRefAccess<IDictionary>("AlmostThere.Storage.ExtendedDataStorage:_store");
        }

        private static void SyncExtendedCaravanData(SyncWorker sync, ref object caravanData)
        {
            var instance = almostThereInstance(null);
            var dataStorage = extendedDataStorageField(instance);
            var dictionary = storeField(dataStorage);

            if (sync.isWriting)
            {
                var found = false;

                foreach (DictionaryEntry entry in dictionary)
                {
                    if (entry.Value == caravanData)
                    {
                        sync.Write((int)entry.Key);
                        found = true;
                        break;
                    }
                }

                if (!found)
                    sync.Write(int.MinValue);
            }
            else
            {
                var id = sync.Read<int>();
                if (id != int.MinValue)
                    caravanData = dictionary[id];
            }
        }
    }
}