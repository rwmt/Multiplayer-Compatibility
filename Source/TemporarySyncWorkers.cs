using System;
using Multiplayer.API;
using RimWorld;
using Verse;

namespace Multiplayer.Compat
{
    public static class TemporarySyncWorkers
    {
        private static bool isIGeneResourceDrainInitialized = false;
        
        public static void RegisterIGeneResourceDrain()
        {
            if (isIGeneResourceDrainInitialized)
                return;
            
            MP.RegisterSyncWorker<IGeneResourceDrain>(SyncIGeneResourceDrain);
            isIGeneResourceDrainInitialized = true;
        }

        private static void SyncIGeneResourceDrain(SyncWorker sync, ref IGeneResourceDrain resourceDrain)
        {
            if (sync.isWriting)
            {
                if (resourceDrain is Gene gene)
                    sync.Write(gene);
                else
                    throw new Exception($"Unsupported {nameof(IGeneResourceDrain)} type: {resourceDrain.GetType()}");
            }
            else
                resourceDrain = sync.Read<Gene>() as IGeneResourceDrain;
        }
    }
}