using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>
    /// Vanilla Races Expanded - Phytokin by Oskar Potocki, Sarg Bjornson, Allie, Erin, Sir Van, Reann Shepard
    /// <see href="https://github.com/Vanilla-Expanded/VanillaRacesExpanded-Phytokin"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2927323805"/>
    /// </summary>
    [MpCompatFor("vanillaracesexpanded.phytokin")]
    public class VanillaRacesPhytokin
    {
        public VanillaRacesPhytokin(ModContentPack mod)
        {
            // Dev mode gizmos
            // Reset dryad counter (in case of counter breaking)
            MpCompat.RegisterLambdaDelegate("VanillaRacesExpandedPhytokin.CompDryadCounter", "CompGetGizmosExtra", 0).SetDebugOnly();

            var type = AccessTools.TypeByName("VanillaRacesExpandedPhytokin.CompVariablePollutionPump");
            // Force pump now
            MP.RegisterSyncMethod(type, "Pump").SetDebugOnly(); // Also called while ticking
            // Set next pump time
            MpCompat.RegisterLambdaDelegate(type, "CompGetGizmosExtra", 1).SetDebugOnly();
            // Fix the mod using Find.CurrentMap instead of parent.Map
            // Can be safely removed it the following PR is accepted: https://github.com/Vanilla-Expanded/VanillaRacesExpanded-Phytokin/pull/2
            PatchingUtilities.ReplaceCurrentMapUsage(AccessTools.DeclaredMethod(type, "CompTick"));
        }
    }
}