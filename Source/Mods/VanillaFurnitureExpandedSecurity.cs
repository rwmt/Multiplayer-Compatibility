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
            // Artillery, mostly gizmos
            {
                LongEventHandler.ExecuteWhenFinished(LateSyncMethods);
            }

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

                    "VFESecurity.CompLongRangeArtillery:CompTick",
                    // ArtilleryComp:TryResolveArtilleryCount is called by ArtilleryComp:CompTick

                    // Motes
                    "VFESecurity.ExtendedMoteMaker:SearchlightEffect",
                    "ExplosiveTrailsEffect.ExhaustFlames:ThrowRocketExhaustFlame",
                    "ExplosiveTrailsEffect.SmokeThrowher:ThrowSmokeTrail",
                };

                var methods = new[]
                {
                    AccessTools.Method(AccessTools.Inner(AccessTools.TypeByName("VFESecurity.Patch_Building_Trap"), "Spring"), "ShouldDestroy"),
                    AccessTools.Method("VFESecurity.Building_Shield:AbsorbDamage", new Type[] { typeof(float), typeof(DamageDef), typeof(float) }),
                };

                var methodsForLater = new[]
                {
                    "VFESecurity.ArtilleryComp:CompTick",
                    "VFESecurity.ArtilleryComp:TryStartBombardment",
                    "VFESecurity.Building_Shield:Notify_EnergyDepleted",
                    "VFESecurity.Building_Shield:Draw",
                };

                PatchingUtilities.PatchPushPopRand(methodNames);
                PatchingUtilities.PatchPushPopRand(methods);
                LongEventHandler.ExecuteWhenFinished(() => PatchingUtilities.PatchPushPopRand(methodsForLater));
            }
        }

        static void LateSyncMethods()
        {
            var type = AccessTools.TypeByName("VFESecurity.CompLongRangeArtillery");

            var methods = new[]
            {
                "StartChoosingTarget",
                "ResetForcedTarget",
                //"SetTargetedTile",
                //"ChooseWorldTarget",
            };

            foreach (var method in methods)
                MP.RegisterSyncMethod(type, method);
        }
    }
}
