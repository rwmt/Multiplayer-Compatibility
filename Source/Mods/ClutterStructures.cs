using HarmonyLib;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Clutter Structures by mrofa and neronix17</summary>
    /// <see href="https://github.com/neronix17/-O21-Clutter-Structure"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2008681009"/>
    [MpCompatFor("neronix17.clutterstructures")]
    class ClutterStructures
    {
        public ClutterStructures(ModContentPack mod)
        {
            MpCompat.harmony.Patch(
                AccessTools.Method("Clutter_StructureWall.StructureDefGenerator:StuffGeneratrs"),
                postfix: new HarmonyMethod(typeof(ClutterStructures), nameof(GetClutterStructureDefPostfix))
                );
        }

        static void GetClutterStructureDefPostfix(ThingDef Wallzie)
        {
            DefDatabase<BuildableDef>.Add(Wallzie);
        }
    }
}