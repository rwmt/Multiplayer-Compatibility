using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Multiplayer.API;
using RimWorld.Planet;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>ResearchPowl by VinaLx, Owlchemist</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2877856030"/>
    [MpCompatFor("Owlchemist.ResearchPowl")]
    public class ResearchPowl
    {
        // Queue
        private static FastInvokeHandler queueSanityCheck;
        // ResearchNode
        private static FastInvokeHandler researchNodeGetAvailable;
        private static AccessTools.FieldRef<object, ResearchProjectDef> researchNodeProjectField;
        // Tree
        private static FastInvokeHandler treeGetResearchNodes;
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
                "Insert",

                // Honestly, syncing it just in case something ends up calling it and causing issues
                "UpdateCurrentResearch",
            };

            queueSanityCheck = MethodInvoker.GetHandler(AccessTools.DeclaredMethod(type, "SanityCheck"));
            MP.RegisterSyncMethod(typeof(ResearchPowl), nameof(SyncedSanityCheck));
            foreach (var method in methods) 
                MP.RegisterSyncMethod(type, method);
            // 3 reasons for patching the "SanityCheck" to sync it manually:
            // - I'm unsure if modifying List<ResearchNode> _queue without syncing it causes issues, so better be safe
            // - Optimize syncing, as instead of syncing it once per each research project remove it'll only sync it once ever
            // - UpdateCurrentResearch() call wasn't synced (it is now, but it wasn't always the case and could cause issues)
            MpCompat.harmony.Patch(AccessTools.DeclaredMethod(type, "SanityCheck"),
                prefix: new HarmonyMethod(typeof(ResearchPowl), nameof(PreSanityCheck)));

            type = AccessTools.TypeByName("ResearchPowl.ResearchNode");
            researchNodeProjectField = AccessTools.FieldRefAccess<ResearchProjectDef>(type, "Research");
            researchNodeGetAvailable = MethodInvoker.GetHandler(AccessTools.DeclaredMethod(type, "GetAvailable"));
            MP.RegisterSyncWorker<object>(SyncResearchNode, type);

            type = AccessTools.TypeByName("ResearchPowl.Tree");
            treeGetResearchNodes = MethodInvoker.GetHandler(AccessTools.Method(type, "ResearchNodes"));
            MpCompat.harmony.Patch(AccessTools.Method(type, "ResearchNodes"),
                prefix: new HarmonyMethod(typeof(ResearchPowl), nameof(PreInitializeNodesStructures)));
        }

        private static void PreInitializeNodesStructures() => nodeCache = null;

        // Will cause PostInitializeNodesStructures to be called
        private static void ForceInitializeCache()
        {
            nodeCache = ((IList)treeGetResearchNodes(null))
                .Cast<object>()
                .ToDictionary(node => researchNodeProjectField(node));
        }

        private static void SyncResearchNode(SyncWorker sync, ref object node)
        {
            if (sync.isWriting)
                sync.Write(researchNodeProjectField(node));
            else
            {
                var project = sync.Read<ResearchProjectDef>();

                if (nodeCache == null)
                    ForceInitializeCache();

                node = nodeCache![project];
            }
        }

        private static bool PreSanityCheck(WorldComponent __instance, IList ____queue)
        {
            // Let it run normally in SP or in synced commands (or other contexts not needing syncing)
            if (!PatchingUtilities.ShouldCancel)
                return true;

            foreach (var node in ____queue)
            {
                var def = researchNodeProjectField(node);

                if (def.IsFinished || !(bool)researchNodeGetAvailable(node))
                {
                    SyncedSanityCheck(__instance);
                    return false;
                }
            }

            return false;
        }

        private static void SyncedSanityCheck(WorldComponent queue) => queueSanityCheck(queue);
    }
}