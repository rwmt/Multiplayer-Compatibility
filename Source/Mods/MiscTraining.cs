using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Misc. Training by HaploX1</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=717575199"/>
    [MpCompatFor("Haplo.Miscellaneous.Training")]
    class MiscTraining
    {
        public MiscTraining(ModContentPack mod)
        {
            PatchingUtilities.PatchPushPopRand("TrainingFacility.JobDriver_Archery:ShootArrow");
        }
    }
}