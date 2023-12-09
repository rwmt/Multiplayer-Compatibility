using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Vanilla Plants Expanded - More Plants by Sarg Bjornson, Oskar Potocki</summary>
    /// <see href="https://github.com/Vanilla-Expanded/VanillaPlantsExpanded-MorePlants"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2748889667"/>
    [MpCompatFor("VanillaExpanded.VPlantsEMore")]
    internal class VanillaPlantsMorePlants
    {
        public VanillaPlantsMorePlants(ModContentPack mod)
        {
            PatchingUtilities.PatchSystemRand("VanillaPlantsExpandedMorePlants.Plant_SowsAdjacent:SpawnSetup", false);
            PatchingUtilities.PatchSystemRand("VanillaPlantsExpandedMorePlants.Plant_TransformOnMaturity:TickLong", false);

            MpCompat.RegisterLambdaMethod("VanillaPlantsExpandedMorePlants.Zone_GrowingAquatic", "GetGizmos", 1, 3);
            MpCompat.RegisterLambdaMethod("VanillaPlantsExpandedMorePlants.Zone_GrowingSandy", "GetGizmos", 1, 3);
        }
    }
}
