using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Vanilla Furniture Expanded - Security by Oskar Potocki, Trunken, and XeoNovaDan </summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=1845154007"/>
    /// <see href="https://github.com/Vanilla-Expanded/VanillaFurnitureExpanded-Security"/>
    /// Contribution to Multiplayer Compatibility by Sokyran and Reshiram
    [MpCompatFor("VanillaExpanded.VFESecurity")]
    class VFESecurity
    {
        public VFESecurity(ModContentPack mod)
        {
            LongEventHandler.ExecuteWhenFinished(LateSyncMethods);

            // 2 of the overloads of VFESecurity.TrenchUtility:FinalAdjustedRangeFromTerrain use `Find.CurrentMap`
            // which would normally cause issues, but they are unused by the mod at all.

            // RNG fix
            {
                // Motes
                PatchingUtilities.PatchPushPopRand("VFESecurity.ExtendedMoteMaker:SearchlightEffect");
            }

            // Patched sync methods
            {
                // When picking a new target the old one is (supposed to be) cleared.
                // Could cause issues if the player is behind on ticks.
                var type = AccessTools.TypeByName("VFESecurity.Patch_Building_TurretGun");
                type = AccessTools.Inner(type, "OrderAttack");
                PatchingUtilities.PatchCancelInInterface(AccessTools.DeclaredMethod(type, "Postfix"));
            }
        }

        private static void LateSyncMethods()
        {
            // Artillery fix
            {
                var type = AccessTools.TypeByName("VFESecurity.CompLongRangeArtillery");

                MP.RegisterSyncMethod(type, "ResetForcedTarget");

                var method = AccessTools.DeclaredMethod(type, "SetTargetedTile");
                MP.RegisterSyncMethod(method).SetContext(SyncContext.MapSelected);
                MpCompat.harmony.Patch(method, prefix: new HarmonyMethod(typeof(VFESecurity), nameof(PreSetTargetedTile)));
            }

            // RNG fix
            {
                var methods = new[]
                {
                    "VFESecurity.Building_Shield:Notify_EnergyDepleted",
                    "VFESecurity.Building_Shield:Draw",
                };

                PatchingUtilities.PatchPushPopRand(AccessTools.Method("VFESecurity.Building_Shield:AbsorbDamage", new[] { typeof(float), typeof(DamageDef), typeof(float) }));
                PatchingUtilities.PatchPushPopRand(methods);
            }
        }

        // Will run before the original method gets synced
        private static void PreSetTargetedTile()
        {
            // Close now, as waiting for the method to be synced may take a while depending on ticks behind
            if (!MP.IsExecutingSyncCommand)
                CameraJumper.TryHideWorld();
        }
    }
}
