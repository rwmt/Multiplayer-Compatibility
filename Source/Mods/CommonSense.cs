using System.Reflection;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Common Sense by avil</summary>
    /// <see href="https://github.com/catgirlfighter/RimWorld_CommonSense"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=1561769193"/>
    [MpCompatFor("avilmask.CommonSense")]
    class CommonSense
    {
        private static ISyncField shouldUnloadSyncField;
        private static FieldInfo manualUnloadEnabledField;
        private static MethodInfo getCompUnlockerCheckerMethod;

        public CommonSense(ModContentPack mod)
        {
            manualUnloadEnabledField = AccessTools.Field(AccessTools.TypeByName("CommonSense.Settings"), "gui_manual_unload");
            var type = AccessTools.TypeByName("CommonSense.CompUnloadChecker");
            MP.RegisterSyncWorker<ThingComp>(SyncComp, type);
            shouldUnloadSyncField = MP.RegisterSyncField(AccessTools.Field(type, "ShouldUnload"));
            getCompUnlockerCheckerMethod = AccessTools.Method(type, "GetChecker");

            MpCompat.harmony.Patch(AccessTools.Method("RimWorld.ITab_Pawn_Gear:DrawThingRow"),
                prefix: new HarmonyMethod(typeof(CommonSense), nameof(CommonSensePatchPrefix)),
                postfix: new HarmonyMethod(typeof(CommonSense), nameof(CommonSensePatchPostix)));
        }

        private static void CommonSensePatchPrefix(Thing thing)
        {
            if (MP.IsInMultiplayer && (bool)manualUnloadEnabledField.GetValue(null))
            {
                MP.WatchBegin();
                var comp = getCompUnlockerCheckerMethod.Invoke(null, new object[] { thing, false, false });
                shouldUnloadSyncField.Watch(comp);
            }
        }

        private static void CommonSensePatchPostix()
        {
            if (MP.IsInMultiplayer && (bool)manualUnloadEnabledField.GetValue(null))
                MP.WatchEnd();
        }

        private static void SyncComp(SyncWorker sync, ref ThingComp thing)
        {
            if (sync.isWriting)
                sync.Write<Thing>(thing.parent);
            else
                thing = (ThingComp)getCompUnlockerCheckerMethod.Invoke(null, new object[] { sync.Read<Thing>(), false, false });
        }
    }
}
