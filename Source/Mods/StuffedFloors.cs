using System;
using System.Reflection;

using HarmonyLib;
using Verse;

namespace Multiplayer.Compat
{

    /// <summary>Stuffed Floors by Fluffy</summary>
    /// <see href="https://github.com/fluffy-mods/StuffedFloors"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=853043503"/>
    [MpCompatFor("fluffy.stuffedfloors")]
    class StuffedFloorsCompat
    {
        public StuffedFloorsCompat(ModContentPack mod)
        {
            MpCompat.harmony.Patch(
                AccessTools.Method("StuffedFloors.FloorTypeDef:GetStuffedTerrainDef"),
                postfix: new HarmonyMethod(typeof(StuffedFloorsCompat), nameof(GetStuffedTerrainDefPosfix))
                );
        }

        static void GetStuffedTerrainDefPosfix(TerrainDef __result)
        {
            DefDatabase<BuildableDef>.Add(__result);
        }
    }
}
