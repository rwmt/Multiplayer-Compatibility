using System;
using Harmony;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Giddy Up! Battle Mounts</summary>
    /// <remarks>Suspect raids desync here</remarks>
    /// <see href="https://github.com/rheirman/battlemounts"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=1217001091"/>
    [MpCompatFor("Giddy-up! Battle Mounts")]
    public class GiddyUpBattleMountsCompat
    {
        public GiddyUpBattleMountsCompat(ModContentPack mod)
        {
            MP.RegisterSyncMethod(
                AccessTools.Method("Battlemounts.Utilities.EnemyMountUtility:mountAnimals")
            );

            Type type;
            type = AccessTools.TypeByName("BattleMounts.Harmony.Jobdriver_Cleanup");
            type = AccessTools.Inner(type, "JobDriver_Cleanup");
            MpCompat.harmony.Patch(AccessTools.Method(type, "Prefix"),
                prefix: new HarmonyMethod(typeof(GiddyUpBattleMountsCompat), nameof(StubItOut)));
        }

        static bool StubItOut => false;
    }
}
