using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Perspective: Buildings by Owlchemist</summary>
    /// <see href="https://github.com/Owlchemist/perspective-buildings"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2594383552"/>
    [MpCompatFor("Owlchemist.PerspectiveBuildings")]
    [MpCompatFor("Mlie.PerspectiveBuildings")]
    internal class PerspectiveBuildings
    {
        public PerspectiveBuildings(ModContentPack mod)
        {
            LongEventHandler.ExecuteWhenFinished(LatePatch);
        }

        private static void LatePatch()
        {
            var type = AccessTools.TypeByName("Perspective.CompOffsetter");
            MP.RegisterSyncMethod(type, "SetCurrentOffset");
            MP.RegisterSyncMethod(type, "SetMirroredState");
        }
    }
}
