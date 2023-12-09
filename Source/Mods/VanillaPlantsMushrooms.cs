using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Vanilla Plants Expanded - Mushrooms by Taranchuk, Sarg Bjornson, Oskar Potocki</summary>
    /// <see href="https://github.com/Vanilla-Expanded/VanillaPlantsExpanded-Mushrooms"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3006389281"/>
    [MpCompatFor("VanillaExpanded.VPlantsEMushrooms")]
    public class VanillaPlantsMushrooms
    {
        public VanillaPlantsMushrooms(ModContentPack mod)
        {
            // Toggle allow cut (1), allow sow (3)
            MpCompat.RegisterLambdaMethod("VanillaPlantsExpandedMushrooms.Zone_GrowingMushroom", "GetGizmos", 1, 3);
        }
    }
}