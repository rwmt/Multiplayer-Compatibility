using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Smart Speed by Sarg</summary>
    /// <see href="https://github.com/juanosarg/Smart-Speed"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=1504723424"/>
    [MpCompatFor("sarg.smartspeed")]
    internal class SmartSpeed
    {
        public SmartSpeed(ModContentPack mod) 
            => LongEventHandler.ExecuteWhenFinished(LatePatch);

        private static void LatePatch()
        {
            var type = AccessTools.TypeByName("Multiplayer.Client.AsyncTime.TimeControlsMarker");
            var drawingTimeControlsPrefix = AccessTools.Method(type, "Prefix");
            var drawingTimeControlsPostfix = AccessTools.Method(type, "Postfix");

            type = AccessTools.TypeByName("Multiplayer.Client.AsyncTime.TimeControlPatch");
            var doTimeControlPrefix = AccessTools.Method(type, "Prefix");
            var doTimeControlPostfix = AccessTools.Method(type, "Postfix");

            type = AccessTools.TypeByName("SmartSpeed.Detouring.TimeControls");
            var method = AccessTools.Method(type, "DoTimeControlsGUI");
            MpCompat.harmony.Patch(method,
                prefix: new HarmonyMethod(drawingTimeControlsPrefix),
                postfix: new HarmonyMethod(drawingTimeControlsPostfix));
            MpCompat.harmony.Patch(method,
                prefix: new HarmonyMethod(doTimeControlPrefix),
                postfix: new HarmonyMethod(doTimeControlPostfix));

            // Cancel the menu for changing speed multiplier in events,
            // as we're not supporting it (at least for now)
            MpCompat.harmony.Patch(AccessTools.Method(type, "DoConfigGUI"),
                prefix: new HarmonyMethod(typeof(SmartSpeed), nameof(CancelInMp)));
        }

        private static bool CancelInMp() => !MP.IsInMultiplayer;
    }
}
