using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Modern Faction Menu by Astryl</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3742926690"/>
    [MpCompatFor("astryl.ModernFactionMenu")]
    [MpCompatFor("astryl.modernfactionmenu")]
    internal class ModernFactionMenu
    {
        public ModernFactionMenu(ModContentPack mod) => LongEventHandler.ExecuteWhenFinished(LatePatch);

        private static void LatePatch()
        {
            var windowType = AccessTools.TypeByName("ModernFactionMenu.Window_ModernFactions");
            if (windowType != null)
            {
                MP.RegisterSyncDialogNodeTree(windowType, "OpenComms");
            }

            var compType = AccessTools.TypeByName("ModernFactionMenu.GameComponent_FactionPins");
            if (compType != null)
            {
                MP.RegisterSyncMethod(compType, "TogglePin");
            }
        }
    }
}
