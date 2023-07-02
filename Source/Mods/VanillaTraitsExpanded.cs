using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Vanilla Traits Expanded by Oskar Potocki, Taranchuk, Chowder</summary>
    /// <see href="https://github.com/Vanilla-Expanded/VanillaTraitsExpanded"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2296404655"/>
    [MpCompatFor("VanillaExpanded.VanillaTraitsExpanded")]
    public class VanillaTraitsExpanded
    {
        public VanillaTraitsExpanded(ModContentPack mod) =>
            PatchingUtilities.PatchPushPopRand("VanillaTraitsExpanded.TraitsManager:GameComponentTick");
    }
}