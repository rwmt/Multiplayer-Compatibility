using HarmonyLib;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Dubs Bad Hygiene by Dubwise</summary>
    /// <see href="https://github.com/Dubwise56/Dubs-Bad-Hygiene"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=836308268"/>
    [MpCompatFor("Dubwise.DubsBadHygiene")]
    public class BadHygiene
    {
        public BadHygiene(ModContentPack mod)
        {
            var type = AccessTools.TypeByName("DubsBadHygiene.Comp_SaunaHeater");
            var methods = new[]
            {
                AccessTools.Method(type, "SteamyNow"),
                AccessTools.Method(type, "CompTick"),
            };

            PatchingUtilities.PatchPushPopRand(methods);
        }
    }
}