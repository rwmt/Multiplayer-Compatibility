using Harmony;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Giddy Up! Ride and Roll by Roolo</summary>
    /// <see href="https://github.com/rheirman/GiddyUpRideAndRoll"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=1331961995"/>
    [MpCompatFor("Giddy-up! Ride and Roll")]
    public class GiddyUpRideAndRollCompat
    {
        public GiddyUpRideAndRollCompat(ModContentPack mod)
        {
            // Stop waiting
            MP.RegisterSyncDelegate(
                AccessTools.TypeByName("GiddyUpRideAndRoll.Harmony.Pawn_GetGizmos"),
                "<>c__DisplayClass1_0",
                "<CreateGizmo_LeaveRider>b__0"
            );

            MP.RegisterSyncMethod(
                AccessTools.Method("GiddyUpRideAndRoll.Harmony.CaravanEnterMapUtility_Enter:MountCaravanMounts")
            );
        }
    }
}
