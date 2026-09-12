using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Modern Quest Menu by Astryl</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3742906203"/>
    [MpCompatFor("astryl.ModernQuestMenu")]
    [MpCompatFor("astryl.modernquestmenu")]
    internal class ModernQuestMenu
    {
        public ModernQuestMenu(ModContentPack mod) => LongEventHandler.ExecuteWhenFinished(LatePatch);

        private static void LatePatch()
        {
            var compType = AccessTools.TypeByName("ModernQuestMenu.ModernQuestMenuGameComp");
            if (compType != null)
            {
                MP.RegisterSyncMethod(compType, "TogglePin");
            }
        }
    }
}
