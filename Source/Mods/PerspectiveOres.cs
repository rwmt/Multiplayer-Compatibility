using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Perspective: Ores by Owlchemist</summary>
    /// <see href="https://github.com/Owlchemist/perspective-ores"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2923871543"/>
    [MpCompatFor("Owlchemist.PerspectiveOres")]
    public class PerspectiveOres
    {
        public PerspectiveOres(ModContentPack mod)
        {
            PatchingUtilities.PatchLongEventMarkers();

            // Stop the "ProcessMap" from running when it could break stuff.
            MpCompat.harmony.Patch(AccessTools.DeclaredMethod("PerspectiveOres.PerspectiveOresSetup:ProcessMap"),
                prefix: new HarmonyMethod(typeof(PerspectiveOres), nameof(StopSecondHostCall)));
        }

        private static bool StopSecondHostCall(bool reset)
        {
            // Stop the host from running it for the second time. It messes stuff up due to the place it's called from.
            if (!PatchingUtilities.AllowedToRunLongEvents)
                return false;
            // Prevent it from being called due to settings change, could mess stuff up.
            if (reset && MP.IsInMultiplayer)
                return false;
            return true;
        }
    }
}