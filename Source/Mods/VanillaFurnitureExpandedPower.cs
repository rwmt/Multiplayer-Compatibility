using System;
using HarmonyLib;
using Multiplayer.API;
using UnityEngine;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Vanilla Furniture Expanded - Power by Oskar Potocki and Sarg Bjornson</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2062943477"/>
    /// <see href="https://github.com/AndroidQuazar/VanillaFurnitureExpanded-Power"/>
    /// Contribution to Multiplayer Compatibility by Sokyran and Reshiram
    [MpCompatFor("VanillaExpanded.VFEPower")]
    class VanillaPowerExpanded
    {
        private static AccessTools.FieldRef<object, float> windDirectionField;

        public VanillaPowerExpanded(ModContentPack mod)
        {
            Type type;

            // Gizmos
            // Debug fill/empty
            {
                type = AccessTools.TypeByName("GasNetwork.CompGasStorage");
                // Both these methods are calling (basically) the same method,
                // but that method also has other callers that don't need syncing
                MpCompat.RegisterLambdaMethod(type, "CompGetGizmosExtra", 0, 1).SetDebugOnly();
            }

            // Order to or cancel plugging the hole (method names shared by both types)
            {
                var types = new[]
                {
                    "VanillaPowerExpanded.Building_ChemfuelPond",
                    "VanillaPowerExpanded.Building_GasGeyser",
                };
                foreach (var typeName in types) {
                    type = AccessTools.TypeByName(typeName);
                    MP.RegisterSyncMethod(type, "SetHoleForPlugging");
                    MP.RegisterSyncMethod(type, "CancelHoleForPlugging");
                }
            }
            
            // Violence generator
            {
                type = AccessTools.TypeByName("VanillaPowerExpanded.CompSoulsPowerPlant");
                MpCompat.RegisterLambdaMethod(type, "CompGetGizmosExtra", 1); // Toggle on/off
            }

            // RNG Fix
            {
                // Patch GasNetwork.MapComponent_WindDirection ctor
                PatchingUtilities.PatchSystemRand(AccessTools.Constructor(AccessTools.TypeByName("GasNetwork.MapComponent_WindDirection"), new Type[]{typeof(Map)}), false);

                var methods = new[]
                {
                    "VanillaPowerExpanded.Building_SmallBattery:Tick",
                    "VanillaPowerExpanded.Building_SmallBattery:PostApplyDamage",
                    "VanillaPowerExpanded.WeatherEvent_CustomLightningStrike:FireEvent",
                    "VanillaPowerExpanded.MapComponentExtender:doMapSpawns",
                    "VanillaPowerExpanded.CompPlantHarmRadiusIfBroken:CompTick",
                    // HarmRandomPlantInRadius is only called by CompPlantHarmRadiusIfBroken:CompTick, no need for patching
                    // CompPowerPlantNuclear:AffectCell is only calling a seeded random
                    "VanillaPowerExpanded.IntermittentGasSprayer:SteamSprayerTick",
                    // IntermittentGasSprayer - NewBaseAirPuff is only called by ThrowAirPuffUp which is called by SteamSprayerTick, no need for patching
                    // Building_GasGeyser:StartSpray is assigned to IntermittentGasSprayer:startSprayCallback, which is called from SteamSprayerTick
                    "GasNetwork.GasNet:GasNetTick",
                    "GasNetwork.MapComponent_WindDirection:MapGenerated",
                    // PipeNetGrid pushes and pops all Rand calls, no need to patch
                    // CompPowerAdvancedWater:RebuildCache is only calling a seeded random
            };

                // These methods are loading resources in their .ctor, must be patched later
                var methodsForLater = new[]
                {
                    "VanillaPowerExpanded.CompPowerAdvancedWater:PostSpawnSetup",
                    "VanillaPowerExpanded.CompPowerAdvancedWind:PostSpawnSetup",
                };

                PatchingUtilities.PatchPushPopRand(methods);
                LongEventHandler.ExecuteWhenFinished(() => PatchingUtilities.PatchPushPopRand(methodsForLater));

                // Wind map comp
                windDirectionField = AccessTools.FieldRefAccess<float>(AccessTools.TypeByName("GasNetwork.MapComponent_WindDirection"), "windDirection");
                MpCompat.harmony.Patch(AccessTools.Method("GasNetwork.MapComponent_WindDirection:MapComponentTick"),
                    prefix: new HarmonyMethod(typeof(VanillaPowerExpanded), nameof(ReplaceWindMapComponentTick)));

                // GasNet utilities
                MpCompat.harmony.Patch(AccessTools.Method("GasNetwork.Utilities:HashOffsetTicks"),
                    prefix: new HarmonyMethod(typeof(VanillaPowerExpanded), nameof(ReplaceHashOffsetTicks)));
            }
        }

        private static bool ReplaceWindMapComponentTick(MapComponent __instance)
        {
            if (!MP.IsInMultiplayer) return true;

            // Removed GetHashCode from here, most likely the source of issues with this method?
            // It means that the wind update for all maps will happen at the same time.
            // If we want to change it, the potential workarounds:
            // Use (__instance.map.Index * 10), multiply by 10 as there is a max of 20 maps for wind (would HashCode from int be more consistant?)
            // Something like Verse.Rand.RangeInclusiveSeeded(0, 250, __instance.map.Index)
            if (GenTicks.TicksAbs % 250 == 0)
            {
                const float twoPI = Mathf.PI * 2;
                // Use Verse rand instead of Unity
                windDirectionField(__instance) = (windDirectionField(__instance) + Rand.Range(-0.3f, 0.3f)) % twoPI;
            }

            return false;
        }

        private static bool ReplaceHashOffsetTicks(ref int __result)
        {
            if (!MP.IsInMultiplayer) return true;

            // Look: ReplaceWindMapComponentTick
            // The same issue - using the default GetHashCode
            // The GasNet class (that this method uses hash from)
            // stores reference to the Map it's used on - maybe 
            // map ID could be used for calling HashOffset on?
            // I'm leaving it as is as it doesn't make a big
            // difference overall.
            __result = Find.TickManager.TicksGame;

            return false;
        }
    }
}
