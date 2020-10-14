using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Deep Storage by LWM</summary>
    /// <see href="https://github.com/lilwhitemouse/RimWorld-LWM.DeepStorage"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=1617282896"/>
    [MpCompatFor("LWM.DeepStorage")]
    class DeepStorage
    {
        public DeepStorage(ModContentPack mod) 
            => MP.RegisterSyncMethod(AccessTools.TypeByName("LWM.DeepStorage.ITab_DeepStorage_Inventory"), "EjectTarget").CancelIfAnyArgNull();
    }
}