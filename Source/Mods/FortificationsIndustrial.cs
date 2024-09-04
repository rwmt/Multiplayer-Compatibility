using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat;

/// <summary>Fortifications - Industrial by AobaKuma</summary>
/// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2561619583"/>
[MpCompatFor("Aoba.Fortress.Industrial")]
public class FortificationsIndustrial
{
    public FortificationsIndustrial(ModContentPack mod)
    {
        LongEventHandler.ExecuteWhenFinished(LatePatch);

        #region AOBAUtilities

        // Seem like it may be used at some point in a different mod. I could not find another one using this, but
        // if that ever happens - this should be extracted into a separate patch (with additional check to apply once).

        // RNG
        {
            PatchingUtilities.PatchPushPopRand("AOBAUtilities.CompFlecker:CompTick");
        }

        // Dev gizmos
        {
            MpCompat.RegisterLambdaMethod("AOBAUtilities.CompFueledSpawner", nameof(ThingComp.CompGetGizmosExtra), 0).SetDebugOnly();
        }

        #endregion
    }

    private static void LatePatch()
    {
        #region Fortifications - Industrial

        // RNG
        {
            PatchingUtilities.PatchPushPopRand("Fortification.CompCastFlecker:BurstFleck");
            PatchingUtilities.PatchUnityRand("Fortification.CompCastFlecker:DrawPos");
        }

        // Gizmos
        {
            // Make all pawns leave a building (bunker)
            MP.RegisterSyncMethod(AccessTools.DeclaredMethod("Fortification.Building_TurretCapacity:GetOut"));
            // (Dev) trigger countdown
            MpCompat.RegisterLambdaMethod("Fortification.CompExplosiveWithComposite", nameof(ThingComp.CompGetGizmosExtra), 0).SetDebugOnly();
            // Deploy minified thing, called from a 2 places
            MP.RegisterSyncMethod(AccessTools.DeclaredMethod("Fortification.MinifiedThingDeployable:Deploy"));
        }

        #endregion

        #region Combat Extended

        // May not be active so patches check for non-null method/type
        // Gizmos
        {
            // Make all pawns leave a building (bunker)
            var method = AccessTools.DeclaredMethod("Fortification.Building_TurretCapacityCE:GetOut");
            if (method != null)
                MP.RegisterSyncMethod(method);

            var type = AccessTools.TypeByName("Fortification.CompExplosiveWithCompositeCE");
            // (Dev) trigger countdown
            if (type != null)
                MP.RegisterSyncDelegateLambda(type, nameof(ThingComp.CompGetGizmosExtra), 0).SetDebugOnly();
        }

        #endregion
    }
}