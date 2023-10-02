using System.Collections;
using HarmonyLib;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Vanilla Psycasts Expanded - Puppeteer by Oskar Potocki, Taranchuk, Reann Shepard</summary>
    /// Not on Github yet it seems
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3033779606"/>
    [MpCompatFor("VanillaExpanded.VPE.Puppeteer")]
    public class VanillaPsycastsExpandedPuppeteer
    {
        private static AccessTools.FieldRef<IDictionary> canDoRandomBreaksCache;

        public VanillaPsycastsExpandedPuppeteer(ModContentPack mod)
        {
            // RNG
            {
                PatchingUtilities.PatchPushPopRand("VPEPuppeteer.Hediff_BrainLeech:ThrowFleck");
            }

            // Cache
            {
                // May not be necessary, but it's hard to test if it actually is or isn't.
                // Won't break anything or cause issues to have this, so may as well keep it.
                canDoRandomBreaksCache = AccessTools.StaticFieldRefAccess<IDictionary>(
                    AccessTools.DeclaredField("VPEPuppeteer.MentalBreaker_CanDoRandomMentalBreaks_Patch:cachedResults"));

                MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(GameComponentUtility), nameof(GameComponentUtility.FinalizeInit)),
                    postfix: new HarmonyMethod(typeof(VanillaPsycastsExpandedPuppeteer), nameof(ClearCache)));
            }
        }

        private static void ClearCache() => canDoRandomBreaksCache().Clear();
    }
}