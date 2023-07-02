using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Alpha Mechs by Sarg Bjornson</summary>
    /// <see href="https://github.com/juanosarg/AlphaMechs"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2973169158"/>
    [MpCompatFor("sarg.alphamechs")]
    public class AlphaMechs
    {
        public AlphaMechs(ModContentPack mod)
        {
            // Fix the mod using Find.CurrentMap instead of parent.Map - in both cases it creates new lord job on current (instead of parent) map
            // Change mech to a vanilla one if the mod mechanoid is disabled
            PatchingUtilities.ReplaceCurrentMapUsage("AlphaMechs.CompChangeDef:CompTick");
            // Hediff runs out and mech turns back hostile
            PatchingUtilities.ReplaceCurrentMapUsage("AlphaMechs.HediffComp_DeleteAfterTime:CompPostTick");

            // Gizmos
            MP.RegisterSyncMethod(AccessTools.DeclaredMethod("AlphaMechs.Pawn_HemogenVat:EjectContents"));
        }
    }
}