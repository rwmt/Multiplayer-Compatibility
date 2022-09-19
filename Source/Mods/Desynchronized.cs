using System;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Desynchronized by Vectorial1024s and emipa606</summary>
    /// <see href="https://github.com/Vectorial1024/Desynchronized"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2222607126"/>
    /// contribution to Multiplayer Compatibility by Ari
    [MpCompatFor("Mlie.Desynchronized")]
    public class Desynchronized
    {
        public Desynchronized(ModContentPack mod)
        {
            var rngFixMethods = new[]
            {
                "Desynchronized.TNDBS.Pawn_NewsKnowledgeTracker:ForgetRandom",
                "Desynchronized.TNDBS.Pawn_NewsKnowledgeTracker:ForgetRandomly",
                "Desynchronized.TNDBS.Utilities.NewsSpreadUtility:SelectNewsRandomly",
                "Desynchronized.TNDBS.Utilities.NewsSpreadUtility:SelectNewsDistinctly",
                "Desynchronized.Patches.NewsTransmit.PostFix_InteractionWorker:ExecuteNewsTarnsmission",
                "Desynchronized.TNDBS.TaleNewsPawnDied:CalculateNewsImportanceForPawn",
            };
            PatchingUtilities.PatchPushPopRand(rngFixMethods);
        }
    }
}