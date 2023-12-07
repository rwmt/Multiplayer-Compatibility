using HarmonyLib;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Vanilla Races Expanded - Lycanthrope by Oskar Potocki, Sarg Bjornson</summary>
    /// TODO: Add links to mod workshop/github
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