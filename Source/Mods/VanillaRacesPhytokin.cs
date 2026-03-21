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
            // Plant saplingchild gizmo (Command_Target on pregnant phytokin hediff)
            MP.RegisterSyncMethod(AccessTools.DeclaredMethod("VanillaRacesExpandedPhytokin.HediffComp_Saplingchild:TryCreateSaplingChild"));

            // Dev mode gizmos
            var type = AccessTools.TypeByName("VanillaRacesExpandedPhytokin.CompVariablePollutionPump");
            // Force pump now
            MP.RegisterSyncMethod(type, "Pump").SetDebugOnly(); // Also called while ticking
            // Set next pump time
            MpCompat.RegisterLambdaDelegate(type, "CompGetGizmosExtra", 1).SetDebugOnly();
        }
    }
}