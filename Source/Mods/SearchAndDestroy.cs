using System.Collections;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Search and Destroy updated mod by MemeGoddess</summary>
    /// <see href="https://github.com/MemeGoddess/SearchAndDestroy"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3232242247"/>
    [MpCompatFor("MemeGoddess.SearchAndDestroy")]
    public class SearchAndDestroy
    {
        // Fields for accessing private data structures
        private static FastInvokeHandler searchAndDestroyInstance;
        private static FastInvokeHandler extendedDataStorageGetter;
        private static AccessTools.FieldRef<object, IDictionary> storeField;

        public SearchAndDestroy(ModContentPack mod)
        {
            // Register the gizmo creation methods to synchronize their actions across clients
            MpCompat.RegisterLambdaDelegate(
                "SearchAndDestroy.Harmony.Pawn_DraftController_GetGizmos",
                "CreateGizmo_SearchAndDestroy_Melee",
                1
            );
            MpCompat.RegisterLambdaDelegate(
                "SearchAndDestroy.Harmony.Pawn_DraftController_GetGizmos",
                "CreateGizmo_SearchAndDestroy_Ranged",
                1
            );

            // Register a SyncWorker for ExtendedPawnData to synchronize custom data
            var extendedPawnDataType = AccessTools.TypeByName("SearchAndDestroy.Storage.ExtendedPawnData");
            MP.RegisterSyncWorker<object>(SyncExtendedPawnData, extendedPawnDataType);

            // Initialize reflection accessors for private fields and properties
            var baseType = AccessTools.TypeByName("SearchAndDestroy.Base");
            searchAndDestroyInstance = MethodInvoker.GetHandler(AccessTools.PropertyGetter(baseType, "Instance"));
            extendedDataStorageGetter = MethodInvoker.GetHandler(AccessTools.PropertyGetter(baseType, "ExtendedDataStorage"));

            storeField = AccessTools.FieldRefAccess<IDictionary>(
                "SearchAndDestroy.Storage.ExtendedDataStorage:_store"
            );
        }

        // SyncWorker method to synchronize ExtendedPawnData across clients
        public static void SyncExtendedPawnData(SyncWorker sync, ref object pawnData)
        {
            var instance = searchAndDestroyInstance(null);
            var dataStorage = extendedDataStorageGetter(instance);
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
