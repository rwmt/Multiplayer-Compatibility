using HarmonyLib;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Vanilla Races Expanded - Lycanthrope by Oskar Potocki, Sarg Bjornson</summary>
    /// <see href="https://github.com/Vanilla-Expanded/VanillaRacesExpanded-Lycanthrope"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3114453100"/>
    [MpCompatFor("vanillaracesexpanded.lycanthrope")]
    public class VanillaRacesExpandedLycanthrope
    {
        public VanillaRacesExpandedLycanthrope(ModContentPack mod)
        {
             AccessTools.DeclaredField("VanillaRacesExpandedLycanthrope.VanillaRacesExpandedLycanthrope_InteractionWorker_Interacted_Patch:random")
                 .SetValue(null, PatchingUtilities.RandRedirector.Instance);
        }
    }
}