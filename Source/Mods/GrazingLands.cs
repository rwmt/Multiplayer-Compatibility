using HarmonyLib;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Grazing Lands by avilmask</summary>
    /// <see href="https://github.com/catgirlfighter/RimWorld_GrazingLands"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=1770268130"/>
    [MpCompatFor("avilmask.GrazingLands")]
    public class GrazingLands
    {
        public GrazingLands(ModContentPack mod)
        {
            var type = AccessTools.TypeByName("GrazingLands.PlantPropertiesPatch");
            type = AccessTools.Inner(type, "Plant_IngestedCalculateAmounts_GrazingLandsPatch");
            PatchingUtilities.PatchSystemRand(AccessTools.Method(type, "Prefix"), false);
        }
    }
}