using System.Linq;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat;

/// <summary>Exosuit Framework by AobaKuma</summary>
/// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3352894993"/>
/// <see href="https://github.com/AobaKuma/MechsuitFramework"/>
[MpCompatFor("Aoba.Exosuit.Framework")]
public class ExosuitFramework
{
    public ExosuitFramework(ModContentPack mod)
    {
        LongEventHandler.ExecuteWhenFinished(LatePatch);

        #region Gizmos

        {
            // Eject to location (0), launch to map (3), release (8)
            MpCompat.RegisterLambdaMethod("WalkerGear.Building_EjectorBay", nameof(Building.GetGizmos), 0, 3, 8)
                // The first 2 gizmos use current map, the last one doesn't care
                .SkipLast(1).SetContext(SyncContext.CurrentMap);
            // Get in (0), toggle auto repair (2)
            MpCompat.RegisterLambdaMethod("WalkerGear.Building_MaintenanceBay", nameof(Building.GetGizmos), 0, 2);
            // Toggle safety (1), syncing it is not necessary, as it's only used to enable the eject
            // gizmo. Better to sync it anyway in case some mods end up using it in some other way.
            // Eject (2) is synced through WalkerGear_Core:Eject for more compatibility.
            MpCompat.RegisterLambdaMethod("WalkerGear.ModuleComp_EmergencyEject", nameof(ThingComp.CompGetWornGizmosExtra), 1);
        }

        #endregion

        #region Float Menus

        {
            var type = AccessTools.TypeByName("WalkerGear.Building_MaintenanceBay");
            // Add/replace/remove methods, called from ITab_MechGear
            MP.RegisterSyncMethod(type, "AddOrReplaceModule");
            MP.RegisterSyncMethod(type, "RemoveModules");

            type = AccessTools.TypeByName("WalkerGear.FloatMenuMakerMap_MakeForFrame");
            // Take to maintenance bay. Needs to be synced due to the method
            // modifying a field after starting a job - `job.count = 1`.
            MpCompat.RegisterLambdaDelegate(type, "AddHumanlikeOrders", 1);
        }

        #endregion

        // Once it's included in a mod/implemented, also patch Building_EjectorBay and WG_PawnFlyer.
        // The building (and thus the flyer) aren't used by this mod or any of its addons yet.
    }

    private static void LatePatch()
    {
        #region Gizmos

        {
            // Eject, only called from lambda in ModuleComp_EmergencyEject. Could sync it instead,
            // but could be called from other mods. Probably safer to call this method.
            MP.RegisterSyncMethod(AccessTools.DeclaredMethod("WalkerGear.WalkerGear_Core:Eject"));
        }

        #endregion
    }
}