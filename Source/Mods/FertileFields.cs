using System;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Fertile Fields by Jamaican Castle</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2012735237"/>
    [MpCompatFor("jamaicancastle.RF.fertilefields")]
    [MpCompatFor("greysuki.RF.fertilefields")]
    class FertileFieldsCompat
    {
        public FertileFieldsCompat(ModContentPack mod)
        {
            LongEventHandler.ExecuteWhenFinished(LatePatch);

            {
                // RNG
                PatchingUtilities.PatchSystemRand("RFF_Code.Building_CompostBin:PlaceProduct");
            }

            Type type = AccessTools.TypeByName("RFF_Code.GrowZoneManager");

            MP.RegisterSyncMethod(type, "ToggleReturnToSoil");
            MP.RegisterSyncMethod(type, "ToggleDesignateReplacements");
        }

        private static void LatePatch()
        {
            // Dev: set progress to 1
            MpCompat.RegisterLambdaMethod("RFF_Code.Building_CompostBarrel", nameof(Building.GetGizmos), 0).SetDebugOnly();
        }
    }
}
