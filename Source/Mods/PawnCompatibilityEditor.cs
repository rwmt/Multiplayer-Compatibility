using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Pawn Compatibility Editor by Jomar</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3692227939"/>
    [MpCompatFor("jomar.pawncompatibilityeditor")]
    public class PawnCompatibilityEditor
    {
        public PawnCompatibilityEditor(ModContentPack mod)
        {
            var type = AccessTools.TypeByName("CompatibilityEditor.CompatibilityOverrideStore");
            MP.RegisterSyncMethod(type, "SetOverride");
            MP.RegisterSyncMethod(type, "RemoveOverride");
        }
    }
}