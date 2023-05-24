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
            MpCompat.harmony.Patch(AccessTools.DeclaredMethod("AlphaMechs.CompChangeDef:CompTick"),
                transpiler: new HarmonyMethod(typeof(VanillaRacesPhytokin), nameof(VanillaRacesPhytokin.UseParentMap)));
            // Hediff runs out and mech turns back hostile
            MpCompat.harmony.Patch(AccessTools.DeclaredMethod("AlphaMechs.HediffComp_DeleteAfterTime:CompPostTick"),
                transpiler: new HarmonyMethod(typeof(VanillaRacesPhytokin), nameof(VanillaRacesPhytokin.UseParentMap)));

            // Gizmos
            MP.RegisterSyncMethod(AccessTools.DeclaredMethod("AlphaMechs.Pawn_HemogenVat:EjectContents"));
        }
    }
}