using HarmonyLib;
using Multiplayer.API;
using Multiplayer.Compat;
using System;
using Verse;

namespace Multiplayer_Compat
{
    [MpCompatFor("syrchalis.setupcamp")]
    class SetUpCamp
    {

        public SetUpCamp(ModContentPack mod)
        {
            Type SetUpCamp_Utility = AccessTools.TypeByName("Syrchalis_SetUpCamp.SetUpCamp_Utility");
            //Sync the method that is in charge of generating the camp map
            MP.RegisterSyncMethod(SetUpCamp_Utility, "Camp");
        }
    }
}

