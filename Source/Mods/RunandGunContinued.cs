using System;
using Verse;
using HarmonyLib;
using Multiplayer.API;

namespace Multiplayer.Compat
{
    /// <summary>RunAndGun Continued</summary>
    /// <see href="https://github.com/MemeGoddess/RunAndGun"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3562365100"/>
    [MpCompatFor("memegoddess.RunAndGun")]
    class RunandGunContinued
    {
        public RunandGunContinued(ModContentPack mod)
        {
            MpCompat.RegisterLambdaDelegate("RunAndGun.Harmony.Pawn_DraftController_GetGizmos_Patch", "Postfix", 1);
            PatchingUtilities.PatchUnityRand("RunAndGun.Harmony.MentalStateHandler_TryStartMentalState:shouldRunAndGun", false);
        }
    }
}
