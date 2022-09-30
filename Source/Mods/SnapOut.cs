using HarmonyLib;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Snap Out! by Weilbyte</summary>
    /// <see href="https://github.com/Weilbyte/SnapOut"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=1319782555"/>
    [MpCompatFor("weilbyte.snapout")]
    public class SnapOut
    {
        public SnapOut(ModContentPack mod)
        {
            var type = AccessTools.TypeByName("SnapOut.JobDriver_GoToSafety");
            type = AccessTools.FirstInner(type, _ => true); // There should be only 1 inner class anyway
            PatchingUtilities.PatchUnityRand(AccessTools.Method(type, "MoveNext"), false);
        }
    }
}