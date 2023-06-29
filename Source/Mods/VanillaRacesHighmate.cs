using System.Linq;
using RimWorld;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Vanilla Races Expanded - Highmate by Oskar Potocki, Taranchuk, Sarg Bjornson</summary>
    /// <see href="https://github.com/Vanilla-Expanded/VanillaRacesExpanded-Highmate"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2995385834"/>
    [MpCompatFor("vanillaracesexpanded.highmate")]
    public class VanillaRacesHighmate
    {
        public VanillaRacesHighmate(ModContentPack mod)
        {
            // Select pregnancy approach (replaces vanilla one if one of the pawns is highmate/lowmate)
            {
                MpSyncWorkers.Requires<SocialCardUtility.CachedSocialTabEntry>();

                MpCompat.RegisterLambdaDelegate(
                    "VanillaRacesExpandedHighmate.SocialCardUtility_DrawPregnancyApproach_Patch", 
                    "AddPregnancyApproachOption",
                    Enumerable.Range(0, 4).ToArray()); // Options 0 through 3
            }
        }
    }
}