using System.Linq;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Biotech Mech Stuff Elongated - Mechanoid Upgrades V2 by MrKociak</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2926928394"/>
    [MpCompatFor("MrKociak.BiotechMoreMechStuffMechUpgradePod")]
    public class BiotechMechStuffElongatedMechanoidUpgrades
    {
        public BiotechMechStuffElongatedMechanoidUpgrades(ModContentPack mod)
        {
            LongEventHandler.ExecuteWhenFinished(LatePatch);

            // Toggle: auto repeat (1), pick same mechanoid (3), pick random mechanoid (5), advanced filters (7)
            // Toggle (dis)allow filters: (light/heavy) mechs (9), (medium/ultra heavy) mechs (11), melee mechs (13), ranged mechs (15), worker mechs (17)
            // Ligh/medium and heavy/ultra heavy gizmos depend on the type of the building.
            MpCompat.RegisterLambdaMethod("RimWorld.CompRepeatCycle_BMU", "CompGetGizmosExtra", 1, 3, 5, 7, 9, 11, 13, 15, 17);
            // Change into another building (single building has multiple of those)
            MpCompat.RegisterLambdaMethod("RimWorld.CompSwapBuilding_BMU", "CompGetGizmosExtra", 0);
        }

        private static void LatePatch()
        {
            // Start procedure (2), cancel load/procedure, forget selected mech (6, 7), dev: enable/disable ingredients (0), complete (8)
            MpCompat.RegisterLambdaMethod("Building_MechUpgrader", "GetGizmos", 2, 6, 0, 8).TakeLast(2).SetDebugOnly();
        }
    }
}