using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    [MpCompatFor("VanillaExpanded.VFEPower")]
    class VanillaPowerExpanded
    {
        /// <summary>Vanilla Furniture Expanded - Power by Oskar Potocki and Sarg Bjornson</summary>
        /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2062943477"/>
        /// <see href="https://github.com/AndroidQuazar/VanillaFurnitureExpanded-Power"/>
        /// Contribution to Multiplayer Compatibility by Sokyran and Reshiram
        public VanillaPowerExpanded(ModContentPack mod)
        {
            LongEventHandler.ExecuteWhenFinished(LatePatch);
        }

        private void LatePatch()
        {
            // Gizmos
            {
                // Debug fill/empty
                var type = AccessTools.TypeByName("VanillaPowerExpanded.CompPipeTank");
                MP.RegisterSyncMethod(type, "<CompGetGizmosExtra>b__17_0");
                MP.RegisterSyncMethod(type, "<CompGetGizmosExtra>b__17_1");

                // Order to or cancel plugging the hole (method names shared by both types)
                foreach (var typeName in new[] { "VanillaPowerExpanded.Building_GasGeyser", "VanillaPowerExpanded.Building_GasGeyser" })
                {
                    type = AccessTools.TypeByName(typeName);
                    MP.RegisterSyncMethod(type, "SetHoleForPlugging");
                    MP.RegisterSyncMethod(type, "CancelHoleForPlugging");
                }
            }

            // RNG Fix
            {
                var methods = new[]
                {
                    AccessTools.Method("VanillaPowerExpanded.Building_SmallBattery:Tick"),
                    AccessTools.Method("VanillaPowerExpanded.Building_SmallBattery:PostApplyDamage"),
                    AccessTools.Method("VanillaPowerExpanded.WeatherEvent_CustomLightningStrike:FireEvent"),
                    AccessTools.Method("VanillaPowerExpanded.MapComponentExtender:doMapSpawns"),
                    AccessTools.Method("VanillaPowerExpanded.CompPlantHarmRadiusIfBroken:CompTick"),
                    // HarmRandomPlantInRadius is only called by CompPlantHarmRadiusIfBroken:CompTick, no need for patching
                    AccessTools.Method("VanillaPowerExpanded.CompPowerPlantNuclear:AffectCell"),
                    AccessTools.Method("VanillaPowerExpanded.Building_GasGeyser:StartSpray"),
                    AccessTools.Method("VanillaPowerExpanded.GasExplosionUtility:TryStartFireNear"),
                    AccessTools.Method("VanillaPowerExpanded.IncidentWorker_GasExplosion:TryExecuteWorker"),
                    AccessTools.Method("VanillaPowerExpanded.IntermittentGasSprayer:SteamSprayerTick"),
                    AccessTools.Method("VanillaPowerExpanded.IntermittentGasSprayer:ThrowAirPuffUp"),
                    // NewBaseAirPuff is only called by IntermittentGasSprayer:ThrowAirPuffUp, no need for patching
                    AccessTools.Method("VanillaPowerExpanded.GasPipeNet:PowerNetTick"),
                    // PipeNetGrid pushes and pops all Rand calls, no need to patch
                    // CompPowerAdvancedWater:RebuildCache is only calling a seeded random
                    AccessTools.Method("VanillaPowerExpanded.Building_Tank:Tick"), // Causes an error and a (rather minor) issue with graphics when patched early
                    AccessTools.Method("VanillaPowerExpanded.Building_Tank:PostApplyDamage"), // Causes an error and a (rather minor) issue with graphics when patched early
                    AccessTools.Method("VanillaPowerExpanded.CompPowerAdvancedWater:PostSpawnSetup"), // Causes an error and issue with graphics when patched early
                    AccessTools.Method("VanillaPowerExpanded.CompPowerAdvancedWind:PostSpawnSetup"), // Causes an error and issue with graphics when patched early
                };

                foreach (var method in methods)
                    PatchingUtilities.PatchPushPopRand(method);
            }
        }
    }
}
