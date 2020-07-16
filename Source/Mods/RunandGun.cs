using System;
using Verse;
using HarmonyLib;
using Multiplayer.API;

namespace Multiplayer.Compat
{
    [MpCompatFor("roolo.RunAndGun")]
    class RunandGun
    {
        Type type = AccessTools.TypeByName("RunAndGun.Harmony.Pawn_DraftController_GetGizmos_Patch");
        public RunandGun(ModContentPack mod)
            {
                MP.RegisterSyncDelegate(type, "<>c__DisplayClass0_0", "<Postfix>b__1");
            }
    }


}
