using System;
using Verse;
using HarmonyLib;
using Multiplayer.API;

namespace Multiplayer.Compat
{
    /// <summary>RunAndGun by roolo</summary>
    /// <see href="https://github.com/rheirman/RunAndGun"/>
    /// <see href="https://github.com/MemeGoddess/RunAndGun"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=1204108550"/>
    [MpCompatFor("roolo.RunAndGun")]
    class RunandGun
    {
        public RunandGun(ModContentPack mod)
        {
            MpCompat.RegisterLambdaDelegate("RunAndGun.Harmony.Pawn_DraftController_GetGizmos_Patch", "Postfix", 2);
            PatchingUtilities.PatchUnityRand("RunAndGun.Harmony.MentalStateHandler_TryStartMentalState:shouldRunAndGun", false);
        }
    }
}
