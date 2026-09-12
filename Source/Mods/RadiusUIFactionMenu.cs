using System;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Radius UI - Faction Menu by Astryl</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3786945713"/>
    [MpCompatFor("astryl.RadiusUI.FactionMenu")]
    internal class RadiusUIFactionMenu
    {
        public RadiusUIFactionMenu(ModContentPack mod) => LongEventHandler.ExecuteWhenFinished(LatePatch);

        private static void LatePatch()
        {
            // Register DialogNodeTree sync for comms console opened from Faction Menu
            var uiType = AccessTools.TypeByName("RadiusUI.FactionMenu.UI.Ui");
            if (uiType != null)
            {
                MP.RegisterSyncDialogNodeTree(uiType, "OpenComms");
            }

            // Register synced closing and pause locking for CommsDialog
            var commsDialogType = AccessTools.TypeByName("RadiusUI.FactionMenu.UI.Dialogs.CommsDialog");
            if (commsDialogType != null)
            {
                DialogUtilities.RegisterDialogCloseSync(commsDialogType, addPauseLock: true);
            }
        }
    }
}
