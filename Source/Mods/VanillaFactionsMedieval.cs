using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Vanilla Factions Expanded - Medieval by Oskar Potocki, XeoNovaDan</summary>
    /// <see href="https://github.com/Vanilla-Expanded/VanillaFactionsExpanded-Medieval"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2023513450"/>
    [MpCompatFor("OskarPotocki.VanillaFactionsExpanded.MedievalModule")]
    public class VanillaFactionsMedieval
    {
        // Archery target uses RNG - but only when the player is looking at it. Which may not be the case in MP for all players.
        public VanillaFactionsMedieval(ModContentPack mod)
            => PatchingUtilities.PatchPushPopRand("VFEMedieval.JobDriver_PlayArchery:ThrowObjectAt");
    }
}