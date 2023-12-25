using Verse;

namespace Multiplayer.Compat
{
    /// <summary>VPE Skipdoor Pathing by V337</summary>
    /// <remarks>You should probably use Better VPE Skipdoor Pathing instead, as it has less issues: <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3011764218"/></remarks>
    /// <see href="https://github.com/Veritas-99/VPE-Skipdoor-Pathing"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2995157602"/>
    [MpCompatFor("v337.VPESkipdoorPathing")]
    public class VPESkipdoorPathing
    {
        public VPESkipdoorPathing(ModContentPack mod)
        {
            // Toggle use while drafted gizmo
            MpCompat.RegisterLambdaMethod("VPESkipdoorPathing.CompSkipdoorPathing", nameof(ThingComp.CompGetGizmosExtra), 1);
        }
    }
}