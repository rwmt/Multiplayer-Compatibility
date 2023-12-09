using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Fanilla Factions Expanded - Settlers by OskarPotocki.VanillaFactionsExpanded.SettlersModule</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2052918119"/>
    /// <see href="https://github.com/Vanilla-Expanded/VanillaFactionsExpanded-Settlers"/>
    [MpCompatFor("OskarPotocki.VanillaFactionsExpanded.SettlersModule")]
    class VanillaFactionsSettlers
    {
        public VanillaFactionsSettlers(ModContentPack mod)
        {
            var type = AccessTools.TypeByName("VFE_Settlers.Utilities.UtilityEvent");

            // Protection fee event
            MP.RegisterSyncDialogNodeTree(type, "ProtectionFee");
            // Caravan gizmo - turn in wanted criminal to settlement
            MpCompat.RegisterLambdaDelegate(type, "CommandTurnInWanted", 0);
            // Toggle mode
            MpCompat.RegisterLambdaMethod(AccessTools.TypeByName("Warmup.CompWarmUpReduction"), "CompGetGizmosExtra", 1);
            // Five fingers fillet table
            PatchingUtilities.PatchUnityRand("VFE_Settlers.JobGivers.JobDriver_PlayFiveFingerFillet:WatchTickAction", false);
        }
    }
}
