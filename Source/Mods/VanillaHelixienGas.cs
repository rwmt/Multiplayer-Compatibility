using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Vanilla Helixien Gas Expanded by Oskar Potocki, Kikohi</summary>
    /// <see href="https://github.com/Vanilla-Expanded/VanillaHelixienGasExpanded"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2877699803"/>
    [MpCompatFor("VanillaExpanded.HelixienGas")]
    public class VanillaHelixienGasExpanded
    {
        public VanillaHelixienGasExpanded(ModContentPack mod)
        {
            // Fill gizmo
            MpCompat.RegisterLambdaMethod("VHelixienGasE.Building_GasGeyser", "GetGizmos", 0);
            // GenView.ShouldSpawnMotesAt call with RNG calls, need to push/pop
            PatchingUtilities.PatchPushPopRand("VHelixienGasE.IntermittentGasSprayer:ThrowGasPuffUp");
        }
    }
}