using System;
using System.Collections.Generic;
using System.Diagnostics;
using Multiplayer.API;
using RimWorld;
using Verse;

namespace Multiplayer.Compat
{
    public static class MpSyncWorkers
    {
        private static HashSet<Type> registeredSyncWorkers = null;

        public static void Requires<T>() => Requires(typeof(T));

        public static void Requires(Type type)
        {
            if (registeredSyncWorkers != null && registeredSyncWorkers.Contains(type))
                return;

            if (type == typeof(IGeneResourceDrain))
                MP.RegisterSyncWorker<IGeneResourceDrain>(SyncIGeneResourceDrain);
            else
            {
                Log.Error($"Trying to register SyncWorker of type {type}, but it's not supported.\n{new StackTrace(1)}");
                return;
            }

            registeredSyncWorkers ??= new HashSet<Type>();
            registeredSyncWorkers.Add(type);
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