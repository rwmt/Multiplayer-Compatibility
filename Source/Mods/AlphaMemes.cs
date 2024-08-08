using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Alpha Memes by Sarg Bjornson, Helixien, Cassie, Luizi</summary>
    /// <see href="https://github.com/juanosarg/AlphaMemes"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2661356814"/>
    [MpCompatFor("Sarg.AlphaMemes")]
    internal class AlphaMemes
    {
        private static Type thoughtCatharsisType;

        public AlphaMemes(ModContentPack mod)
        {
            PatchingUtilities.PatchSystemRand("AlphaMemes.AlphaMemes_DamageWorker_AddInjury_Apply_Patch:SendHistoryIfMelee", false);
            PatchingUtilities.PatchPushPopRand("AlphaMemes.RitualBehaviorWorker_FuneralFramework:TryExecuteOn");
            PatchingUtilities.PatchSystemRandCtor("AlphaMemes.CompAbilityOcularConversion");
            // The following method is seeded, so it should be fine
            // If not, then patching it as well should fix it
            //"AlphaMemes.GameComponent_RandomMood:GameComponentTick",

            // Hediffs added in MoodOffset, can be called during alert updates (not synced).
            thoughtCatharsisType = AccessTools.TypeByName("AlphaMemes.Thought_Catharsis");
            if (thoughtCatharsisType != null)
                PatchingUtilities.PatchTryGainMemory(TryGainThoughtCatharsis);
            else
                Log.Error("Trying to patch `AlphaMemes.Thought_Catharsis`, but the type is null. Did it get moved, renamed, or removed?");

            // Current map usage
            var type = AccessTools.TypeByName("AlphaMemes.AlphaMemesIdeo_Notify_Patches");
            PatchingUtilities.ReplaceCurrentMapUsage(AccessTools.Inner(type, "FuneralFramework_Ideo_MemberCorpseDestroyed"), "Prefix");
        }

        private static bool TryGainThoughtCatharsis(Thought_Memory thought)
        {
            if (!thoughtCatharsisType.IsInstanceOfType(thought))
                return false;

            // Call MoodOffset to cause the method to add hediffs, etc.
            thought.MoodOffset();
            return true;
        }
    }
}