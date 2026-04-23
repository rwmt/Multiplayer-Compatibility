using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Vanilla Furniture Expanded - Factory by Oskar Potocki and dninemfive</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3686924415"/>
    /// <see href="https://github.com/Vanilla-Expanded/VanillaFurnitureExpanded-Factory"/>
    /// contribution to Multiplayer Compatibility by Sakura_TA
    [MpCompatFor("VanillaExpanded.VFEFactory")]
    class VanillaFurnitureExpandedFactory
    {
        /// <summary>
        /// TEXT refer
        /// All gyzmo are not synchronized between players. 
        /// This applies to changing conveyor belt directions, changing any autofarmer settings, 
        /// and the "Allow Taking" and "Allow Inserting" buttons in the Factory hopper, 
        /// as well as changing any process in any mechanism from "Do X Times" to "Do Forever"
        /// (all these actions must be pressed simultaneously by all players while paused to avoid desync). 
        /// The Overclock button on any mechanism does not work in multiplayer. Otherwise, the mod works.
        /// </summary>
        /// <param name="mod"></param>
        public VanillaFurnitureExpandedFactory(ModContentPack mod)
        {
            LongEventHandler.ExecuteWhenFinished(LatePatch);
        }
        static void LatePatch()
        {
            // Gizmo fix
            {
                MpCompat.RegisterLambdaDelegate("VanillaFurnitureExpandedFactory.Building_Conveyor", "GetGizmos", 1, 2, 3, 4);

                MpCompat.RegisterLambdaDelegate("VanillaFurnitureExpandedFactory.Building_UndergroundConveyorBase", "GetGizmos", 2);
                MpCompat.RegisterLambdaMethod("VanillaFurnitureExpandedFactory.Building_UndergroundConveyorBase", "GetGizmos", 4);

                MpCompat.RegisterLambdaMethod("VanillaFurnitureExpandedFactory.Building_Autofarmer", "GetGizmos", 0, 1, 2, 5, 7, 9);
            }
        }
    }
}