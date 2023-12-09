using System.Collections;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Search and Destroy by Roolo</summary>
    /// <see href="https://github.com/rheirman/SearchAndDestroy"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=1467764609"/>
    [MpCompatFor("roolo.SearchAndDestroy")]
    public class SearchAndDestroy
    {
        // Base class
        private static FastInvokeHandler searchAndDestroyInstance;
        private static AccessTools.FieldRef<object, object> extendedDataStorageField;

        // ExtendedDataStorage class
        private static AccessTools.FieldRef<object, IDictionary> storeField;

        public SearchAndDestroy(ModContentPack mod)
        {
            var type = AccessTools.TypeByName("SearchAndDestroy.Harmony.Pawn_DraftController_GetGizmos");

            MpCompat.RegisterLambdaDelegate(type, "CreateGizmo_SearchAndDestroy_Melee", 1);
            MpCompat.RegisterLambdaDelegate(type, "CreateGizmo_SearchAndDestroy_Ranged", 1);

            type = AccessTools.TypeByName("SearchAndDestroy.Base");
            searchAndDestroyInstance = MethodInvoker.GetHandler(AccessTools.PropertyGetter(type, "Instance"));
            extendedDataStorageField = AccessTools.FieldRefAccess<object>(type, "_extendedDataStorage");

            type = AccessTools.TypeByName("SearchAndDestroy.Storage.ExtendedPawnData");
            MP.RegisterSyncWorker<object>(SyncExtendedPawnData, type);

            storeField = AccessTools.FieldRefAccess<IDictionary>("SearchAndDestroy.Storage.ExtendedDataStorage:_store");
        }

        public static void SyncExtendedPawnData(SyncWorker sync, ref object pawnData)
        {
            var instance = searchAndDestroyInstance(null);
            var dataStorage = extendedDataStorageField(instance);
            var dictionary = storeField(dataStorage);

            if (sync.isWriting)
            {
                var found = false;

                foreach (DictionaryEntry entry in dictionary)
                {
                    if (entry.Value == pawnData)
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
                    pawnData = dictionary[id];
            }
        }
    }
}