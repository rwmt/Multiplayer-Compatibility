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
            // Hatch now (0), regenerate child (1)
            MpCompat.RegisterLambdaMethod("VRESaurids.Comp_HumanHatcher", "CompGetGizmosExtra", 0, 1).SetDebugOnly();
        }
    }
}