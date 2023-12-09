using System.Collections.Generic;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using SaveOurShip2;
using UnityEngine;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Save Our Ship 2 by Thain, Kentington</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=1909914131"/>
    [MpCompatFor("kentington.saveourship2")]
    internal class SaveOurShip2
    {
        private static AccessTools.FieldRef<object, bool> toggleShieldAnyShieldOnField;
        private static AccessTools.FieldRef<object, Building_ShipBridge> toggleShieldParentClassField;
        private static object bridgeInnerClassStaticField;

        private static Dictionary<int, Thing> thingsById;

        public SaveOurShip2(ModContentPack mod) => LongEventHandler.ExecuteWhenFinished(LatePatch);

        private static void LatePatch()
        {
            thingsById = (Dictionary<int, Thing>)AccessTools.Field(AccessTools.TypeByName("Multiplayer.Client.ThingsById"), "thingsById").GetValue(null);

            // Map rendering fix
            {
                var type = AccessTools.TypeByName("Multiplayer.Client.MapDrawerRegenPatch");
                MpCompat.harmony.Patch(AccessTools.Method(type, "Prefix"),
                    prefix: new HarmonyMethod(typeof(SaveOurShip2), nameof(CancelMapDrawerRegenPatch)));
            }

            // Ship bridge
            {
                // Launching ship
                MP.RegisterSyncMethod(typeof(Building_ShipBridge), nameof(Building_ShipBridge.TryLaunch));
                MpCompat.harmony.Patch(AccessTools.Method(typeof(Building_ShipBridge), nameof(Building_ShipBridge.TryLaunch)),
                    postfix: new HarmonyMethod(typeof(SaveOurShip2), nameof(PostTryLaunch)));

                // Capturing ship
                MP.RegisterSyncMethod(typeof(Building_ShipBridge), nameof(Building_ShipBridge.CaptureShip));

                // Toggle shields, cloak
                var toggleShields = MpMethodUtil.GetLambda(typeof(Building_ShipBridge), nameof(Building_ShipBridge.GetGizmos), MethodType.Normal, null, 8);
                var toggleCloak = MpMethodUtil.GetLambda(typeof(Building_ShipBridge), nameof(Building_ShipBridge.GetGizmos), MethodType.Normal, null, 10);

                MP.RegisterSyncDelegate(typeof(Building_ShipBridge), toggleShields.DeclaringType.Name, toggleShields.Name);
                MP.RegisterSyncMethod(toggleCloak);

                MpCompat.harmony.Patch(toggleShields,
                    prefix: new HarmonyMethod(typeof(SaveOurShip2), nameof(CacheShields)));
                MpCompat.harmony.Patch(toggleCloak,
                    prefix: new HarmonyMethod(typeof(SaveOurShip2), nameof(CacheCloaks)));

                toggleShieldAnyShieldOnField = AccessTools.FieldRefAccess<bool>(toggleShields.DeclaringType, "anyShieldOn");
                toggleShieldParentClassField = AccessTools.FieldRefAccess<Building_ShipBridge>(toggleShields.DeclaringType, "<>4__this");

                // showReport - could cause issues, as it recalculates stuff?
                // Go to new world - left for later
                // Move ship, dev mode move ship flip, land ship - left for later

                // Lambda methods
                var delegates = new[]
                {
                    15, // Attack KSS Horizon
                    16, // Salvage enemy ship
                    18, // Cancel salvage
                    // Combat
                    22, // Retreat
                    23, // Maintain distance
                    24, // Stop movement
                    25, // Advance
                    27, // Emergency long range jump
                    29, // Emergency short range jump
                    31, // Escape combat
                    // 35, // Capture ship - calls a method which we sync instead
                };
                MpCompat.RegisterLambdaMethod(typeof(Building_ShipBridge), nameof(Building_ShipBridge.GetGizmos), delegates);

                // Dev mode lambda methods
                delegates = new[]
                {
                    19, // Start battle
                    21, // Remove all previously visited worlds
                    // Combat
                    32, // Retreat enemy
                    33, // Stop enemy movement
                    34, // Advance enemy
                };

                MpCompat.RegisterLambdaMethod(typeof(Building_ShipBridge), nameof(Building_ShipBridge.GetGizmos), delegates).SetDebugOnly();

                // Lambda delegates
                delegates = new[]
                {
                    36, // Attack trade ship
                    37, // Attack ship
                    38, // Approach derelict ship
                };

                MpCompat.RegisterLambdaDelegate(typeof(Building_ShipBridge), nameof(Building_ShipBridge.GetGizmos),
                    delegates).SetContext(SyncContext.CurrentMap);

                // Renaming the ship creates a dialog, syncing it instead
                MP.RegisterSyncWorker<Dialog_NameShip>(SyncDialog_NameShip);
                MP.RegisterSyncMethod(typeof(Dialog_NameShip), nameof(Dialog_NameShip.SetName));

                // (Dev) loading a ship with specific def is the same deal as renaming the ship
                MP.RegisterSyncWorker<Dialog_LoadShipDef>(SyncDialog_LoadShipDef);
                MP.RegisterSyncMethod(typeof(Dialog_LoadShipDef), nameof(Dialog_LoadShipDef.SetName));

                var type = AccessTools.Inner(typeof(Building_ShipBridge), "<>c");
                MP.RegisterSyncWorker<object>(SyncShipBridgeInnerClass, type);
                bridgeInnerClassStaticField = AccessTools.Field(type, "<>9").GetValue(null);
            }

            // Building_ShipTurret
            {
                // Extract shells
                MP.RegisterSyncMethod(typeof(Building_ShipTurret), nameof(Building_ShipTurret.ExtractShells));

                var delegates = new[]
                {
                    0, // Stop forced attack
                    1, // Hold fire
                    4, // Toggle point defense
                };

                MpCompat.RegisterLambdaMethod(typeof(Building_ShipTurret), nameof(Building_ShipTurret.GetGizmos), delegates);

                // Select target
                MP.RegisterSyncMethod(typeof(SaveOurShip2), nameof(SyncedSelectTarget));
                MpCompat.harmony.Patch(
                    MpMethodUtil.GetLambda(typeof(Command_VerbTargetShip), nameof(Command_VerbTargetShip.ProcessInput), lambdaOrdinal: 0),
                    prefix: new HarmonyMethod(typeof(SaveOurShip2), nameof(PreProcessInput)));
            }

            // Other buildings
            {
                // Float menus to check and do:
                // Building_SatelliteCore
                // Building_ShipAirlock
                // Building_ShipBridge

                // Advanced sensors
                MP.RegisterSyncMethod(typeof(Building_ShipAdvSensor), nameof(Building_ShipAdvSensor.ChoseWorldTarget)); // Scan map, called from WorldTargeter
                MpCompat.RegisterLambdaMethod(typeof(Building_ShipAdvSensor), nameof(Building_ShipBridge.GetGizmos), 1); // Stop scanning

                // Ship vent, toggle heat with power
                MpCompat.RegisterLambdaMethod(typeof(Building_ShipVent), nameof(Building_ShipBridge.GetGizmos), 0);
            }

            // Comps
            {
                // Float menus to check and do:
                // CompBlackBoxAI
                // CompBlackBoxConsole
                // CompHologramRelay, from gizmos

                // Convert hull into archotech hull
                MpCompat.RegisterLambdaMethod(typeof(CompArchoHullConversion), nameof(CompArchoHullConversion.CompGetGizmosExtra), 0);

                // Become Pawn/Building
                MpCompat.RegisterLambdaMethod(typeof(CompBecomeBuilding), nameof(CompBecomePawn.CompGetGizmosExtra), 0);
                MP.RegisterSyncMethod(typeof(CompBecomePawn), nameof(CompBecomePawn.transform));

                // CompBuildingConsciousness
                var delegates = new[]
                {
                    0,  // Spawn hologram
                    1,  // Despawn hologram
                    2,  // Generate and spawn hologram
                    15, // Install core
                };
                MpCompat.RegisterLambdaMethod(typeof(CompBuildingConsciousness), nameof(CompBuildingConsciousness.CompGetGizmosExtra), delegates);

                delegates = new[]
                {
                    4,  // Set color
                    7,  // Set drug
                    9,  // Set apparel
                    12, // Select pawn (dead or alive)
                    13, // Select pawn (only dead)
                };
                MpCompat.RegisterLambdaDelegate(typeof(CompBuildingConsciousness), nameof(CompBuildingConsciousness.CompGetGizmosExtra), delegates);

                // Float menus from gizmos: 3, 6, 8, 11

                MP.RegisterSyncWorker<Dialog_NameAI>(SyncDialog_NameAi);

                // CompHibernatableSoS
                // public method, not used anywhere else but in a dialog inside of a gizmo
                MP.RegisterSyncMethod(typeof(CompHibernatableSoS), nameof(CompHibernatableSoS.Startup));

                // Long range mineral scanner
                delegates = new[]
                {
                    0, // Toggle scan sites
                    2, // Toggle scan ships
                };
                MpCompat.RegisterLambdaMethod(typeof(CompLongRangeMineralScannerSpace),
                    nameof(CompLongRangeMineralScannerSpace.CompGetGizmosExtra), delegates);

                // Dev mode find now gizmo
                MpCompat.RegisterLambdaMethod(typeof(CompLongRangeMineralScannerSpace),
                    nameof(CompLongRangeMineralScannerSpace.CompGetGizmosExtra), 4).SetDebugOnly();

                // Set power overdrive setting
                MP.RegisterSyncMethod(typeof(CompPowerTraderOverdrivable), nameof(CompPowerTraderOverdrivable.FlickOverdrive));

                // Change shield size
                MP.RegisterSyncMethod(typeof(CompShipCombatShield), nameof(CompShipCombatShield.ChangeShieldSize));
                MpCompat.RegisterLambdaMethod(typeof(CompShipCombatShield), nameof(CompShipCombatShield.CompGetGizmosExtra), 2);

                // Purge heat
                MpCompat.RegisterLambdaMethod(typeof(CompShipHeatPurge), nameof(CompShipHeatPurge.CompGetGizmosExtra), 0);

                // Comp shuttle
                // TODO: do the map launching
                MpCompat.RegisterLambdaDelegate(typeof(CompShuttleLaunchable), nameof(CompShuttleLaunchable.CompGetGizmosExtra), 3);
            }

            // Caravan shuttle gizmos
            {
                MP.RegisterSyncMethod(typeof(ShuttleCaravanUtility), nameof(ShuttleCaravanUtility.RefuelMe));
                MP.RegisterSyncMethod(typeof(ShuttleCaravanUtility), nameof(ShuttleCaravanUtility.ActivateMe));
                MP.RegisterSyncMethod(typeof(ShuttleCaravanUtility), nameof(ShuttleCaravanUtility.TryLaunch)).ExposeParameter(1);
            }

            // World object - orbiting ship
            {
                // Abandon ship
                MP.RegisterSyncMethod(typeof(WorldObjectOrbitingShip), nameof(WorldObjectOrbitingShip.Abandon), new SyncType[] { typeof(WorldObjectOrbitingShip) });

                // Ship movement
                var delegates = new[]
                {
                    5, // Move ship west (far)
                    6, // Move ship west
                    7, // Move ship east (far)
                    8, // Move ship east
                };
                MpCompat.RegisterLambdaMethod(typeof(WorldObjectOrbitingShip), nameof(WorldObjectOrbitingShip.GetGizmos), delegates);

                // Dev mode gizmos
                delegates = new[]
                {
                    9,
                    10
                };
                MpCompat.RegisterLambdaMethod(typeof(WorldObjectOrbitingShip), nameof(WorldObjectOrbitingShip.GetGizmos), delegates).SetDebugOnly();
            }
        }

        private static void SyncDialog_NameShip(SyncWorker sync, ref Dialog_NameShip dialog)
        {
            if (sync.isWriting) sync.Write(dialog.ship);
            else dialog = new Dialog_NameShip(sync.Read<Building_ShipBridge>());
        }

        private static void SyncDialog_LoadShipDef(SyncWorker sync, ref Dialog_LoadShipDef dialog)
        {
            if (sync.isWriting)
            {
                sync.Write(dialog.ship);
                sync.Write(dialog.mapi);
            }
            else dialog = new Dialog_LoadShipDef(sync.Read<string>(), sync.Read<Map>());
        }

        private static void SyncDialog_NameAi(SyncWorker sync, ref Dialog_NameAI dialog)
        {
            if (sync.isWriting) sync.Write(dialog.AI);
            else dialog = new Dialog_NameAI(sync.Read<ThingComp>() as CompBuildingConsciousness);
        }

        private static void CacheCloaks(Building_ShipBridge __instance)
        {
            if (!MP.IsInMultiplayer) return;

            __instance.anyCloakOn = false;
            __instance.cachedShipParts = ShipUtility.ShipBuildingsAttachedTo(__instance);
            __instance.cachedCloaks = new List<Building>();

            foreach (var building in __instance.cachedShipParts)
            {
                if (building is Building_ShipCloakingDevice)
                {
                    __instance.cachedCloaks.Add(building);
                    if (building.TryGetComp<CompFlickable>().SwitchIsOn)
                        __instance.anyCloakOn = true;
                }
            }
        }

        private static void CacheShields(object __instance)
        {
            if (!MP.IsInMultiplayer) return;

            var parent = toggleShieldParentClassField();

            var anyShieldOn = false;
            parent.cachedShipParts = ShipUtility.ShipBuildingsAttachedTo(parent);
            parent.cachedShields = new List<Building>();

            foreach (var building in parent.cachedShipParts)
            {
                if (building.TryGetComp<CompShipCombatShield>() != null)
                {
                    parent.cachedShields.Add(building);
                    if (building.TryGetComp<CompFlickable>().SwitchIsOn)
                        anyShieldOn = true;
                }
            }

            toggleShieldAnyShieldOnField(__instance) = anyShieldOn;
        }

        private static bool PreProcessInput(Command_VerbTargetShip __instance, LocalTargetInfo x)
        {
            if (MP.IsInMultiplayer) return true;

            if (__instance.turrets == null || !__instance.turrets.Any()) return false;
            if (x.Thing == null && !x.Cell.IsValid) return false;

            SyncedSelectTarget(__instance.turrets, x.Thing?.thingIDNumber ?? -1, x.Cell);

            return false;
        }

        private static void SyncedSelectTarget(List<Building_ShipTurret> turrets, int id, IntVec3 cell)
        {
            LocalTargetInfo target;

            if (id >= 0)
                target = new LocalTargetInfo(thingsById.GetValueSafe(id));
            else target = new LocalTargetInfo(cell);

            foreach (var turret in turrets) turret.SetTarget(target);
        }

        private static void SyncShipBridgeInnerClass(SyncWorker sync, ref object obj)
        {
            if (!sync.isWriting) obj = bridgeInnerClassStaticField;
        }

        // Stop MP from caching/restoring the map, as SoS2 does its own thing with it
        private static bool CancelMapDrawerRegenPatch(ref bool __result, [HarmonyArgument("__instance")] MapDrawer instance)
        {
            if (!MP.IsInMultiplayer || instance.map.Biome != ShipInteriorMod2.OuterSpaceBiome) 
                return true;
            
            __result = true;
            return false;

        }

        private static void PostTryLaunch()
        {
            if (!ShipCountdown.CountingDown || !MP.IsInMultiplayer)
                return;
            // I think MP does something which messes with stuff,
            // so we stop manually instead of calling the countdown stop method.
            
            ShipCountdown.timeLeft = -1000f;
            ScreenFader.SetColor(Color.clear);
            // TODO: Find a way to do it non-instantly that would work in a synced way
            SaveShip.SaveShipAndRemoveItemStacks();
        }
    }
}
