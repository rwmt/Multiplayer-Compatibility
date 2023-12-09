using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Enhanced Vat Learning by SmArtKar, Elseud</summary>
    /// <see href="https://github.com/SmArtKar/EnhancedVatLearning"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2908453851"/>
    [MpCompatFor("smartkar.enhancedvatlearning")]
    public class EnhancedVatLearning
    {
        public EnhancedVatLearning(ModContentPack mod) 
            => PatchingUtilities.PatchSystemRandCtor("EnhancedVatLearning.HediffComp_EnhancedLearning", false);
    }
}