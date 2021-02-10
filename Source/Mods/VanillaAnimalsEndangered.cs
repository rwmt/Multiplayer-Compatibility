using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Vanilla Animals Expanded — Endangered by Oskar Potocki, Sarg Bjornson, Erin, Taranchuk</summary>
    /// <see href="https://github.com/juanosarg/VanillaAnimalsExpanded-EndangeredAndRecentlyExtinct"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2366589898"/>
    [MpCompatFor("VanillaExpanded.VAEEndAndExt")]
    class VanillaAnimalsEndangered
    {
        public VanillaAnimalsEndangered(ModContentPack mod)
        {
            var type = AccessTools.TypeByName("VanillaAnimalsExpandedEndangered.Pawn_GetGizmos_Patch");

            MP.RegisterSyncDelegate(type, "<>c__DisplayClass1_0", "<Postfix>b__1");
        }
    }
}
