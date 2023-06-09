using HarmonyLib;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Alpha Memes by Sarg Bjornson, Helixien, Cassie, Luizi</summary>
    /// <see href="https://github.com/juanosarg/AlphaMemes"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2661356814"/>
    [MpCompatFor("Sarg.AlphaMemes")]
    internal class AlphaMemes
    {
        public AlphaMemes(ModContentPack mod)
        {
            PatchingUtilities.PatchSystemRand("AlphaMemes.AlphaMemes_DamageWorker_AddInjury_Apply_Patch:SendHistoryIfMelee", false);
            PatchingUtilities.PatchPushPopRand("AlphaMemes.RitualBehaviorWorker_FuneralFramework:TryExecuteOn");
            // The following method is seeded, so it should be fine
            // If not, then patching it as well should fix it
            //"AlphaMemes.GameComponent_RandomMood:GameComponentTick",

            // Hediffs added in MoodOffset, can be called during alert updates (not synced).
            // Possibly causes https://github.com/rwmt/Multiplayer-Compatibility/issues/302 
            PatchingUtilities.PatchCancelMethodOnUI("AlphaMemes.Thought_Catharsis:MoodOffset");

            // Current map usage
            var type = AccessTools.TypeByName("AlphaMemes.AlphaMemesIdeo_Notify_Patches");
            PatchingUtilities.ReplaceCurrentMapUsage(AccessTools.Inner(type, "FuneralFramework_Ideo_MemberCorpseDestroyed"), "Prefix");
        }
    }
}
