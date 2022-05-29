using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Vanilla Ideology Expanded - Memes and Structures by Oskar Potocki, Sarg Bjornson, Chowder</summary>
    /// <see href="https://github.com/juanosarg/VanillaIdeologyExpanded-Memes"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2636329500"/>
    [MpCompatFor("VanillaExpanded.VMemesE")]
    internal class VanillaIdeologyMemes
    {
        public VanillaIdeologyMemes(ModContentPack mod)
        {
            // RNG
            {
                var methods = new[]
                {
                    // Commented out the ones that use seeded random, as they should be fine
                    // If not, then uncommenting those lines should fix it
                    //"VanillaMemesExpanded.VanillaMemesExpanded_GameConditionManager_RegisterCondition_Patch:SendRandomMood",
                    //"VanillaMemesExpanded.VanillaMemesExpanded_GameCondition_Aurora_Init_Patch:SendRandomMood",
                    //"VanillaMemesExpanded.RitualOutcomeEffectWorker_DivineStars:Apply",
                    "VanillaMemesExpanded.RitualOutcomeEffectWorker_SlaveEmancipation:Apply",
                    "VanillaMemesExpanded.RitualOutcomeEffectWorker_ViolentConversion:Apply",
                };
                PatchingUtilities.PatchSystemRand(methods, false);

                // Patch separately to avoid "Ambiguous match in Harmony patch"
                var compAbilityHarvestBodyPartsApply = AccessTools.Method("VanillaMemesExpanded.CompAbilityHarvestBodyParts:Apply", new System.Type[] { typeof(LocalTargetInfo), typeof(LocalTargetInfo) });
                PatchingUtilities.PatchPushPopRand(compAbilityHarvestBodyPartsApply);
            }

            // Gizmos
            {
                var type = AccessTools.TypeByName("VanillaMemesExpanded.CompDeconstructJunk");

                MP.RegisterSyncMethod(type, "SetObjectForDeconstruction");
                MP.RegisterSyncMethod(type, "CancelObjectForDeconstruction");
            }
        }
    }
}
