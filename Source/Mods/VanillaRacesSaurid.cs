using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>
    /// Vanilla Races Expanded - Saurid by Oskar Potocki, Neronix17
    /// <see href="https://github.com/Vanilla-Expanded/VanillaRacesExpanded-Saurid"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2880990495"/>
    /// </summary>
    [MpCompatFor("vanillaracesexpanded.saurid")]
    public class VanillaRacesSaurid
    {
        public VanillaRacesSaurid(ModContentPack mod)
        {
            // Dev gizmos - Hatch now, Regenerate child (method groups, not lambdas)
            var type = AccessTools.TypeByName("VRESaurids.Comp_HumanHatcher");
            MP.RegisterSyncMethod(type, "Hatch").SetDebugOnly();
            MP.RegisterSyncMethod(type, "RegenerateChild").SetDebugOnly();
        }
    }
}