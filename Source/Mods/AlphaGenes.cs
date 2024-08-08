using HarmonyLib;
using Multiplayer.API;
using RimWorld;
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
                // The following method is seeded, so it should be fine
                // If not, then patching it as well should fix it
                //"AlphaGenes.GameComponent_RandomMood:GameComponentTick",
            }

            // Abilities
            {
                MpCompat.RegisterLambdaMethod("AlphaGenes.Ability_MineralOverdrive", "GetGizmos", 0, 2);
                MpCompat.RegisterLambdaMethod("AlphaGenes.Ability_ReactiveArmour", "GetGizmos", 0, 2);
                MpCompat.RegisterLambdaDelegate("AlphaGenes.CompScorpionCounter", "CompGetGizmosExtra", 0).SetDebugOnly();
            }

            // Randomizer gene
            {
                MpCompat.harmony.Patch(AccessTools.DeclaredMethod("AlphaGenes.Gene_Randomizer:PostAdd"),
                    prefix: new HarmonyMethod(typeof(AlphaGenes), nameof(PrePostAddRandomizerGene)));
            }
        }

        private static bool PrePostAddRandomizerGene(Gene __instance)
        {
            if (!MP.IsInMultiplayer)
                return true;

            // Restore the pawn to baseliner if they had the randomizer gene.
            // Alpha Genes does this before applying random xenotype loaded from player's files.
            var pawn = __instance.pawn;
            pawn.genes.SetXenotype(XenotypeDefOf.Baseliner);
            pawn.genes.RemoveGene(__instance);

            // If we're to patch it so the xenotype is random from player's files, it would make it
            // easily possible if sync methods could be synced from gameplay code (like sync fields).

            return false;
        }
    }
}
