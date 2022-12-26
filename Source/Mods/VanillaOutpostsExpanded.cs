using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Vanilla Outposts Expanded by legodude17, Oskar Potocki, Chowder, Kikohi</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2688941031"/>
    /// <see href="https://github.com/Vanilla-Expanded/VanillaOutpostsExpanded"/>
    [MpCompatFor("vanillaexpanded.outposts")]
    internal class VanillaOutpostsExpanded
    {
        public VanillaOutpostsExpanded(ModContentPack mod) => LongEventHandler.ExecuteWhenFinished(LatePatch);

        public void LatePatch()
        {
            MP.RegisterSyncMethod(AccessTools.TypeByName("VOE.Outpost_Artillery"), "Fire");
            MpCompat.RegisterLambdaDelegate("VOE.Outpost_Defensive", "GetGizmos", 3);
        }
    }
}
