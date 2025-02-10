using System;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Cleaning Area by Hatti</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=870089952"/>
    [MpCompatFor("hatti.cleaningarea")]
    [MpCompatFor("s1.cleaningareatemp")]
    public class CleaningArea
    {
        static Type CleaningAreaMapComponentType;
        
        public CleaningArea(ModContentPack mod)
        {
            Type type = CleaningAreaMapComponentType = AccessTools.TypeByName("CleaningArea.CleaningArea_MapComponent") ??
                                                       AccessTools.TypeByName("CleaningAreaTemp.CleaningArea_MapComponent");

            MP.RegisterSyncMethod(AccessTools.PropertySetter(type, "cleaningArea"));
            MP.RegisterSyncWorker<MapComponent>(SyncWorkerForCleaningArea, type);
        }

        static void SyncWorkerForCleaningArea(SyncWorker sw, ref MapComponent comp)
        {
            if (sw.isWriting) {
                sw.Write(comp.map);
            } else {
                comp = sw.Read<Map>().GetComponent(CleaningAreaMapComponentType);
            }
        }
    }
}
