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
        private static AccessTools.FieldRef<object> socialInteractionsManagerField;
        private static AccessTools.FieldRef<IDictionary> checkedPawnsField;

        public VanillaSocialInteractions(ModContentPack mod)
        {
            // Clear static caches between games
            // VSIE_Utils.averageOpinionOfCache - cached opinion values per pawn
            var field = AccessTools.DeclaredField("VanillaSocialInteractionsExpanded.VSIE_Utils:averageOpinionOfCache");
            averageOpinionCacheField = AccessTools.StaticFieldRefAccess<IDictionary>(field);

            // VSIE_Utils.sManager - cached GameComponent reference, stale between games
            var sManagerField = AccessTools.DeclaredField("VanillaSocialInteractionsExpanded.VSIE_Utils:sManager");
            socialInteractionsManagerField = AccessTools.StaticFieldRefAccess<object>(sManagerField);

            // ImmunityRecord_ImmunityTickInterval_Patch.checkedPawns - tracks pawns checked for trait development
            var checkedField = AccessTools.DeclaredField("VanillaSocialInteractionsExpanded.ImmunityRecord_ImmunityTickInterval_Patch:checkedPawns");
            checkedPawnsField = AccessTools.StaticFieldRefAccess<IDictionary>(checkedField);

            MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(GameComponentUtility), nameof(GameComponentUtility.FinalizeInit)),
                postfix: new HarmonyMethod(typeof(VanillaSocialInteractions), nameof(ClearCache)));
        }

        private static void ClearCache()
        {
            averageOpinionCacheField().Clear();
            socialInteractionsManagerField() = null;
            checkedPawnsField().Clear();
        }
    }
}