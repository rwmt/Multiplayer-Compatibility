using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Vanilla Landmarks Expanded by Oskar Potocki, Taranchuk, Kentington, Sarg Bjornson</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3656316229"/>
    /// <see href="https://github.com/Vanilla-Expanded/VanillaLandmarksExpanded"/>
    [MpCompatFor("VanillaExpanded.VExplorationE")]
    class VanillaLandmarksExpanded
    {
        public VanillaLandmarksExpanded(ModContentPack mod)
        {
            // Vent building sprayers call ThrowAirPuffUp during Tick, which checks
            // ShouldSpawnMotesAt (camera-dependent) before consuming RNG for mote
            // position/velocity. If clients have different camera positions, RNG diverges.
            PatchingUtilities.PatchPushPopRand("VanillaExplorationExpanded.DeadlifeSprayer:ThrowAirPuffUp");
            PatchingUtilities.PatchPushPopRand("VanillaExplorationExpanded.ToxicSprayer:ThrowAirPuffUp");
            PatchingUtilities.PatchPushPopRand("VanillaExplorationExpanded.RotstinkSprayer:ThrowAirPuffUp");
            PatchingUtilities.PatchPushPopRand("VanillaExplorationExpanded.SmokeSprayer:ThrowAirPuffUp");
        }
    }
}
