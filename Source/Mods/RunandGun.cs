using System;
using Verse;
using HarmonyLib;
using Multiplayer.API;

namespace Multiplayer.Compat
{
    /// <summary>RunAndGun by roolo, continued by MemeGoddess</summary>
    /// <see href="https://github.com/rheirman/RunAndGun"/>
    /// <see href="https://github.com/MemeGoddess/RunAndGun-continued"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=1204108550"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3070109648"/>
    [MpCompatFor("roolo.RunAndGun")]
    [MpCompatFor("MemeGoddess.RunAndGun")]
    class RunandGun
    {
        public RunandGun(ModContentPack mod)
        {
            var type = AccessTools.TypeByName("RunAndGun.Harmony.Pawn_DraftController_GetGizmos_Patch");
            if (type != null)
            {
                MP.RegisterSyncDelegateLambda(type, "Postfix", 1);
            }

            type = AccessTools.TypeByName("RunAndGun.Utilities.WorldComponent_ToggleData");
            if (type != null)
            {
                MP.RegisterSyncMethod(type, "SetRunAndGun");
            }

            var mentalStateMethod = AccessTools.Method("RunAndGun.Harmony.MentalStateHandler_TryStartMentalState:shouldRunAndGun");
            if (mentalStateMethod != null)
            {
                PatchingUtilities.PatchSystemRand("RunAndGun.Harmony.MentalStateHandler_TryStartMentalState:shouldRunAndGun", false);
            }
        }
    }
}
