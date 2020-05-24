using System;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Fertile Fields by Jamaican Castle</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2012735237"/>
    [MpCompatFor("jamaicancastle.RF.fertilefields")]
    class FertileFieldsCompat
    {
        readonly Type GrowZoneManagerType;

        public FertileFieldsCompat(ModContentPack mod)
        {
            Type type = GrowZoneManagerType = AccessTools.TypeByName("RFF_Code.GrowZoneManager");

            MP.RegisterSyncMethod(type, "ToggleReturnToSoil");
            MP.RegisterSyncMethod(type, "ToggleDesignateReplacements");

            MP.RegisterSyncWorker<MapComponent>(SyncWorkerForGrowZoneManager, type);
        }

        void SyncWorkerForGrowZoneManager(SyncWorker sync, ref MapComponent obj)
        {
            if (sync.isWriting) {
                sync.Write(obj.map);
            } else {
                var map = sync.Read<Map>();

                obj = map.GetComponent(GrowZoneManagerType);
            }
        }
    }
}
