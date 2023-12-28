using System;
using System.Linq;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>One bed to sleep with all by Densevoid</summary>
    /// <see href="https://github.com/densevoid/one-bed-to-sleep-with-all"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2572109781"/>
    [MpCompatFor("densevoid.onebedtosleepwithall")]
    internal class OneBedToSleepWithAll
    {
        private static Type compType;

        public OneBedToSleepWithAll(ModContentPack mod)
        {
            compType = AccessTools.TypeByName("OneBedToSleepWithAll.CompPolygamyMode");
            MpCompat.RegisterLambdaMethod(compType, "CompGetGizmosExtra", 1);
            // Comp has a null props.compClass, most likely it's dynamically created
            MP.RegisterSyncWorker<ThingComp>(SyncCompPolygamyMode, compType);
        }

        private static void SyncCompPolygamyMode(SyncWorker sync, ref ThingComp comp)
        {
            if (sync.isWriting) sync.Write(comp.parent);
            else comp = sync.Read<ThingWithComps>().AllComps.FirstOrDefault(x => x.GetType() == compType);
        }
    }
}
