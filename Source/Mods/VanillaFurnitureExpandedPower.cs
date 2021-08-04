using System;
using System.Reflection;
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
        private static FieldInfo windDirectionField;

        public VanillaPowerExpanded(ModContentPack mod)
        {
            Type type;

            // Gizmos
            // Debug fill/empty
            {
                type = AccessTools.TypeByName("GasNetwork.CompGasStorage");
                // Both these methods are calling (basically) the same method,
                // but that method also has other callers that don't need syncing
                MpCompat.RegisterSyncMethodsByIndex(type, "<CompGetGizmosExtra>", 0, 1).Do(m => m.SetDebugOnly());
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

            // RNG Fix
            {
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
                windDirectionField = AccessTools.Field(AccessTools.TypeByName("GasNetwork.MapComponent_WindDirection"), "windDirection");
                
                PatchingUtilities.PatchUnityRand("GasNetwork.MapComponent_WindDirection:MapGenerated");
                MpCompat.harmony.Patch(AccessTools.Method("GasNetwork.MapComponent_WindDirection:MapComponentTick"),
                    prefix: new HarmonyMethod(typeof(VanillaPowerExpanded), nameof(ReplaceWindMapComponentTick)));
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
                windDirectionField.SetValue(__instance, ((float)windDirectionField.GetValue(__instance) + Rand.Range(-0.3f, 0.3f)) % twoPI);
            }

            return false;
        }
    }
}
