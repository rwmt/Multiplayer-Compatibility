using Verse;

namespace Multiplayer.Compat
{
    /// <summary>
    /// Vanilla Nutrient Paste Expanded by Oskar Potocki, Kikohi
    /// Seems it's not on GitHub anymore?
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2920385763"/>
    /// </summary>
    [MpCompatFor("VanillaExpanded.VNutrientE")]
    public class VanillaNutrientPasteExpanded
    {
        public VanillaNutrientPasteExpanded(ModContentPack mod)
        {
            LongEventHandler.ExecuteWhenFinished(LatePatch);

            // Drop 5/10/20 meals
            MpCompat.RegisterLambdaDelegate("VNPE.Building_NutrientPasteDispenser_GetGizmos", "Postfix", 0, 1, 2);
        }

        private static void LatePatch()
        {
            // Drop 5/10/20 meals
            MpCompat.RegisterLambdaMethod("VNPE.Building_NutrientPasteTap", "GetGizmos", 0, 1, 2);

            // Drain
            MpCompat.RegisterLambdaMethod("VNPE.CompRegisterIngredients", "CompGetGizmosExtra", 0);
        }
    }
}