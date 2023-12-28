using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Gene Ripper by Obi Vayne Kenobi</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2885485814"/>
    [MpCompatFor("DanielWedemeyer.GeneRipper")]
    public class GeneRipper
    {
        public GeneRipper(ModContentPack mod)
        {
            // It's loading resources, must patch later.
            LongEventHandler.ExecuteWhenFinished(LatePatch);
        }

        static void LatePatch()
        {
            var type = AccessTools.TypeByName("GeneRipper.Building_GeneRipper");
            // Dialog accept
            MpCompat.RegisterLambdaDelegate(type, "SelectPawn", 1);

            // Command finish, debug
            MP.RegisterSyncMethod(type, "Finish");

            // Command cancel
            MP.RegisterSyncMethod(type, "Cancel");
        }
    }
}