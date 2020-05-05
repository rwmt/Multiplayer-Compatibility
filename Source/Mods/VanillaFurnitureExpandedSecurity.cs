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
    [MpCompatFor("Vanilla Furniture Expanded - Security")]
    class VFESecurity
    {
        public VFESecurity(ModContentPack mod)
        {
            LongEventHandler.ExecuteWhenFinished(LateFix);
        }

        private void LateFix()
        {
            // Artillery, mostly gizmos
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

            // RNG fix
            {
                var methods = new MethodBase[]
                {
                    AccessTools.Method("VFESecurity.ArtilleryStrikeArrivalAction_AIBase:Arrived"),
                    AccessTools.Method("VFESecurity.ArtilleryStrikeArrivalAction_Insectoid:StrikeAction"),
                    AccessTools.Method("VFESecurity.ArtilleryStrikeArrivalAction_Map:Arrived"),
                    AccessTools.Method("VFESecurity.ArtilleryStrikeArrivalAction_Outpost:StrikeAction"),
                    AccessTools.Method("VFESecurity.ArtilleryStrikeArrivalAction_PeaceTalks:Arrived"),
                    AccessTools.Method("VFESecurity.ArtilleryStrikeArrivalAction_Settlement:StrikeAction"),
                    AccessTools.Method("VFESecurity.ArtilleryComp:CompTick"),
                    AccessTools.Method("VFESecurity.ArtilleryComp:TryStartBombardment"),
                    AccessTools.Method("VFESecurity.WorldObjectCompProperties_Artillery:ArtilleryCountFor"),
                    //AccessTools.Method("VFESecurity.Patch_Building_Trap.Spring:ShouldDestroy"),
                    AccessTools.Method(AccessTools.Inner(AccessTools.TypeByName("VFESecurity.Patch_Building_Trap"), "Spring"), "ShouldDestroy"),
                    // ArtilleryStrikeUtility:GetRandomShellFor and ArtilleryStrikeUtility:PotentialStrikeCells are only called by methods that are patched already
                    AccessTools.Method("VFESecurity.Building_BarbedWire:SpringSub"),
                    AccessTools.Method("VFESecurity.Building_TrapBear:SpringSub"),

                    // This one seems like it should have no random calls at all in its hierarchy, but desync traces show that there are actually some.
                    AccessTools.Method("VFESecurity.Verb_Dazzle:TryCastShot"),

                    AccessTools.Method("VFESecurity.CompLongRangeArtillery:CompTick"),
                    // ArtilleryComp:TryResolveArtilleryCount is called by ArtilleryComp:CompTick
                    AccessTools.Method("VFESecurity.Building_Shield:Notify_EnergyDepleted"),
                    AccessTools.Method("VFESecurity.Building_Shield:AbsorbDamage", new Type[] { typeof(float), typeof(DamageDef), typeof(float) }),
                    AccessTools.Method("VFESecurity.Building_Shield:Draw"),

                    AccessTools.Method("VFESecurity.ExtendedMoteMaker:SearchlightEffect"),
                    AccessTools.Method("ExplosiveTrailsEffect.SmokeThrowher:ThrowSmokeTrail"),
                };

                foreach (var method in methods)
                {
                    MpCompat.harmony.Patch(method,
                        prefix: new HarmonyMethod(typeof(VFEF), nameof(FixRNGPre)),
                        postfix: new HarmonyMethod(typeof(VFEF), nameof(FixRNGPos))
                    );
                }
            }
        }

        static void FixRNGPre() => Rand.PushState();
        static void FixRNGPos() => Rand.PopState();
    }
}
