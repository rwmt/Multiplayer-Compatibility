using System;
using HarmonyLib;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Dubs Mint Menus by Dubwise</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=1446523594"/>
    [MpCompatFor("Dubwise.DubsMintMenus")]
    internal class DubsMintMenu
    {
        public DubsMintMenu(ModContentPack mod) => LongEventHandler.ExecuteWhenFinished(() => Patch("DubsMintMenus.MP_Util"));

        public static void Patch(string name)
        {
            var modType = AccessTools.TypeByName(name);

            var mpSyncMethod = AccessTools.TypeByName("Multiplayer.Client.SyncMethod");
            AccessTools.Field(modType, "RegisterRef").SetValue(null, AccessTools.Method(mpSyncMethod, "Register"));

            var mpSyncUtil = AccessTools.TypeByName("Multiplayer.Client.SyncFieldUtil");
            AccessTools.Field(modType, "FieldWatchPrefixRef").SetValue(null, AccessTools.Method(mpSyncUtil, "FieldWatchPrefix"));
            AccessTools.Field(modType, "FieldWatchPostfixRef").SetValue(null, AccessTools.Method(mpSyncUtil, "FieldWatchPostfix"));

            var mpSyncField = AccessTools.TypeByName("Multiplayer.Client.SyncField");
            AccessTools.Field(modType, "WatchRef").SetValue(null, AccessTools.Method(mpSyncField, "Watch"));
            AccessTools.Field(modType, "SetBufferChangesRef").SetValue(null, AccessTools.Method(mpSyncField, "SetBufferChanges"));

            var mpSync = AccessTools.TypeByName("Multiplayer.Client.Sync");
            AccessTools.Field(modType, "FieldRef").SetValue(null, AccessTools.Method(mpSync, "Field", new[] { typeof(Type), typeof(string) }));

            AccessTools.Method(modType, "asshai").Invoke(null, Array.Empty<object>());
        }
    }
}
