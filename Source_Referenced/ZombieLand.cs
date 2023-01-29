using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Multiplayer.API;
using UnityEngine;
using Verse;
using ZombieLand;

namespace Multiplayer.Compat
{
    /// <summary>ZombieLand by brrainz</summary>
    /// <see href="https://github.com/pardeike/Zombieland"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=928376710"/>
    [MpCompatFor("brrainz.zombieland")]
    public class ZombieLand
    {
        public ZombieLand(ModContentPack mod)
        {
            Tools.avoider.running = false;

            PatchingUtilities.PatchUnityRand(AccessTools.Method(typeof(SubEffecter_ZombieShocker), "RandomZap"), false);
            PatchingUtilities.PatchUnityRand(AccessTools.Method(typeof(SubEffecter_ZombieShocker), "ZapNextCell"), false);

            var transpiler = new HarmonyMethod(typeof(ZombieLand), nameof(ReplaceCoroutineCall));
            MpCompat.harmony.Patch(
                AccessTools.Method(typeof(ZombiesRising), nameof(ZombiesRising.TryExecute)),
                transpiler: transpiler);
            MpCompat.harmony.Patch(
                AccessTools.Method(typeof(ZombieAvoider), nameof(ZombieAvoider.UpdateZombiePositions)),
                transpiler: transpiler);
            MpCompat.harmony.Patch(
                AccessTools.Method(typeof(ZombieAvoider), nameof(ZombieAvoider.GetCostsGrid)),
                prefix: new HarmonyMethod(typeof(ZombieLand), nameof(PreGetCostsGrid)));

            MP.RegisterSyncMethod(typeof(ColonistConfig), "ToggleAutoExtractZombieSerum");
            MP.RegisterSyncMethod(typeof(ColonistConfig), "ToggleAutoDoubleTap");
            MP.RegisterSyncMethod(typeof(ColonistConfig), "ToggleAutoAvoidZombies");
            MP.RegisterSyncWorker<ColonistConfig>(SyncColonistConfig);

            LongEventHandler.ExecuteWhenFinished(LatePatch);
        }

        private static void LatePatch()
        {
            // Set static System.Random field to our reusable instance
            Constants.random = PatchingUtilities.RandRedirector.Instance;
            // ZombieLand.MapInfo has seeded random - should be fine
        }

        private static void SyncColonistConfig(SyncWorker sync, ref ColonistConfig config)
        {
            if (sync.isWriting)
            {
                var tempConfig = config; // Can't use ref parameter in a lambda
                var pawn = ColonistSettings.colonists.FirstOrDefault(x => x.Value == tempConfig).Key;
                sync.Write(pawn);
            }
            else
            {
                var pawn = sync.Read<Pawn>();
                config = ColonistSettings.colonists[pawn];
            }
        }

        private static void RedirectCoroutine(IEnumerator coroutine) => ZombieLandMpComponent.Coroutines.Add(coroutine);

        private static bool PreGetCostsGrid(Map map, ref AvoidGrid __result)
        {
            var queue = ZombieLandMpComponent.QueueForMap(map);
            if (queue.Any())
                __result = queue.Dequeue();

            return false;
        }

        private static IEnumerable<CodeInstruction> ReplaceCoroutineCall(IEnumerable<CodeInstruction> instr)
        {
            var coroutineMethod = AccessTools.Method(typeof(MonoBehaviour), nameof(MonoBehaviour.StartCoroutine), new[] { typeof(IEnumerator) });
            var coroutineReplacement = AccessTools.Method(typeof(ZombieLand), nameof(RedirectCoroutine));

            var enqueueMethod = AccessTools.Method(typeof(ConcurrentQueue<ZombieCostSpecs>), nameof(ConcurrentQueue<ZombieCostSpecs>.Enqueue));
            var enqueueReplacement = AccessTools.Method(typeof(ZombieLandMpComponent), nameof(ZombieLandMpComponent.Enqueue)).MakeGenericMethod(typeof(ZombieCostSpecs));

            var patchedCount = 0;

            foreach (var ci in instr)
            {
                if (ci.opcode == OpCodes.Callvirt && ci.operand is MethodInfo method)
                {
                    if (method == coroutineMethod)
                    {
                        ci.operand = coroutineReplacement;
                        patchedCount++;
                    }
                    else if (method == enqueueMethod)
                    {
                        ci.operand = enqueueReplacement;
                        ci.opcode = OpCodes.Call;
                        patchedCount++;
                    }
                }

                yield return ci;
            }
            
            if (patchedCount == 0) Log.Warning("Failed to patch ZombieLand coroutine");
            else if (patchedCount == 1) Log.Warning("Failed to fully patch ZombieLand coroutine");
        }
    }

    [MpCompatRequireMod("brrainz.zombieland")]
    public class ZombieLandMpComponent : GameComponent
    {
        public static readonly List<IEnumerator> Coroutines = new();
        private static readonly List<int> Cleanup = new();

        // ZombieLand's ConcurrentQueue<> (which we're replacing) uses a list as its base collection
        // which allows for a enqueue with replace predicate to it
        private static readonly List<AvoidRequest> ReplacementRequestQueue = new();
        private static readonly Dictionary<Map, Queue<AvoidGrid>> ReplacementResultQueue = new();

        public ZombieLandMpComponent(Game game)
        {
        }

        public static Queue<AvoidGrid> QueueForMap(Map map)
        {
            if (ReplacementResultQueue.TryGetValue(map, out var queue))
                return queue;

            queue = new Queue<AvoidGrid>();
            ReplacementResultQueue.Add(map, queue);
            return queue;
        }

        public override void GameComponentTick()
        {
            if (ReplacementRequestQueue.Any())
            {
                try
                {
                    var avoidRequest = ReplacementRequestQueue[0];
                    ReplacementRequestQueue.RemoveAt(0);
                    var item = Tools.avoider.ProcessRequest(avoidRequest);
                    QueueForMap(avoidRequest.map).Enqueue(item);
                }
                catch (Exception e)
                {
                    Log.Warning($"ZombieAvoider thread replacement for multiplayer error:\n{e}");
                }
            }

            for (var i = 0; i < Coroutines.Count; i++)
            {
                var coroutine = Coroutines[i];
                if (!coroutine.MoveNext())
                    Cleanup.Add(i);
            }

            if (Cleanup.Any())
            {
                for (var i = Cleanup.Count - 1; i >= 0; i--)
                    Coroutines.RemoveAt(Cleanup[i]);

                Cleanup.Clear();
            }
        }

        public override void StartedNewGame() => LogError();
        public override void LoadedGame() => LogError();

        private static void LogError()
        {
            if (!MP.IsInMultiplayer)
                Log.ErrorOnce(
                    "For performance reasons, ZombieLand should not be used in single player if Multiplayer Compatibility mod is active. Unless you're setting up a multiplayer session, please disable MP Compat to gain better performance.",
                    -434115778);
        }

        public static void Enqueue<T>(IList<T> queue, T item, Func<T, bool> overwritePredicate)
        {
            if (overwritePredicate == null)
            {
                queue.Add(item);
                return;
            }

            var index = queue.FirstIndexOf(overwritePredicate);
            if (index >= 0 && index < queue.Count)
                queue[index] = item;
            else
                queue.Add(item);
        }
    }
}