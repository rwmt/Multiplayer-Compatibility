using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>RimFridge by KiameV, maintained by "Just Harry"</summary>
    /// <remarks>Fixes for gizmos</remarks>
    /// <see href="https://github.com/just-harry/rimworld-rimfridge-now-with-shelves"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2898411376"/>
    [MpCompatFor("rimfridge.kv.rw")]
    public class RimFridgeCompat
    {
        public RimFridgeCompat(ModContentPack mod)
        {
            // Several Gizmos
            {
                var type = AccessTools.TypeByName("RimFridge.CompRefrigerator");
                // Offset temperature by x degrees
                MP.RegisterSyncMethod(type, "InterfaceChangeTargetTemperature");
                // Reset to default
                MpCompat.RegisterLambdaDelegate(type, "CompGetGizmosExtra", 2);

                // Toggle darklight
                MpCompat.RegisterLambdaMethod("RimFridge.CompToggleGlower", "CompGetGizmosExtra", 0);
            }

            // Current map usage
            // Not needed for the fork made by "Not Harry" (at the moment, the only 1.5 fork), but other forks may need this.
            {
                PatchingUtilities.ReplaceCurrentMapUsage("RimFridge.Patch_ReachabilityUtility_CanReach:Prefix", false, false);
            }
        }
    }
}
