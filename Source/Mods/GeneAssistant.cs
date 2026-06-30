using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat;

/// <summary>Gene Assistant by RandomCoughDrop</summary>
/// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3141472661"/>
[MpCompatFor("rimworld.randomcoughdrop.geneassistant")]
public class GeneAssistant
{
    public GeneAssistant(ModContentPack mod)
    {
        // Eject Genepacks
        var type = AccessTools.TypeByName("RandomsGeneAssistant.PatchGeneAssemblerEject");
        MP.RegisterSyncMethod(AccessTools.Method(type, "EjectDuplicateGenepacks"));
        MP.RegisterSyncMethod(AccessTools.Method(type, "EjectCosmeticGenepacks"));
        MP.RegisterSyncMethod(AccessTools.Method(type, "EjectCombinedGenepacks"));
    }
}