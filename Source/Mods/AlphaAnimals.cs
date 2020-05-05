using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
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
                    AccessTools.Method("AlphaBehavioursAndEvents.CompGasProducer:CompTick"),
                    AccessTools.Method("AlphaBehavioursAndEvents.CompAnimalProduct:InformGathered"),
                    AccessTools.Method("AlphaBehavioursAndEvents.CompInitialHediff:CompTickRare"),
                    AccessTools.Method("AlphaBehavioursAndEvents.Gas_Ocular:Tick"),
                    AccessTools.Method("AlphaBehavioursAndEvents.Hediff_Crushing:RandomFilthGenerator")
                };
                foreach (var method in rngFixMethods)
                    PatchingUtilities.PatchSystemRand(method);
            }
        }
    }
}