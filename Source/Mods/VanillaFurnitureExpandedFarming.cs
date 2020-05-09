using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Vanilla Furniture Expanded by Oskar Potocki and dninemfive</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=1957158779"/>
    /// contribution to Multiplayer Compatibility by Cody Spring
    [MpCompatFor("VanillaExpanded.VFEFarming")]
    class VFEF
    {
        public VFEF(ModContentPack mod)
        {
            //RNG Fix
            {
                var methods = new[] {
                    "VFEF.MoteSprinkler:NewMote",
                    "VFEF.MoteSprinkler:ThrowWaterSpray",
                };

                PatchingUtilities.PatchPushPopRand(methods);
            }
        }
    }
}