using System.Collections.Generic;
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
        private static AccessTools.FieldRef<HashSet<Pawn>> pawnsToRiseField;

        public AlphaGenes(ModContentPack mod)
        {
            LongEventHandler.ExecuteWhenFinished(LatePatch);

            // Abilities
            {
                // Toggle off (0) or on (1)
                MpCompat.RegisterLambdaMethod("AlphaGenes.Ability_MineralOverdrive", "GetGizmos", 0, 2);
                // Toggle off (0) or on (1)
                MpCompat.RegisterLambdaMethod("AlphaGenes.Ability_ReactiveArmour", "GetGizmos", 0, 2);
                // Dev: reset scorpion counter to 0 (if it bugs out)
                MpCompat.RegisterLambdaDelegate("AlphaGenes.CompScorpionCounter", "CompGetGizmosExtra", 0).SetDebugOnly();
                // Dev: force mutation
                MpCompat.RegisterLambdaMethod("AlphaGenes.HediffComp_RandomMutation", "CompGetGizmos", 0).SetDebugOnly();
            }

            // Randomizer gene
            {
                MpCompat.harmony.Patch(AccessTools.DeclaredMethod("AlphaGenes.Gene_Randomizer:PostAdd"),
                    prefix: new HarmonyMethod(typeof(AlphaGenes), nameof(PrePostAddRandomizerGene)));
            }
        }

        private static void LatePatch()
        {
            // Clear cache
            {
                // List of pawns to rise as shamblers.
                // The collection, if not cleared, will
                // cause bugs in the game. Remove if
                // fixed in the mod itself (and make
                // sure it's safe to remove as well).
                var field = AccessTools.DeclaredField("AlphaGenes.StaticCollectionsClass:pawnsToRise");
                pawnsToRiseField = AccessTools.StaticFieldRefAccess<HashSet<Pawn>>(field);
                MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(GameComponentUtility), nameof(GameComponentUtility.FinalizeInit)),
                    postfix: new HarmonyMethod(ClearCache));
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

        private static void ClearCache() => pawnsToRiseField().Clear();
    }
}
