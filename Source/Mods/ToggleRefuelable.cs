using System;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Toggle Refuelable by likeafox</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=1570308352"/>
    [MpCompatFor("Toggle Refuelable")]
    public class ToggleRefuelable
    {
        public ToggleRefuelable(ModContentPack mod)
        {
            MP.RegisterSyncDelegate(
                AccessTools.TypeByName("ToggleRefuelable.CompRefuelable_CompGetGizmosExtra_Patch"),
                "<>c__DisplayClass0_0",
                "<Postfix>b__1"
                );
        }
    }
}
