using HarmonyLib;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Alpha Genes by Allie, Sarg</summary>
    /// <see href="https://github.com/juanosarg/AlphaGenes"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2891845502"/>
    [MpCompatFor("sarg.alphagenes")]
    internal class AlphaGenes
    {
        public AlphaGenes(ModContentPack mod)
        {
            // RNG
            {
                PatchingUtilities.PatchSystemRandCtor("AlphaGenes.CompAbilityOcularConversion", false);
                PatchingUtilities.PatchSystemRand("AlphaGenes.CompInsanityBlast:Apply", false);
                PatchingUtilities.PatchSystemRand("AlphaGenes.HediffComp_Parasites:Hatch", false);
            }

            // Abilities
            {
                MpCompat.RegisterLambdaMethod("AlphaGenes.Ability_MineralOverdrive", "GetGizmos", 0, 2);
                MpCompat.RegisterLambdaMethod("AlphaGenes.Ability_ReactiveArmour", "GetGizmos", 0, 2);
                MpCompat.RegisterLambdaDelegate("AlphaGenes.CompScorpionCounter", "CompGetGizmosExtra", 0).SetDebugOnly();
            }

            // Current map usage
            {
                PatchingUtilities.ReplaceCurrentMapUsage("AlphaGenes.HediffComp_DeleteAfterTime:CompPostTick");
                // Debug stuff, may get changed or removed.
                PatchingUtilities.ReplaceCurrentMapUsage("AlphaGenes.CompRandomItemSpawner:CompTick");
            }
        }
    }
}
