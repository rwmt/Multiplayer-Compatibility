using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Alpha Animals by Sarg Bjornson</summary>
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
                    "AlphaBehavioursAndEvents.CompAnimalProduct",
                    "AlphaBehavioursAndEvents.CompExploder",
                    "AlphaBehavioursAndEvents.CompGasProducer",
                    "AlphaBehavioursAndEvents.CompInitialHediff",
                    "AlphaBehavioursAndEvents.Gas_Ocular",
                    "AlphaBehavioursAndEvents.Hediff_Crushing",
                    "AlphaBehavioursAndEvents.DeathActionWorker_ExplodeAndSpawnEggs",

                    //"NewAlphaAnimalSubproducts.CompAnimalProduct ", // System.Random initialized, but not used
                };

                PatchingUtilities.PatchSystemRandCtor(rngFixConstructors, false);

                var rngFixMethods = new[] //System.Random fixes
                {
                    "AlphaBehavioursAndEvents.CompGasProducer:CompTick",
                    "AlphaBehavioursAndEvents.CompAnimalProduct:InformGathered",
                    "AlphaBehavioursAndEvents.CompInitialHediff:CompTickRare",
                    "AlphaBehavioursAndEvents.Gas_Ocular:Tick",
                    "AlphaBehavioursAndEvents.Hediff_Crushing:RandomFilthGenerator",
                    "AlphaBehavioursAndEvents.CompExploder:wickInitializer",
                };
                PatchingUtilities.PatchPushPopRand(rngFixMethods);

                PatchingUtilities.PatchSystemRand("AlphaBehavioursAndEvents.DamageWorker_ExtraInfecter:ApplySpecialEffectsToPart");
            }
        }
    }
}