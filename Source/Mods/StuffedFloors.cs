using System;
using System.Reflection;

using HarmonyLib;
using Verse;

namespace Multiplayer.Compat
{

    /// <summary>Stuffed Floors by Fluffy</summary>
    /// <see href="https://github.com/FluffierThanThou/StuffedFloors"/>
    [MpCompatFor("fluffy.stuffedfloors")]
    class StuffedFloorsCompat
    {
        internal static MethodInfo defDatabaseAddMethod;

        public StuffedFloorsCompat(ModContentPack mod)
        {
            Init();

            MpCompat.harmony.Patch(
                AccessTools.Method("StuffedFloors.FloorTypeDef:GetStuffedTerrainDef"),
                postfix: new HarmonyMethod(typeof(StuffedFloorsCompat), nameof(GetStuffedTerrainDefPosfix))
                );
        }

        internal static void Init()
        {
            if (defDatabaseAddMethod == null)
            {
                Type[] generic = { typeof(BuildableDef) };

                defDatabaseAddMethod = AccessTools.Method(typeof(DefDatabase<>).MakeGenericType(generic), "Add", generic);
            }
        }

        static void GetStuffedTerrainDefPosfix(TerrainDef __result)
        {
            defDatabaseAddMethod.Invoke(null, new object[] { __result });
        }
    }
}
