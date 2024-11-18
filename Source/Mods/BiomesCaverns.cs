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
                // Create drill pod blueprint
                MpCompat.RegisterLambdaDelegate("BiomesCaverns.Patches.Building_PodLauncher_GetGizmos_Patch", "DrillPodGizmo", 0);
                // (Dev) set progress to 100%
                MpCompat.RegisterLambdaMethod("Building_MushroomFermentingBarrel", nameof(Building.GetGizmos), 0).SetDebugOnly();
            }

            // RNG + GenView.ShouldSpawnMotesAt/mote saturation check
            {
                PatchingUtilities.PatchPushPopRand("Caveworld_Flora_Unleashed.FruitingBody_Gleamcap:ThrowPoisonSmoke");
            }
        }
    }
}