﻿using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>VanillaCuisineExpanded-Fishing by juanosarg</summary>
    /// <see href="https://github.com/juanosarg/VanillaCuisineExpanded-Fishing"/>
    /// contribution to Multiplayer Compatibility by Cody Spring
    [MpCompatFor("VanillaExpanded.VCEF")]
    class VanillaFishingExpanded
    {
        public VanillaFishingExpanded(ModContentPack mod)
        {
            MP.RegisterSyncMethod(AccessTools.Method("VCE_Fishing.JobDriver_Fish:SelectFishToCatch"));
        }
    }
}