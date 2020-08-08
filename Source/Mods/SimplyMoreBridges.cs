using System;
using System.Reflection;

using HarmonyLib;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Simply More Bridges</summary>
    /// <see href="https://github.com/emipa606/SimplyMoreBridges"/>
    [MpCompatFor("Mlie.SimplyMoreBridges")]
    class SimplyMoreBridgesCompat
    {
        static MethodInfo defDatabaseAddMethod;

        public SimplyMoreBridgesCompat(ModContentPack mod)
        {
            Type[] generic = { typeof(BuildableDef) };

            defDatabaseAddMethod = AccessTools.Method(typeof(DefDatabase<>).MakeGenericType(generic), "Add", generic);

            MpCompat.harmony.Patch(
                AccessTools.Method("SimplyMoreBridges.GenerateBridges:GenerateBridgeDef"),
                postfix: new HarmonyMethod(typeof(SimplyMoreBridgesCompat), nameof(GenerateBridgeDefPostfix))
                );
        }

        static void GenerateBridgeDefPostfix(TerrainDef __result)
        {
            defDatabaseAddMethod.Invoke(null, new object[] { __result });
        }
    }
}
