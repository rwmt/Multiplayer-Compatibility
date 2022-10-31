using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Alpha Animals by Sarg Bjornson</summary>
    /// <see href="https://github.com/juanosarg/AlphaAnimals"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=1541721856"/>
    /// contribution to Multiplayer Compatibility by Reshiram and Sokyran
    [MpCompatFor("sarg.alphaanimals")]
    class AlphaBehavioursAndEvents
    {
        public AlphaBehavioursAndEvents(ModContentPack mod)
        {
            //RNG Fix
            {
                var rngFixConstructors = new[]
                {
                    "AlphaBehavioursAndEvents.DeathActionWorker_ExplodeAndSpawnEggs",
                    "AlphaBehavioursAndEvents.Hediff_Crushing",

                    // Ocular plant conversion
                    "AlphaBehavioursAndEvents.CompAbilityOcularConversion",
                    "AlphaBehavioursAndEvents.Gas_Ocular",
                };

                PatchingUtilities.PatchSystemRandCtor(rngFixConstructors, false);

                var rngFixMethods = new[] //System.Random fixes
                {
                    "AlphaBehavioursAndEvents.Hediff_Crushing:RandomFilthGenerator",

                    // Ocular plant conversion
                    "AlphaBehavioursAndEvents.CompAbilityOcularConversion:Apply",
                    "AlphaBehavioursAndEvents.Gas_Ocular:Tick",
                };
                PatchingUtilities.PatchPushPopRand(rngFixMethods);

                var fixSystemRngMethods = new[]
                {
                    "AlphaBehavioursAndEvents.Ability_SpawnOnRadius:Cast",
                    "AlphaBehavioursAndEvents.CompCreateOcularPlants:CompTick",
                    "AlphaBehavioursAndEvents.CompCreateOcularPlants:ConvertRandomPlantInRadius",
                };
                PatchingUtilities.PatchSystemRand(fixSystemRngMethods, false);
            }
        }
    }
}