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
        MP.RegisterSyncMethod(AccessTools.Method("RandomsGeneAssistant.PatchGeneAssemblerEject:EjectDuplicateGenepacks"));
    }
}