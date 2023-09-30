using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Vanilla Races Expanded - Fungoid by Oskar Potocki, Sarg Bjornson</summary>
    /// <see href="https://github.com/Vanilla-Expanded/VanillaRacesExpanded-Fungoid"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3042690053"/>
    [MpCompatFor("vanillaracesexpanded.fungoid")]
    public class VanillaRacesFungoid
    {
        private static AccessTools.FieldRef<IDictionary<Pawn, XenotypeDef>> pawnsAndXenotypesDictionaryField;

        public VanillaRacesFungoid(ModContentPack mod)
        {
            // RNG
            {
                PatchingUtilities.PatchSystemRand(new[]
                {
                    "VanillaRacesExpandedFungoid.DamageWorker_ExtraFungoidInfection:ApplySpecialEffectsToPart",
                    // random.NextDouble() > 0... Feels a bit pointless considering extremely low chance?
                    "VanillaRacesExpandedFungoid.DamageWorker_ExtraFungoidInfection_Bite:ApplySpecialEffectsToPart",
                });

                PatchingUtilities.PatchUnityRand("VanillaRacesExpandedFungoid.Building_FungoidShip:PopUpFungoids");
            }

            // Gizmos
            {
                // Dev advance by 10 days
                MpCompat.RegisterLambdaMethod("VanillaRacesExpandedFungoid.Hediff_GeneInfected", nameof(Hediff.GetGizmos), 0).SetDebugOnly();
            }

            // Cache
            {
                var type = AccessTools.TypeByName("VanillaRacesExpandedFungoid.StaticCollectionsClass");
                pawnsAndXenotypesDictionaryField = AccessTools.StaticFieldRefAccess<IDictionary<Pawn,XenotypeDef>>(AccessTools.DeclaredField(type, "pawns_and_xenotypes"));

                // Those 2 likely don't need clearing, and could possibly cause issues instead:
                // VanillaRacesExpandedFungoid.MapComponent_CoalescenceTracker:xenotypesAndMood_backup - used for exposing data only
                // VanillaRacesExpandedFungoid.StaticCollectionsClass:xenotypesAndMood - is exposed on load (copied from the previous one in FinalizeInit), so it should be safe

                MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(GameComponentUtility), nameof(GameComponentUtility.FinalizeInit)),
                    postfix: new HarmonyMethod(typeof(VanillaRacesFungoid), nameof(ClearCache)));
            }
        }

        private static void ClearCache()
        {
            pawnsAndXenotypesDictionaryField().Clear();
        }
    }
}