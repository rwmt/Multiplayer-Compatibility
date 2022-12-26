using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Vanilla Social Interactions Expanded by Oskar Potocki, Taranchuk</summary>
    /// <see href="https://github.com/Vanilla-Expanded/VanillaSocialInteractionsExpanded"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2439736083"/>
    [MpCompatFor("VanillaExpanded.VanillaSocialInteractionsExpanded")]
    public class VanillaSocialInteractions
    {
        public VanillaSocialInteractions(ModContentPack mod) =>
            PatchingUtilities.PatchPushPopRand("VanillaSocialInteractionsExpanded.SocialInteractionsManager:GameComponentTick");
    }
}