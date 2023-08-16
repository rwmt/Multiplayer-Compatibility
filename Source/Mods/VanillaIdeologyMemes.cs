using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Vanilla Ideology Expanded - Memes and Structures by Oskar Potocki, Sarg Bjornson, Chowder</summary>
    /// <see href="https://github.com/Vanilla-Expanded/VanillaIdeologyExpanded-Memes"/>
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
                    "VanillaMemesExpanded.CompAbilityHarvestBodyParts:Apply",
                    //"VanillaMemesExpanded.VanillaMemesExpanded_GameConditionManager_RegisterCondition_Patch:SendRandomMood",
                    //"VanillaMemesExpanded.VanillaMemesExpanded_GameCondition_Aurora_Init_Patch:SendRandomMood",
                    //"VanillaMemesExpanded.RitualOutcomeEffectWorker_DivineStars:Apply",
                    "VanillaMemesExpanded.RitualOutcomeEffectWorker_SlaveEmancipation:Apply",
                    "VanillaMemesExpanded.RitualOutcomeEffectWorker_ViolentConversion:Apply",
                };

                PatchingUtilities.PatchSystemRand(methods, false);
            }

            // Gizmos
            {
                var type = AccessTools.TypeByName("VanillaMemesExpanded.CompDeconstructJunk");

                MP.RegisterSyncMethod(type, "SetObjectForDeconstruction");
                MP.RegisterSyncMethod(type, "CancelObjectForDeconstruction");
            }

            // Gamplay logic during UI code
            {
                // Hediffs added in MoodOffset, can be called during alert updates (not synced)
                PatchingUtilities.PatchCancelInInterface("VanillaMemesExpanded.Thought_DisableFirstDefeatThought:MoodOffset");
            }

            // Patched sync methods
            {
                // Resets timer for the last time a settlement was abandoned. If called before syncing it
                // will cause pawns to temporarily lose specific thoughts related to them for the player syncing.
                PatchingUtilities.PatchCancelInInterface("VanillaMemesExpanded.VanillaMemesExpanded_SettlementAbandonUtility_Abandon_Patch:SetAbandonedTimeToZero");
            }
        }
    }
}