using System;

using HarmonyLib;
using Multiplayer.API;
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
        public VanillaPowerExpanded(ModContentPack mod)
        {
            Type type;

            /// Gizmos
            // Debug fill/empty
            {
                type = AccessTools.TypeByName("GasNetwork.CompGasStorage");
                // Both these methods are calling (basically) the same method,
                // but that method also has other callers that don't need syncing
                MP.RegisterSyncMethod(type, "<CompGetGizmosExtra>b__15_0");
                MP.RegisterSyncMethod(type, "<CompGetGizmosExtra>b__15_1");

                type = AccessTools.TypeByName("VanillaPowerExpanded.CompPipeTank");
                // This method is only called by 2 gizmos and nothing else (as far as I can tell)
                MP.RegisterSyncMethod(type, "SetStoredEnergyPct");
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
                    "VanillaPowerExpanded.CompPowerPlantNuclear:AffectCell",
                    "VanillaPowerExpanded.Building_GasGeyser:StartSpray",
                    "VanillaPowerExpanded.GasExplosionUtility:TryStartFireNear",
                    "VanillaPowerExpanded.IncidentWorker_GasExplosion:TryExecuteWorker",
                    "VanillaPowerExpanded.IntermittentGasSprayer:SteamSprayerTick",
                    "VanillaPowerExpanded.IntermittentGasSprayer:ThrowAirPuffUp",
                    // NewBaseAirPuff is only called by IntermittentGasSprayer:ThrowAirPuffUp, no need for patching
                    "VanillaPowerExpanded.GasPipeNet:PowerNetTick",
                    "GasNetwork.GasNet:GasNetTick",
                    // PipeNetGrid pushes and pops all Rand calls, no need to patch
                    // CompPowerAdvancedWater:RebuildCache is only calling a seeded random
                };

                // These methods are loading resources in their .ctor, must be patched later
                var methodsForLater = new[]
                {
                    //"VanillaPowerExpanded.Building_Tank:Tick",
                    //"VanillaPowerExpanded.Building_Tank:PostApplyDamage",
                    "VanillaPowerExpanded.CompPowerAdvancedWater:PostSpawnSetup",
                    "VanillaPowerExpanded.CompPowerAdvancedWind:PostSpawnSetup",
                };

                PatchingUtilities.PatchPushPopRand(methods);
                LongEventHandler.ExecuteWhenFinished(() => PatchingUtilities.PatchPushPopRand(methodsForLater));
            }
        }
    }
}
