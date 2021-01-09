using HarmonyLib;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Vanilla Weapons Expanded - Laser by Oskar Potocki, Kikohi, Primus the Conqueror, Ogliss, AUTOMATIC, Jecrell</summary>
    /// <see href="https://github.com/AndroidQuazar/VanillaWeaponsExpanded-Laser"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=1989352844"/>
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
