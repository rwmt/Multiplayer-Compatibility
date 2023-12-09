using System.Collections;
using HarmonyLib;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Vanilla Psycasts Expanded - Hemosage by Oskar Potocki, Taranchuk, Reann Shepard, Sir Van</summary>
    /// <see href="https://github.com/Vanilla-Expanded/VanillaPsycastsExpanded-Hemosage"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2990596478"/>
    [MpCompatFor("VanillaExpanded.VPE.Hemosage")]
    public class VanillaPsycastsExpandedHemosage
    {
        private static AccessTools.FieldRef<IDictionary> unroofedCellsField;

        public VanillaPsycastsExpandedHemosage(ModContentPack mod)
        {
            // RNG
            {
                PatchingUtilities.PatchPushPopRand("VPEHemosage.Hediff_Bloodmist:ThrowFleck");
                // Doesn't seem like it's needed (no ShouldSpawnMotesAt) - VPEHemosage.Ability_CorpseExplosion:ThrowBloodSmoke
            }

            // Gizmos
            {
                // Cancel bloodfocus/remove hediff
                MpCompat.RegisterLambdaDelegate("VPEHemosage.Ability_Bloodfocus", "GetGizmo", 0);
            }

            // Cache
            {
                unroofedCellsField = AccessTools.StaticFieldRefAccess<IDictionary>(
                    AccessTools.DeclaredField("VPEHemosage.WeatherOverlay_Bloodstorm:unroofedCells"));

                MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(GameComponentUtility), nameof(GameComponentUtility.FinalizeInit)),
                    postfix: new HarmonyMethod(typeof(VanillaPsycastsExpandedHemosage), nameof(ClearCache)));
            }
        }

        private static void ClearCache() => unroofedCellsField().Clear();
    }
}