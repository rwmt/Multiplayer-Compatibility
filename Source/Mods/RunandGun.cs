using System;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>RunAndGun by roolo</summary>
    /// <see href="https://github.com/rheirman/RunAndGun"/>
    /// <see href="https://github.com/MemeGoddess/RunAndGun"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=1204108550"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3523879860"/>
    [MpCompatFor("roolo.RunAndGun")]
    [MpCompatFor("roolo.RunAndGun.kotobike")]
    [MpCompatFor("memegoddess.RunAndGun")]
    class RunandGun
    {
        [MpCompatSyncField("RunAndGun.CompRunAndGun", "isEnabled")]
        protected static ISyncField runAndGunEnabledField;

        public RunandGun(ModContentPack mod)
        {
            MpCompatPatchLoader.LoadPatch(this);

            MpCompat.RegisterLambdaDelegate("RunAndGun.Harmony.Pawn_DraftController_GetGizmos_Patch", "Postfix", 2);
            PatchingUtilities.PatchUnityRand("RunAndGun.Harmony.MentalStateHandler_TryStartMentalState:shouldRunAndGun", false);
        }

        [MpCompatPrefix("RunAndGun.Harmony.Pawn_DraftController_GetGizmos_Patch", "Postfix")]
        private static void WatchRunAndGunEnabled(Pawn __instance)
        {
            if (!MP.IsInMultiplayer)
                return;

            foreach (var comp in __instance.AllComps)
            {
                if (comp.GetType().Name != "CompRunAndGun")
                    continue;

                MP.WatchBegin();
                runAndGunEnabledField.Watch(comp);
                break;
            }
        }

        [MpCompatPostfix("RunAndGun.Harmony.Pawn_DraftController_GetGizmos_Patch", "Postfix")]
        private static void EndWatchRunAndGunEnabled()
        {
            if (MP.IsInMultiplayer)
                MP.WatchEnd();
        }
    }
}
