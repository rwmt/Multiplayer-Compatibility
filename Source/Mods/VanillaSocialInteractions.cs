using System.Collections;
using HarmonyLib;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Vanilla Social Interactions Expanded by Oskar Potocki, Taranchuk</summary>
    /// <see href="https://github.com/Vanilla-Expanded/VanillaSocialInteractionsExpanded"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2439736083"/>
    [MpCompatFor("VanillaExpanded.VanillaSocialInteractionsExpanded")]
    public class VanillaSocialInteractions
    {
        private static AccessTools.FieldRef<IDictionary> averageOpinionCacheField;

        public VanillaSocialInteractions(ModContentPack mod)
        {
            // Clear cache
            var field = AccessTools.DeclaredField("VanillaSocialInteractionsExpanded.VSIE_Utils:averageOpinionOfCache");
            averageOpinionCacheField = AccessTools.StaticFieldRefAccess<IDictionary>(field);

            MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(GameComponentUtility), nameof(GameComponentUtility.FinalizeInit)),
                postfix: new HarmonyMethod(typeof(VanillaSocialInteractions), nameof(ClearCache)));
        }

        private static void ClearCache() => averageOpinionCacheField().Clear();
    }
}