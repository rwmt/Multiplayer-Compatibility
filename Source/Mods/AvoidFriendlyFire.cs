using System;
using System.Collections;

using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{

    /// <summary>Avoid Friendly Fire by Falconne</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=1134165362"/>
    /// <see href="https://github.com/Falconne/AvoidFriendlyFire"/>
    [MpCompatFor("falconne.AFF")]
    public class AvoidFriendlyFire
    {
        static IDictionary extendedPawnDataDictionary;

        public AvoidFriendlyFire(ModContentPack mod)
        {
            {
                var type = AccessTools.TypeByName("AvoidFriendlyFire.ExtendedPawnData");
                MP.RegisterSyncWorker<object>(SyncWorkerFor, type);
            }
            {
                MpCompat.RegisterLambdaDelegate("AvoidFriendlyFire.Pawn_DraftController_GetGizmos_Patch", "Postfix", 1);
            }
        }

        static IDictionary ExtendedPawnDataDictionary {
            get {
                if (extendedPawnDataDictionary == null) {
                    Type type = AccessTools.TypeByName("AvoidFriendlyFire.ExtendedDataStorage");

                    var comp = Find.World.GetComponent(type);

                    extendedPawnDataDictionary = AccessTools.Field(type, "_store").GetValue(comp) as IDictionary;
                }

                return extendedPawnDataDictionary;
            }
        }

        static int GetIdFromExtendedPawnData(object extendedPawnData) {
            foreach(object key in ExtendedPawnDataDictionary.Keys)
            {
                if (ExtendedPawnDataDictionary[key] == extendedPawnData) {
                    return (int) key;
                }
            }

            return 0;
        }

        static void SyncWorkerFor(SyncWorker sw, ref object extendedPawnData)
        {
            if (sw.isWriting) {
                sw.Write(GetIdFromExtendedPawnData(extendedPawnData));
            } else {
                int id = sw.Read<int>();

                extendedPawnData = ExtendedPawnDataDictionary[id];
            }
        }
    }
}
