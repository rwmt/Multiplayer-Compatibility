using HarmonyLib;
using Verse;

namespace Multiplayer.Compat
{
    [MpCompatFor("VanillaExpanded.VWEL")]
    public class VanillaWeaponsLaser
    {
        public VanillaWeaponsLaser(ModContentPack mod)
        {
            var type = AccessTools.TypeByName("VanillaWeaponsExpandedLaser.CompLaserCapacitor");
            MpCompat.RegisterSyncMethodByIndex(type, "<CompGetGizmosExtra>", 1);
        }
    }
}
