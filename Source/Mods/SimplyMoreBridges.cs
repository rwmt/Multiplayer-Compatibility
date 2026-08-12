using System;
using System.Reflection;

using HarmonyLib;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Simply More Bridges by Lanilor</summary>
    /// <see href="https://github.com/emipa606/SimplyMoreBridges"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3560458000"/>
    [MpCompatFor("Mlie.SimplyMoreBridges")]
    class SimplyMoreBridgesCompat
    {
        public SimplyMoreBridgesCompat(ModContentPack mod)
        {
            MpCompat.harmony.Patch(
                AccessTools.Method("SimplyMoreBridges.GenerateBridges:generateBridgeDef"),
                postfix: new HarmonyMethod(typeof(SimplyMoreBridgesCompat), nameof(GenerateBridgeDefPostfix))
                );
        }

        static void GenerateBridgeDefPostfix(TerrainDef __result)
        {
            DefDatabase<BuildableDef>.Add(__result);
        }
    }
}
