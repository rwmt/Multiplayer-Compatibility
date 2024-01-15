using System;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Biomes! Core by The Biomes Mod Team</summary>
    /// <see href="https://github.com/biomes-team/BiomesCore"/>
    /// <see href="https://steamcommunity.com/workshop/filedetails/?id=2038000893"/>
    [MpCompatFor("BiomesTeam.BiomesCore")]
    public class BiomesCore
    {
        public BiomesCore(ModContentPack mod)
        {
            // Gizmos (only dev mode gizmos)
            {
                var typeNames = new[]
                {
                    "BiomesCore.CompHarvestAnimalProduct",

                    "BiomesCore.ThingComponents.CompAnimalsAroundThing",
                    "BiomesCore.ThingComponents.CompAnimalThingSpawner",
                };

                foreach (var type in typeNames)
                    MpCompat.RegisterLambdaMethod(type, nameof(ThingComp.CompGetGizmosExtra), 0).SetDebugOnly();

                var methodNames = new[]
                {
                    "BiomesCore.CompCleanFilthAround:ClearFilth",
                    "BiomesCore.CompPlantPolluteOverTime:Pollute",
                    "BiomesCore.CompPlantProximityExplosive:Detonate",
                };

                foreach (var method in methodNames)
                    MP.RegisterSyncMethod(AccessTools.DeclaredMethod(method)).SetDebugOnly();
            }

            // Current map usage
            {
                PatchingUtilities.ReplaceCurrentMapUsage("BiomesCore.GameCondition_Earthquake:GameConditionTick");
                PatchingUtilities.ReplaceCurrentMapUsage("BiomesCore.IncidentWorker_Earthquake:TryExecuteWorker");
            }

            // RNG + GenView.ShouldSpawnMotesAt
            {
                PatchingUtilities.PatchSystemRand(new[]
                {
                    // Two 'new Random()' calls, one is never used
                    "BiomesCore.GenSteps.ValleyPatch:Postfix",
                }, false);

                PatchingUtilities.PatchPushPopRand(new[]
                {
                    "BiomesCore.CompPlantReleaseSpore:ThrowPoisonSmoke",
                    "BiomesCore.TerrainComp_MoteSpawner:ThrowMote",
                });
            }
        }
    }
}