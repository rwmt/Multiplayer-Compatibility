using Harmony;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Giddy Up! Battle Mounts</summary>
    /// <remarks>Suspect raids desync here</remarks>
    /// <see href="https://github.com/rheirman/GiddyUpRideAndRoll"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=1331961995"/>
    [MpCompatFor("Giddy-up! Battle Mounts")]
    public class GiddyUpBattleMounts
    {
        public GiddyUpBattleMounts(ModContentPack mod)
        {
            MP.RegisterSyncMethod(
                AccessTools.Method("Battlemounts.Utilities.EnemyMountUtility:mountAnimals")
            );
        }
    }
}
