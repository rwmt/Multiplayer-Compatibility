using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>ResearchPowl by VinaLx, Owlchemist</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2877856030"/>
    [MpCompatFor("Owlchemist.ResearchPowl")]
    public class ResearchPowl
    {
        private static FastInvokeHandler getResearchNodes;
        private static AccessTools.FieldRef<object, ResearchProjectDef> nodeProjectField;
        private static Dictionary<ResearchProjectDef, object> nodeCache;

        public ResearchPowl(ModContentPack mod)
        {
            var type = AccessTools.TypeByName("ResearchPowl.Queue");
            var methods = new[]
            {
                // modifies _queue
                "UnsafeConcat",
                "UnsafeInsert",
                "Remove",
                "Replace",
                "ReplaceMore",
                "Finish",
                "DoMove",
                
                // calls UpdateCurrentResearch
                "UnsafeAppend",
                "Prepend",
            };

            foreach (var method in methods) 
                MP.RegisterSyncMethod(type, method);

            type = AccessTools.TypeByName("ResearchPowl.ResearchNode");
            nodeProjectField = AccessTools.FieldRefAccess<ResearchProjectDef>(type, "Research");
            MP.RegisterSyncWorker<object>(SyncResearchNode, type);

            type = AccessTools.TypeByName("ResearchPowl.Tree");
            getResearchNodes = MethodInvoker.GetHandler(AccessTools.Method(type, "ResearchNodes"));
            MpCompat.harmony.Patch(AccessTools.Method(type, "ResearchNodes"),
                prefix: new HarmonyMethod(typeof(ResearchPowl), nameof(PreInitializeNodesStructures)));
        }

        private static void PreInitializeNodesStructures() => nodeCache = null;

        // Will cause PostInitializeNodesStructures to be called
        private static void ForceInitializeCache()
        {
            nodeCache = ((IList)getResearchNodes(null))
                .Cast<object>()
                .ToDictionary(node => nodeProjectField(node));
        }

        private static void SyncResearchNode(SyncWorker sync, ref object node)
        {
            if (sync.isWriting)
                sync.Write(nodeProjectField(node));
            else
            {
                var project = sync.Read<ResearchProjectDef>();

                if (nodeCache == null)
                    ForceInitializeCache();

                node = nodeCache![project];
            }
        }
    }
}