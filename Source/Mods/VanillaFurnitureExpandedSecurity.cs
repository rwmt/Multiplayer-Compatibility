using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.Sound;

namespace Multiplayer.Compat
{
    /// <summary>Vanilla Furniture Expanded - Security by Oskar Potocki, Trunken, and XeoNovaDan </summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=1845154007"/>
    /// <see href="https://github.com/Vanilla-Expanded/VanillaFurnitureExpanded-Security"/>
    /// Contribution to Multiplayer Compatibility by Sokyran and Reshiram
    [MpCompatFor("VanillaExpanded.VFESecurity")]
    class VFESecurity
    {
        private static PropertyInfo selectedCompsProperty;
        private static AccessTools.FieldRef<object, GlobalTargetInfo> targetedTileField;
        private static MethodInfo resetWarmupTicksMethod;

        public VFESecurity(ModContentPack mod)
        {
            // RNG fix
            {
                var methodNames = new[]
                {
                    "VFESecurity.ArtilleryStrikeArrivalAction_AIBase:Arrived",
                    "VFESecurity.ArtilleryStrikeArrivalAction_Insectoid:StrikeAction",
                    "VFESecurity.ArtilleryStrikeArrivalAction_Map:Arrived",
                    "VFESecurity.ArtilleryStrikeArrivalAction_Outpost:StrikeAction",
                    "VFESecurity.ArtilleryStrikeArrivalAction_PeaceTalks:Arrived",
                    "VFESecurity.ArtilleryStrikeArrivalAction_Settlement:StrikeAction",
                    "VFESecurity.WorldObjectCompProperties_Artillery:ArtilleryCountFor",
                    // ArtilleryStrikeUtility:GetRandomShellFor and ArtilleryStrikeUtility:PotentialStrikeCells are only called by methods that are patched already
                    "VFESecurity.Building_BarbedWire:SpringSub",
                    "VFESecurity.Building_TrapBear:SpringSub",
                    // This one seems like it should have no random calls at all in its hierarchy, but desync traces show that there are actually some.
                    "VFESecurity.Verb_Dazzle:TryCastShot",
                    "NoCamShakeExplosions.DamageWorker_FlameNoCamShake:Apply",
                    "NoCamShakeExplosions.DamageWorker_FlameNoCamShake:ExplosionAffectCell",
                    // Motes
                    "VFESecurity.ExtendedMoteMaker:SearchlightEffect",
                };

                PatchingUtilities.PatchPushPopRand(AccessTools.Method(AccessTools.Inner(AccessTools.TypeByName("VFESecurity.Patch_Building_Trap"), "Spring"), "ShouldDestroy"));
                PatchingUtilities.PatchPushPopRand(methodNames);
                LongEventHandler.ExecuteWhenFinished(LateSyncMethods);
            }
        }

        private static void LateSyncMethods()
        {
            // Artillery fix
            {
                var type = AccessTools.TypeByName("VFESecurity.CompLongRangeArtillery");

                selectedCompsProperty = AccessTools.Property(type, "SelectedComps");
                targetedTileField = AccessTools.FieldRefAccess<GlobalTargetInfo>(type, "targetedTile");
                resetWarmupTicksMethod = AccessTools.DeclaredMethod(type, "ResetWarmupTicks");

                MP.RegisterSyncMethod(type, "ResetForcedTarget");
                MP.RegisterSyncMethod(typeof(VFESecurity), nameof(SetTargetedTile));

                MpCompat.harmony.Patch(AccessTools.Method(type, "SetTargetedTile"), new HarmonyMethod(typeof(VFESecurity), nameof(Prefix)));
            }

            // RNG fix
            {
                var methods = new[]
                {
                    // ArtilleryComp:TryResolveArtilleryCount is called by ArtilleryComp:CompTick
                    "VFESecurity.ArtilleryComp:BombardmentTick",
                    "VFESecurity.ArtilleryComp:TryStartBombardment",
                    "VFESecurity.Building_Shield:Notify_EnergyDepleted",
                    "VFESecurity.Building_Shield:Draw",
                    "VFESecurity.CompLongRangeArtillery:CompTick",
                };

                PatchingUtilities.PatchPushPopRand(AccessTools.Method("VFESecurity.Building_Shield:AbsorbDamage", new[] { typeof(float), typeof(DamageDef), typeof(float) }));
                PatchingUtilities.PatchPushPopRand(methods);
            }
        }

        private static bool Prefix(GlobalTargetInfo t)
        {
            if (!MP.IsInMultiplayer)
                return true;

            CameraJumper.TryHideWorld();
            var selected = ((IEnumerable)selectedCompsProperty.GetValue(null)).Cast<ThingComp>().ToList();
            SetTargetedTile(t.WorldObject, selected);
            return false;
        }

        private static void SetTargetedTile(WorldObject worldObject, List<ThingComp> elements)
        {
            foreach (var artillery in elements)
            {
                var turret = (Building_TurretGun)artillery.parent;
                turret.ResetForcedTarget();
                turret.ResetCurrentTarget();
                targetedTileField(artillery) = new GlobalTargetInfo(worldObject);
                SoundDefOf.TurretAcquireTarget.PlayOneShot(new TargetInfo(artillery.parent.Position, artillery.parent.Map, false));
                resetWarmupTicksMethod.Invoke(artillery, null);
            }
        }
    }
}
