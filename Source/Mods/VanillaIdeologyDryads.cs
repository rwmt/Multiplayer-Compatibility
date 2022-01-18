using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using Multiplayer.API;
using Multiplayer.Compat;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Vanilla Ideology Expanded - Dryads by Oskar Potocki, Sarg Bjornson, Taranchuk, Reann Shepard</summary>
    /// <see href="https://github.com/juanosarg/VanillaIdeologyExpanded-Dryads"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2720631512"/>
    [MpCompatFor("VanillaExpanded.Ideo.Dryads")]
    internal class VanillaIdeologyDryads
    {
        public VanillaIdeologyDryads(ModContentPack mod)
        {
            // RNG
            PatchingUtilities.PatchSystemRandCtor("VanillaIdeologyExpanded_Dryads.HediffComp_PeriodicWounds");

            // Gizmos
            MP.RegisterSyncMethod(AccessTools.TypeByName("VanillaIdeologyExpanded_Dryads.CompPawnMerge"), "SetDryadAwakenPod");
            MpCompat.RegisterLambdaMethod("VanillaIdeologyExpanded_Dryads.CompSpawnAwakened", "CompGetGizmosExtra", 0).SetDebugOnly();
        }
    }
}
