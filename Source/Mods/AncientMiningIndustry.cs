using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat;

/// <summary>Ancient mining industry by MO</summary>
/// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3141472661"/>
[MpCompatFor("XMB.AncientMiningIndustry.MO")]
public class AncientMiningIndustry
{
    private static SyncType questScriptPairType;

    public AncientMiningIndustry(ModContentPack mod)
    {
        // Turn on/off (0), build walls on/off (1)
        MpCompat.RegisterLambdaMethod("AncientMining.Building_BoringMachine", nameof(Thing.GetGizmos), 0, 1);

        // Dev produce portion (0), shutdown (1)
        MpCompat.RegisterLambdaMethod("AncientMining.CompDeepDrillAutomated", nameof(ThingComp.CompGetGizmosExtra), 0, 1).SetDebugOnly();

        // No quest (1), specific quest (3) 
        MpCompat.RegisterLambdaDelegate("AncientMining.CompQuestScanner", nameof(ThingComp.CompGetGizmosExtra), 1, 3).SetContext(SyncContext.MapSelected);

        // A bit of a "hacky" solution. It'll sync the field by exposing it, instead of setting it
        // to a specific reference from the comp's props. However, due to the way it's handled, it
        // should work completely fine - the reference itself doesn't matter, only its contents.
        // An alternative would be to make a sync worker that searches all `ThingDef`s for quest
        // scanner props, checks if it contains that specific item in its list of possible quests,
        // and then syncs the def and position of that object on the list.
        // Side note: this could be done using ISyncDelegate.ExposeFields - however,
        // seems I may have left a bug behind in that code, so it'll need to be fixed first.
        questScriptPairType = AccessTools.TypeByName("AncientMining.QuestScriptTexPathPair");
        questScriptPairType.expose = true;

        MP.RegisterSyncWorker<object>(SyncQuestScriptTexPathPair, questScriptPairType.type);
    }

    private static void SyncQuestScriptTexPathPair(SyncWorker sync, ref object obj) => sync.Bind(ref obj, questScriptPairType);
}