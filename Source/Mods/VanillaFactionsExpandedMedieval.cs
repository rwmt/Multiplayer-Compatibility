using System.Reflection;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Vanilla Furniture Expanded - Power by Oskar Potocki and XeoNovaDan </summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2023513450"/>
    /// <see href="https://github.com/AndroidQuazar/VanillaFactionsExpandedMedieval"/>
    /// Contribution to Multiplayer Compatibility by Sokyran and Reshiram
    [MpCompatFor("OskarPotocki.VanillaFactionsExpanded.MedievalModule")]
    class VFEMedieval
    {
        private static FieldInfo qualityField;

        public VFEMedieval(ModContentPack mod)
        {
            // Gizmos
            {
                // Select wine quality
                var outerType = AccessTools.TypeByName("VFEMedieval.Command_SetTargetWineQuality");
                var innerType = AccessTools.Inner(outerType, "<>c__DisplayClass1_0");
                qualityField = AccessTools.Field(innerType, "quality");
                var method = AccessTools.Method(innerType, "<ProcessInput>b__1");

                MP.RegisterSyncMethod(method);
                MP.RegisterSyncWorker<object>(SyncWineBarrel, innerType, shouldConstruct: true);
                // Debug age wine by 1 day
                MP.RegisterSyncMethod(AccessTools.Method(AccessTools.TypeByName("VFEMedieval.CompWineFermenter"), "<CompGetGizmosExtra>b__46_0"));
            }

            //RNG Fix
            {
                var methods = new[] {
                    //"VFEMedieval.SymbolResolver_AddMustToWineBarrels:Resolve",
                    //"VFEMedieval.SymbolResolver_CastleEdgeSandbags:Resolve",
                    //// GenerateSandbags and TrySpawnSandbags have call hierarchy originating from the previous method
                    //"VFEMedieval.SymbolResolver_CastleEdgeWalls:Resolve",
                    //// TryGenerateTower and TrySpawnWall have call hierarchy originating from the previous method
                    //"VFEMedieval.SymbolResolver_Interior_Winery:Resolve",
                    //"VFEMedieval.SymbolResolver_MedievalEdgeDefense:Resolve",
                    //"VFEMedieval.SymbolResolver_MedievalSettlement:Resolve",
                    //"VFEMedieval.GenStep_CastleRuins:ScatterAt",
                    "VFEMedieval.IncidentWorker_BlackKnight:PostProcessGeneratedPawnsAfterSpawning",
                    "VFEMedieval.IncidentWorker_QuestMedievalTournament:TryExecuteWorker",
                    "VFEMedieval.IncidentWorker_QuestMedievalTournament:TryFindFaction", // Called by TryExecuteWorker and CanFireNowSub
                    "VFEMedieval.IncidentWorker_QuestMedievalTournament:TryFindTile", // Called by TryExecuteWorker and CanFireNowSub
                    "VFEMedieval.StockGenerator_MedievalMercs:GenerateThings",
                    "VFEMedieval.BlackKnightUtility:TryDoFleshWound",
                    // MedievalTournamentUtility:GenerateCompetitors is only called by methods in MedievalTournament, which we are patching instead
                    // MedievalTournamentUtility:GenerateRewards is only called by IncidentWorker_QuestMedievalTournament:TryExecuteWorker, which we're patching already
                    // All ResolveDisaster methods have call hierarchy originating from MedievalTournament:DoTournament
                    "VFEMedieval.MedievalTournament:Notify_CaravanArrived",
                    "VFEMedieval.MedievalTournament:DoTournament",
                };

                PatchingUtilities.PatchPushPopRand(methods);
            }
        }

        private static void SyncWineBarrel(SyncWorker sync, ref object obj)
        {
            if (sync.isWriting)
                sync.Write((byte)qualityField.GetValue(obj));
            else
                qualityField.SetValue(obj, sync.Read<byte>());
        }
    }
}
