using System.Reflection;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat.Mods
{
    class SaveOurShip2
    {
        public SaveOurShip2(ModContentPack mod)
        {
            LongEventHandler.ExecuteWhenFinished(LatePatch);
        }

        private static void LatePatch()
        {
            // Gizmos
            {
                // Rimworld namespace
                // Ship bridge
                //var type = AccessTools.TypeByName("RimWorld.Building_ShipBridge");

                //MP.RegisterSyncMethod(type, "<GetGizmos>b__7_0");
                //MP.RegisterSyncMethod(type, "<GetGizmos>b__7_1");
                //MP.RegisterSyncMethod(type, "<GetGizmos>b__7_2");
                //MP.RegisterSyncMethod(type, "<GetGizmos>b__7_3");
                //MP.RegisterSyncMethod(type, "<GetGizmos>b__7_4");
                ////MP.RegisterSyncMethod(type, "<GetGizmos>b__7_5") // Rename ship
                //MP.RegisterSyncMethod(type, "<GetGizmos>b__7_6");
                //MP.RegisterSyncMethod(type, "<GetGizmos>b__7_9");
                //MP.RegisterSyncMethod(type, "<GetGizmos>b__7_16");

                //var innerType = AccessTools.Inner(type, "<>c");
                //MP.RegisterSyncMethod(innerType, "<GetGizmos>b__7_8");
                //MP.RegisterSyncMethod(innerType, "<GetGizmos>b__7_10");
                //MP.RegisterSyncMethod(innerType, "<GetGizmos>b__7_11");
                //MP.RegisterSyncMethod(innerType, "<GetGizmos>b__7_12");
                //MP.RegisterSyncMethod(innerType, "<GetGizmos>b__7_13");
                //MP.RegisterSyncMethod(innerType, "<GetGizmos>b__7_14");
                //MP.RegisterSyncMethod(innerType, "<GetGizmos>b__7_15");
                //MP.RegisterSyncMethod(innerType, "<GetGizmos>b__7_17");

                //innerType = AccessTools.Inner(type, "<>c__DisplayClass7_0");
                //MP.RegisterSyncMethod(innerType, "<GetGizmos>b__0");
                //MP.RegisterSyncMethod(innerType, "<GetGizmos>b__7");

                //// Ship turret
                //type = AccessTools.TypeByName("RimWorld.Building_ShipTurret");
                //MP.RegisterSyncMethod(type, "<GetGizmos>b__48_0");
                //MP.RegisterSyncMethod(type, "<GetGizmos>b__48_1");
                ////MP.RegisterSyncMethod(type, "<GetGizmos>b__48_2"); // Not an action, but a check if action is toggled or not - should be skippable
                //MP.RegisterSyncMethod(type, "<GetGizmos>b__48_3");
                //MP.RegisterSyncMethod(type, "<GetGizmos>b__48_4");
                ////MP.RegisterSyncMethod(type, "<GetGizmos>b__48_5"); // Not an action, but a check if action is toggled or not - should be skippable

                //// Target ship command (from ship turret) - is it how it should be patched?
                //MP.RegisterSyncMethod(AccessTools.TypeByName("RimWorld.Command_VerbTargetShip"), "ProcessInput");
                //// Turning shuttles from pawns to building and vice versa
                //type = AccessTools.TypeByName("RimWorld.CompBecomeBuilding");
                ////MP.RegisterSyncWorker<ThingComp>(SyncShuttleTransform, type);
                //MP.RegisterSyncMethod(type, "<CompGetGizmosExtra>b__4_0");
                ////MP.RegisterSyncMethod(AccessTools.TypeByName("RimWorld.CompBecomeBuilding"), "transform");
                //type = AccessTools.TypeByName("RimWorld.CompBecomePawn");
                ////MP.RegisterSyncWorker<ThingComp>(SyncShuttleTransform, type);
                //MP.RegisterSyncMethod(type, "<CompGetGizmosExtra>b__5_0");
                ////MP.RegisterSyncMethod(AccessTools.TypeByName("RimWorld.CompBecomePawn"), "transform");

                //MP.RegisterSyncMethod(AccessTools.TypeByName("RimWorld.CompHibernatableSoS"), "<CompGetGizmosExtra>b__16_0");
                //// Long range scanners
                //MP.RegisterSyncMethod(AccessTools.TypeByName("RimWorld.CompLongRangeMineralScannerSpace"), "<CompGetGizmosExtra>b__11_0");
                //MP.RegisterSyncMethod(AccessTools.TypeByName("RimWorld.CompLongRangeMineralScannerSpaceAI"), "<CompGetGizmosExtra>b__3_0");
                //// Shuttle
                //MP.RegisterSyncMethod(AccessTools.TypeByName("RimWorld.CompShuttleLaunchable"), "<CompGetGizmosExtra>b__12_0");
                //// Ship on the world map
                MP.RegisterSyncMethod(AccessTools.TypeByName("RimWorld.WorldObjectOrbitingShip"), "<GetGizmos>b__9_0");

                // SoS2 namespace
                var type = AccessTools.Inner(AccessTools.TypeByName("OtherGizmoFix"), "<>c__DisplayClass0_0");
                MP.RegisterSyncMethod(type, "<AddTheStuffToTheYieldReturnedEnumeratorThingy>b__0");
                MP.RegisterSyncMethod(type, "<AddTheStuffToTheYieldReturnedEnumeratorThingy>b__1");
                MP.RegisterSyncMethod(type, "<AddTheStuffToTheYieldReturnedEnumeratorThingy>b__2");
            }

            // RNG fix
            {
                var methods = new[]
                {
                    // SoS2 types using Rimworld namespace
                    "RimWorld.ApparelSpaceBelt:DrawWornExtras",
                    // BaseGen/SymbolResolver
                    //"RimWorld.BaseGen.SymbolResolver_DebrisClump:SpawnFloor",
                    //"RimWorld.BaseGen.SymbolResolver_DebrisEdgeStreet:Resolve",
                    //"RimWorld.BaseGen.SymbolResolver_DebrisStreet:SpawnFloor",
                    //"RimWorld.BaseGen.SymbolResolver_EdgeSlag:Resolve",
                    //"RimWorld.BaseGen.SymbolResolver_ExtraShipDoor:Resolve",
                    //"RimWorld.BaseGen.SymbolResolver_FillWithThingsNoClear:Resolve",
                    //"RimWorld.BaseGen.SymbolResolver_Interior_Black_Box:Resolve",
                    //"RimWorld.BaseGen.SymbolResolver_Interior_Cannibal_Barracks:Resolve",
                    //"RimWorld.BaseGen.SymbolResolver_Interior_Salvage_Triangle:Resolve",
                    //"RimWorld.BaseGen.SymbolResolver_Interior_Security_Triangle:Resolve",
                    //"RimWorld.BaseGen.SymbolResolver_Interior_SpaceCrypto:Resolve",
                    //"RimWorld.BaseGen.SymbolResolver_Interior_SpaceDanger:Resolve",
                    //"RimWorld.BaseGen.SymbolResolver_Interior_SpaceEmpty:Resolve",
                    //"RimWorld.BaseGen.SymbolResolver_Interior_SpaceLab:Resolve",
                    //"RimWorld.BaseGen.SymbolResolver_Interior_SpaceMechsAndTurrets:Resolve",
                    //"RimWorld.BaseGen.SymbolResolver_Interior_StorageTriangle:Resolve",
                    //"RimWorld.BaseGen.SymbolResolver_ShipDebris:Resolve",
                    //"RimWorld.BaseGen.SymbolResolver_ShipDebrisEdgeWalls:Resolve",
                    //"RimWorld.BaseGen.SymbolResolver_ShipDebrisPart_Indoors:Resolve",
                    //"RimWorld.BaseGen.SymbolResolver_ShipDebrisPart_Indoors_Division_Split:Resolve",
                    //"RimWorld.BaseGen.SymbolResolver_ShipDebrisPart_Indoors_Leaf_Danger:CanResolve",
                    //"RimWorld.BaseGen.SymbolResolver_ShipDebrisPart_Outdoors:Resolve",
                    //"RimWorld.BaseGen.SymbolResolver_ShipDebrisPart_Outdoors_Division_Grid:Resolve",
                    //"RimWorld.BaseGen.SymbolResolver_ShipDebrisPart_Outdoors_Division_Split:CanResolve",
                    //"RimWorld.BaseGen.SymbolResolver_ShipDebrisPart_Outdoors_Division_Split:Resolve",
                    //"RimWorld.BaseGen.SymbolResolver_ShipDebrisPart_Outdoors_LeafDecorated_EdgeStreet:Resolve",
                    //"RimWorld.BaseGen.SymbolResolver_ShipDebrisPart_Outdoors_LeafDecorated_RandomInnerRect:Resolve",
                    //"RimWorld.BaseGen.SymbolResolver_ShipDebrisPart_Outdoors_LeafPossiblyDecorated:Resolve",
                    //"RimWorld.BaseGen.SymbolResolver_ShipDoors:Resolve",
                    //"RimWorld.BaseGen.SymbolResolver_ShipEnsureCanReachMapEdge:Resolve",
                    // Buildings
                    "RimWorld.Building_SatelliteCore:Destroy",
                    "RimWorld.Building_SatelliteCore:HackMe",
                    "RimWorld.Building_SatelliteCore:RepairMe",
                    //"RimWorld.Building_SatelliteCore:DispenseTargeters", // Only called from Building_SatelliteCore:HackMe
                    "RimWorld.Building_SatelliteCore:TickRare",
                    "RimWorld.Building_ShipTurret:TryStartShootSomething",
                    // Comps
                    "RimWorld.CompBlackBoxAI:PersuadeMe",
                    "RimWorld.CompBlackBoxConsole:HackMe",
                    "RimWorld.CompDamagedReactor:CompTick",
                    "RimWorld.CompLongRangeMineralScannerSpace:Used",
                    "RimWorld.CompLongRangeMineralScannerSpace:FoundMinerals",
                    // Thing
                    "RimWorld.DetachedShipPart:EmitSmokeAndFlame",
                    // GenSteps
                    //"RimWorld.GenStep_HackableSatellite:SeedPart", // Property, does it need patching?
                    //"RimWorld.GenStep_HackableSatellite:ScatterAt",
                    //"RimWorld.GenStep_ShipDebris:SeedPart", // Property, does it need patching?
                    //"RimWorld.GenStep_ShipDebris:ScatterAt",
                    //"RimWorld.GenStep_ShipEngineImpactSite:ScatterAt",
                    //"RimWorld.GenStep_ValuableAsteroids:SeedPart", // Property, does it need patching?
                    //"RimWorld.GenStep_ValuableAsteroids:Generate",
                    //"RimWorld.GenStep_ValuableAsteroids:FastLump",
                    // QuestGen
                    "RimWorld.QuestGen.QuestNode_GenerateSpaceSite:RunInt",
                    // Ship combat
                    "RimWorld.ShipCombatManager:SpawnEnemyShip",
                    "RimWorld.ShipCombatManager:Tick",
                    "RimWorld.ShipCombatManager:FindClosestEdgeCell",
                    "RimWorld.ShipCombatManager:SalvageThing",
                    // Verb
                    "RimWorld.Verb_LaunchProjectileShip:TryCastShot",
                    // types using SoS2 namespaces
                    "SaveOurShip2.DontRegenerateHiddenFactions:Replace",
                    "SaveOurShip2.IncomingPodFix:PatchThat",
                    "SaveOurShip2.NoQuestsNearTileZero:CheckNonZeroTile",
                    "SaveOurShip2.NoRaidsFromPreviousPlanets:Replace",
                    "SaveOurShip2.ShipInteriorMod2:GenerateImpactSite",
                    "SaveOurShip2.WorldSwitchUtility:KillAllColonistsNotInCrypto",
                };

                PatchingUtilities.PatchPushPopRand(methods);
            }
        }

        //private static void SyncShuttleTransform(SyncWorker sync, ref ThingComp thingComp)
        //{
        //    var traverse = Traverse.Create(thingComp);

        //    var propsField = traverse.Field("props");
        //    var parentField = traverse.Field("parent");

        //    if (sync.isWriting)
        //    {
        //        sync.Write(propsField.GetValue<CompProperties>());
        //        sync.Write(parentField.GetValue<ThingComp>());
        //    }
        //    else
        //    {
        //        propsField.SetValue(sync.Read<CompProperties>());
        //        parentField.SetValue(sync.Read<ThingComp>());
        //    }
        //}
    }
}
