using System;
using System.Diagnostics;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using Verse;

namespace Multiplayer.Compat
{
    public static class MpSyncWorkers
    {
        public static void Requires<T>() => Requires(typeof(T));

        public static void Requires(Type type)
        {
            if (HasSyncWorker(type))
            {
                Log.Warning($"Sync worker of type {type} already exists in MP, temporary sync worker can be removed from MP Compat");
                return;
            }

            if (type == typeof(IGeneResourceDrain))
                MP.RegisterSyncWorker<IGeneResourceDrain>(SyncIGeneResourceDrain);
            else
                Log.Error($"Trying to register SyncWorker of type {type}, but it's not supported.\n{new StackTrace(1)}");
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

        private static bool HasSyncWorker(Type type)
        {
            const string methodPath = "Multiplayer.Client.SyncSerialization:CanHandle";

            // Don't cache the method, it'll be used very rarely (assuming it'll even be used at all), so there's no point in having a field for it.
            var method = AccessTools.DeclaredMethod(methodPath, new[] { typeof(SyncType) });

            if (method == null)
            {
                Log.Error($"Failed to check if sync worker for type {type} is already registered in MP - failed to find method {methodPath}");
                return false;
            }

            if (method.ReturnType != typeof(bool))
            {
                Log.Error($"Failed to check if sync worker for type {type} is already registered in MP - return type is not bool but {method.ReturnType} for method {methodPath}");
                return false;
            }

            return (bool)method.Invoke(null, new object[] { new SyncType(type) });
        }
    }
}