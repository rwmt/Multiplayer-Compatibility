using System.Linq;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Reinforced Mechanoid 2 by Helixien, Taranchuk, OskarPotocki, Luizi, IcingWithCheeseCake, Sir Van, Reann_Shepard</summary>
    /// <see href="https://github.com/Helixien/ReinforcedMechanoids-2"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2910050186"/>
    [MpCompatFor("hlx.ReinforcedMechanoids2")]
    public class ReinforcedMechanoids2
    {
        public ReinforcedMechanoids2(ModContentPack mod)
        {
            // Gestalt Engine assembly
            {
                var type = AccessTools.TypeByName("GestaltEngine.CompGestaltEngine");
                // Both called from targeter inside of lambda methods for CompGetGizmosExtra
                MP.RegisterSyncMethod(type, "StartConnect"); // Connect colony mechanoid
                MP.RegisterSyncMethod(type, "StartConnectNonColonyMech"); // Hack hostile mechanoid
                // Upgrade/downgrade (0/1) and dev instant upgrade/downgrade (2/3)
                MpCompat.RegisterLambdaMethod("GestaltEngine.CompUpgradeable", "CompGetGizmosExtra", 0, 1, 2, 3).TakeLast(2).SetDebugOnly();
                // Tune pawn (2)/engine (3) to band node (methods are identical)
                MpCompat.RegisterLambdaDelegate("GestaltEngine.CompBandNode_CompGetGizmosExtra_Patch", "Postfix", 2, 3);
            }

            // Reinforced Mechanoid assembly
            {
                // Called from CompCauseGameCondition_ForceWeather and CommandAction_RightClickWeather
                MP.RegisterSyncMethod(AccessTools.DeclaredMethod("ReinforcedMechanoids.CompCauseGameCondition_ForceWeather:ChangeWeather"));
                // Called from CompCauseGameCondition_TemperatureOffset
                MP.RegisterSyncMethod(AccessTools.DeclaredMethod("ReinforcedMechanoids.CompCauseGameCondition_TemperatureOffset:SetTemperatureOffset"));
                // Called from Targeter called from gizmo
                MP.RegisterSyncMethod(AccessTools.DeclaredMethod("ReinforcedMechanoids.CompPawnJumper:DoJump", new []{ typeof(LocalTargetInfo) }));

                // Toggle overlay on/off
                MpCompat.RegisterLambdaDelegate("ReinforcedMechanoids.CompAllianceOverlayToggle", "CompGetGizmosExtra", 1);
                // Toggle invisibility on/off
                MpCompat.RegisterLambdaMethod("ReinforcedMechanoids.CompInvisibility", "CompGetGizmosExtra", 1);

                // ReinforcedMechanoids.Building_Container:GetMultiSelectFloatMenuOptions has a delegate that will need syncing,
                // but as of right now - it's broken in the RM2 mod itself and causes ArgumentOutOfRangeException
                // First it clears tmpAllowedPawns list, and then tries to access its values
                // https://github.com/Helixien/ReinforcedMechanoids-2/blob/4772309ab7329e94c8c4cc5d1d40f38946200308/1.4/Source/ReinforcedMechanoids/ReinforcedMechanoids/Comps/CompLootContainer.cs#L245
            }
        }
    }
}