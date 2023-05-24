using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Mech Framework by andery233xj</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2826884300"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2763366485"/>
    [MpCompatFor("Aoba.AMP")]
    [MpCompatFor("Aoba.WalkerGears")] // 1.3, not updated yet
    // Couldn't find any more mods using it, but more may exist.
    public class MechFramework
    {
        // Just in case multiple mods using it are active
        private static bool patchOnce = false;

        public MechFramework(ModContentPack mod)
        {
            if (patchOnce)
                return;

            patchOnce = true;
            LongEventHandler.ExecuteWhenFinished(LatePatch);
        }

        private static void LatePatch()
            => MP.RegisterSyncMethod(AccessTools.DeclaredMethod("WalkerGear.WalkerGear_Core:GetOut"));
    }
}