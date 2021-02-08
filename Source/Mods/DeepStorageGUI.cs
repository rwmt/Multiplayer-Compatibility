using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Netrve's DeepStorage GUI by Netrve</summary>
    /// <see href="https://github.com/Dakraid/RW_DSGUI/"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2169841018"/>
    [MpCompatFor("netrve.dsgui")]
    class DeepStorageGUI
    {
        public DeepStorageGUI(ModContentPack mod)
            => MP.RegisterSyncMethod(AccessTools.TypeByName("DSGUI.DSGUI_TabItem"), "EjectTarget").CancelIfAnyArgNull();
    }
}