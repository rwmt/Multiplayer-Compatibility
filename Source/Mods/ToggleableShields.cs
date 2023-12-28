using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Toggleable Shields by Owlchemist</summary>
    /// <see href="https://github.com/Owlchemist/toggleable-shields"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2690413766"/>
    [MpCompatFor("Owlchemist.ToggleableShields")]
    internal class ToggleableShields
    {
        public ToggleableShields(ModContentPack mod) => LongEventHandler.ExecuteWhenFinished(LatePatch);

        private static void LatePatch()
            => MP.RegisterSyncMethod(AccessTools.DeclaredMethod("ToggleableShields.Patch_Gizmo_EnergyShieldStatus_GizmoOnGUI:ToggleShields"));
    }
}
