using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Vanilla Brewing Expanded by Oskar Potocki, Sarg Bjornson, Chowder</summary>
    /// <see href="https://steamcommunity.com/workshop/filedetails/?id=2186560858"/>
    /// <see href="https://github.com/Vanilla-Expanded/VanillaBrewingExpanded"/>
    [MpCompatFor("VanillaExpanded.VBrewE")]
    class VanillaBrewingExpanded
    {
        public VanillaBrewingExpanded(ModContentPack mod)
        {
            PatchingUtilities.PatchSystemRand("VanillaBrewingExpanded.Plant_AutoProduce:TickLong");
            PatchingUtilities.PatchSystemRandCtor("VanillaBrewingExpanded.Hediff_ConsumedCocktail");
        }
    }
}
