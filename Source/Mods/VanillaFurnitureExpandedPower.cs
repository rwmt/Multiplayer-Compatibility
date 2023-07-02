using System;
using HarmonyLib;
using Multiplayer.API;
using UnityEngine;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Vanilla Furniture Expanded - Power by Oskar Potocki and Sarg Bjornson</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2062943477"/>
    /// <see href="https://github.com/Vanilla-Expanded/VanillaFurnitureExpanded-Power"/>
    /// Contribution to Multiplayer Compatibility by Sokyran and Reshiram
    [MpCompatFor("VanillaExpanded.VFEPower")]
    class VanillaPowerExpanded
    {
        public VanillaPowerExpanded(ModContentPack mod)
        {
            // Violence generator
            var type = AccessTools.TypeByName("VanillaPowerExpanded.CompSoulsPowerPlant");
            MpCompat.RegisterLambdaMethod(type, "CompGetGizmosExtra", 1); // Toggle on/off
        }
    }
}