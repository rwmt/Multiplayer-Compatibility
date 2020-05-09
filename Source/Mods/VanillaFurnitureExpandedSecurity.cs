using System;
using System.Reflection;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Vanilla Furniture Expanded - Power by Oskar Potocki, Trunken, and XeoNovaDan </summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=1845154007"/>
    /// <see href="https://github.com/AndroidQuazar/VanillaFurnitureExpanded-Security"/>
    /// Contribution to Multiplayer Compatibility by Sokyran and Reshiram
    [MpCompatFor("VanillaExpanded.VFESecurity")]
    class VFESecurity
    {
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
                    "ExplosiveTrailsEffect.ExhaustFlames:ThrowRocketExhaustFlame",
                    "ExplosiveTrailsEffect.SmokeThrowher:ThrowSmokeTrail",
                };

                PatchingUtilities.PatchPushPopRand(AccessTools.Method(AccessTools.Inner(AccessTools.TypeByName("VFESecurity.Patch_Building_Trap"), "Spring"), "ShouldDestroy"));
                PatchingUtilities.PatchPushPopRand(methodNames);
                LongEventHandler.ExecuteWhenFinished(LateSyncMethods);
            }
        }

        static void LateSyncMethods()
        {
            // Artillery fix
            {
                var type = AccessTools.TypeByName("VFESecurity.CompLongRangeArtillery");

                var methods = new[]
                {
                    //"StartChoosingTarget",
                    "ResetForcedTarget",
                    //"SetTargetedTile",
                    //"ChooseWorldTarget",
                };

                foreach (var method in methods)
                    MP.RegisterSyncMethod(type, method);
            }

            // RNG fix
            {
                var methods = new[]
                {
                    // ArtilleryComp:TryResolveArtilleryCount is called by ArtilleryComp:CompTick
                    "VFESecurity.ArtilleryComp:CompTick",
                    "VFESecurity.ArtilleryComp:TryStartBombardment",
                    "VFESecurity.Building_Shield:Notify_EnergyDepleted",
                    "VFESecurity.Building_Shield:Draw",
                    "VFESecurity.CompLongRangeArtillery:CompTick",
                };

                PatchingUtilities.PatchPushPopRand(AccessTools.Method("VFESecurity.Building_Shield:AbsorbDamage", new Type[] { typeof(float), typeof(DamageDef), typeof(float) }));
                PatchingUtilities.PatchPushPopRand(methods);
            }
        }
    }
}
