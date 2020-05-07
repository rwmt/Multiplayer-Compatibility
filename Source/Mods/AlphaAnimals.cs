using HarmonyLib;
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
                var rngFixMethods = new[] { //System.Random fixes
                    "AlphaBehavioursAndEvents.CompGasProducer:CompTick",
                    "AlphaBehavioursAndEvents.CompAnimalProduct:InformGathered",
                    "AlphaBehavioursAndEvents.CompInitialHediff:CompTickRare",
                    "AlphaBehavioursAndEvents.Gas_Ocular:Tick",
                    "AlphaBehavioursAndEvents.Hediff_Crushing:RandomFilthGenerator",
                };
                PatchingUtilities.PatchSystemRand(rngFixMethods);
            }
        }
    }
}