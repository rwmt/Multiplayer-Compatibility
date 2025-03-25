using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat;

/// <summary>Biomes! Polluted Lands by The Biomes Mod Team</summary>
/// <see href="https://github.com/biomes-team/BiomesPollutedLands"/>
/// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3390196656"/>
[MpCompatFor("BiomesTeam.BiomesPollutedLands")]
public class BiomesPollutedLands
{
    public BiomesPollutedLands(ModContentPack mod)
    {
        #region (Dev) gizmos

        {
            // DEV: chronic pains
            MP.RegisterSyncMethod(AccessTools.DeclaredMethod("BMT_PollutedLands.Gene_AddHediffWithInterval:Pain")).SetDebugOnly();
            // Dev: add or remove hediff
            MpCompat.RegisterLambdaMethod("BMT_PollutedLands.Gene_AddOrRemoveHediff", nameof(Gene.GetGizmos), 0).SetDebugOnly();
            // Dev: blood vomit
            MP.RegisterSyncMethod(AccessTools.DeclaredMethod("BMT_PollutedLands.Gene_BloodExplusion:Vomit")).SetDebugOnly();
            // Dev: try hunt for food
            MpCompat.RegisterLambdaMethod("BMT_PollutedLands.Gene_CarrionMetabolism", nameof(Gene.GetGizmos), 0).SetDebugOnly();
            // Dev: heal permanent wound
            MP.RegisterSyncMethod(AccessTools.DeclaredMethod("BMT_PollutedLands.Gene_MoltingRegeneration:TryHealWound")).SetDebugOnly();
            // Dev: release gas
            MpCompat.RegisterLambdaMethod("BMT_PollutedLands.Gene_ToxspewingPores", nameof(Gene.GetGizmos), 0).SetDebugOnly();
        }

        #endregion

        #region RNG

        {
            PatchingUtilities.PatchSystemRand("BMT_PollutedLands.Hediff_Mutapox:Tick");
        }

        #endregion
    }
}