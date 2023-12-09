using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Biomes! Caverns by The Biomes Mod Team</summary>
    /// <see href="https://github.com/biomes-team/BiomesCaverns"/>
    /// <see href="https://steamcommunity.com/workshop/filedetails/?id=2969748433"/>
    [MpCompatFor("BiomesTeam.BiomesCaverns")]
    public class BiomesCaverns
    {
        public BiomesCaverns(ModContentPack mod)
        {
            // Gizmos
            {
                MpCompat.RegisterLambdaMethod("Building_MushroomFermentingBarrel", nameof(Building.GetGizmos), 0).SetDebugOnly();
                // Unused/commented out
                MpCompat.RegisterLambdaMethod("BMT.CompThingDefReplacer", nameof(ThingComp.CompGetGizmosExtra), 0).SetDebugOnly();
            }

            // RNG + GenView.ShouldSpawnMotesAt
            {
                PatchingUtilities.PatchPushPopRand("Caveworld_Flora_Unleashed.FruitingBody_Gleamcap:ThrowPoisonSmoke");
            }
        }
    }
}