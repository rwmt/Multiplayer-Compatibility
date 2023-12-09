using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Better ground-penetrating scanner by Kikohi</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2809972387"/>
    [MpCompatFor("kikohi.BetterGroundPenetratingScanner")]
    public class BetterGroundPenetratingScanner
    {
        public BetterGroundPenetratingScanner(ModContentPack mod)
        {
            // Select random outcome (2) or specific one (3)
            MpCompat.RegisterLambdaDelegate("BGPScanner.CompBetterDeepScanner", "PostSpawnSetup", 2, 3).SetContext(SyncContext.MapSelected);
        }
    }
}