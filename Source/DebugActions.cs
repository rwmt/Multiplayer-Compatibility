using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Random = System.Random;

namespace Multiplayer.Compat
{
    internal static class DebugActions
    {
        private const string CategoryName = "Multiplayer Compatibility";

        #region ReferenceBuilder

        const string REFERENCES_FOLDER = "References";
        internal static ModContentPack content;

        [DebugAction(CategoryName, "Generate reference DLL", allowedGameStates = AllowedGameStates.Entry)]
        private static void BuildDllReferences()
        {
            Log.Warning($"MPCompat :: Put any reference building requests under {REFERENCES_FOLDER} as {{DLLNAME}}.txt");
            var (successes, skipped, failures) = ReferenceBuilder.Restore(Path.Combine(content.RootDir, REFERENCES_FOLDER));
            Log.Warning($"MPCompat :: Finished building references. Built: {successes}. Skipped: {skipped}. Failed: {failures}.");
        }

        #endregion

        #region DesyncSourceSearch

        internal enum StuffToSearch
        {
            SystemRng,
            UnityRng,
            GenView,
            Coroutines,
            Multithreading,
            CameraDriver,
            CurrentMap,
            Selector,
            Stopwatch,
            GameComponentUpdate,
            TimeManager,
        }

        private static readonly int MaxFoundStuff = Enum.GetNames(typeof(StuffToSearch)).Length;

        private static readonly MethodInfo FindCurrentMap = AccessTools.DeclaredPropertyGetter(typeof(Find), nameof(Find.CurrentMap));
        private static readonly MethodInfo GameCurrentMap = AccessTools.DeclaredPropertyGetter(typeof(Game), nameof(Game.CurrentMap));
        private static readonly MethodInfo GameComponentUpdate = AccessTools.DeclaredMethod(typeof(GameComponent), nameof(GameComponent.GameComponentUpdate));

        [DebugAction(CategoryName, "Log unsafe stuff", allowedGameStates = AllowedGameStates.Entry)]
        public static void LogUnpatchedStuff() => LogUnpatchedStuff(false);

        // Having the same name could be confusing, so people would potentially use it since it "logs more",
        // but this stuff is more useful for checking if too much (like RimWorld itself) is getting checked.
        // Also, name didn't fit in fully.
        [DebugAction(CategoryName, "Test checked types", allowedGameStates = AllowedGameStates.Entry)]
        public static void LogUnpatchedRngLogAll() => LogUnpatchedStuff(true);

        public static void LogUnpatchedStuff(bool logAllCheckedClasses)
        {
            var unsupportedTypes = new[]
            {
                nameof(System),
                nameof(Unity),
                nameof(UnityEditor),
                nameof(UnityEngine),
                nameof(UnityEngineInternal),
                nameof(Multiplayer),
                nameof(Microsoft),
                nameof(HarmonyLib),
                nameof(Mono),
                nameof(MonoMod),
                nameof(Ionic),
                nameof(NVorbis),
                nameof(RuntimeAudioClipLoader),
                nameof(JetBrains),
                nameof(AOT),
                nameof(Steamworks),
                nameof(UnityStandardAssets),
                nameof(ObjCRuntimeInternal),
                nameof(TMPro),
                nameof(NAudio),
                nameof(ICSharpCode),
                nameof(MS),
                "DynDelegate",
                "I18N",
                "LiteNetLib",
                "RestSharp",
                "YamlDotNet",
                "SemVer",

                // Used by some mods, don't include
                //nameof(RimWorld),
                //nameof(Verse),
            };

            var types = LoadedModManager.RunningMods
                .Where(x => x.PackageId.ToLower() != "rwmt.multiplayer" && x.PackageId.ToLower() != "rwmt.multiplayercompatibility")
                .SelectMany(x => x.assemblies.loadedAssemblies)
                .SelectMany(x => x.GetTypes());

            var log = Enum.GetValues(typeof(StuffToSearch))
                .Cast<StuffToSearch>()
                .ToDictionary(x => x, _ => new List<string>());

            List<string> logAllClasses = null;
            if (logAllCheckedClasses)
                logAllClasses = new List<string>();

            Parallel.ForEach(types, t => FindUnpatchedInType(t, unsupportedTypes, log, logAllClasses));

            if (log.Any(x => x.Value.Any()))
            {
                Log.Warning("== Potentially unpatched RNG or unsafe methods found. ==");
                Log.Warning("Please note, it doesn't always need syncing, or might even break if synced, depending on how the mod uses it. It could also be patched in an alternative way.");
                Log.Warning("Things that are already patched or don't need patching are not listed here, if possible to (easily) check for that.");

                if (log[StuffToSearch.SystemRng].Any())
                {
                    Log.Warning("== Unpatched System RNG: ==");
                    Log.Warning("== Unless it's deterministically seeded or unused, it'll cause issues. ==");
                    Log.Message(log[StuffToSearch.SystemRng].Append("\n").Join(delimiter: "\n"));
                }

                if (log[StuffToSearch.UnityRng].Any())
                {
                    Log.Warning("== Unpatched Unity RNG: ==");
                    Log.Warning("== Unless it's deterministically seeded or unused, it'll cause issues. ==");
                    Log.Message(log[StuffToSearch.UnityRng].Append("\n").Join(delimiter: "\n"));
                }

                if (log[StuffToSearch.GenView].Any())
                {
                    Log.Warning("== GenView usage found: ==");
                    Log.Warning("== Usage of GenView means the mod is doing something based on if something is (not) visible for the user. Can cause issues as players tend to have different camera positions, or be on different maps. ==");
                    Log.Message(log[StuffToSearch.GenView].Append("\n").Join(delimiter: "\n"));
                }

                if (log[StuffToSearch.Coroutines].Any())
                {
                    Log.Warning("== Coroutine usage found: ==");
                    Log.Warning("== Coroutine are not supported by MP as they are not deterministic. Unless they were patched, or are used on game startup, expect issues. ==");
                    Log.Message(log[StuffToSearch.Coroutines].Append("\n").Join(delimiter: "\n"));
                }

                if (log[StuffToSearch.Multithreading].Any())
                {
                    Log.Warning("== Multithreading usage found: ==");
                    Log.Warning("== Please note, the detection may not be perfect and miss some some multithreading usage! ==");
                    Log.Warning("== Multithreading is not supported by MP as they are not deterministic. Unless they were patched, or are used on game startup, expect issues. ==");
                    Log.Message(log[StuffToSearch.Multithreading].Append("\n").Join(delimiter: "\n"));
                }

                if (log[StuffToSearch.CameraDriver].Any())
                {
                    Log.Warning("== CameraDriver usage found: ==");
                    Log.Warning("== Usage of CameraDriver may cause issues if used for things like checking if something is (not) visible on the screen. Can cause issues as players tend to have different camera positions, or be on different maps. Mods moving camera around, etc. are generally fine. ==");
                    Log.Message(log[StuffToSearch.CameraDriver].Append("\n").Join(delimiter: "\n"));
                }

                if (log[StuffToSearch.CurrentMap].Any())
                {
                    Log.Warning("== Current map usage found: ==");
                    Log.Warning("== Mods basing code on current map for things like spawning events may cause issues, as players can be on different maps. ==");
                    Log.Message(log[StuffToSearch.CurrentMap].Append("\n").Join(delimiter: "\n"));
                }

                if (log[StuffToSearch.Selector].Any())
                {
                    Log.Warning("== Selector usage found: ==");
                    Log.Warning("== Usage of selector could cause issues when mod needs to check what the player has selected, but for obvious reason in MP there's more than 1 player. Using it for displaying overlays, etc. is fine. ==");
                    Log.Message(log[StuffToSearch.Selector].Append("\n").Join(delimiter: "\n"));
                }

                if (log[StuffToSearch.Stopwatch].Any())
                {
                    Log.Warning("== Stopwatch usage found: ==");
                    Log.Warning("== Potential issues from it arise when mod try to make a mod more performant by limiting how long some code can run. Using it to measure performance is safe. ==");
                    Log.Message(log[StuffToSearch.Stopwatch].Append("\n").Join(delimiter: "\n"));
                }

                if (log[StuffToSearch.GameComponentUpdate].Any())
                {
                    Log.Warning("== GameComponent.Update usage found: ==");
                    Log.Warning("== It can be called while the game is paused, and is not called once per tick. Depending on what it's used for, it may cause issues. ==");
                    Log.Message(log[StuffToSearch.GameComponentUpdate].Append("\n").Join(delimiter: "\n"));
                }

                if (log[StuffToSearch.TimeManager].Any())
                {
                    Log.Warning("== TimeManager usage found: ==");
                    Log.Warning("== TimeManager uses timing functions that won't be synced across players. Unless used for UI, sounds, etc. then it will cause desyncs. ==");
                    Log.Message(log[StuffToSearch.TimeManager].Append("\n").Join(delimiter: "\n"));
                }
            }
            else Log.Warning("== No unpatched RNG or potentially unsafe methods found ==");

            if (logAllClasses != null && logAllClasses.Any())
            {
                Log.Warning("== All checked classes: ==");
                Log.Message(logAllClasses.OrderBy(x => x).Join(delimiter: "\n"));
            }
        }

        internal static void FindUnpatchedInType(Type type, string[] unsupportedTypes, Dictionary<StuffToSearch, List<string>> log, List<string> logAllClasses = null)
        {
            // Don't mind all the try/catch blocks, I went for maximum safety
            try
            {
                if (unsupportedTypes.Any(t => type.Namespace != null && (type.Namespace == t || type.Namespace.StartsWith($"{t}.")))) return;
            }
            catch (Exception)
            {
                // ignored
            }

            if (logAllClasses != null)
            {
                lock (logAllClasses)
                    logAllClasses.Add(type.FullName);
            }

            const string monoFunctionPointerClass = "System.MonoFNPtrFakeClass";

            try
            {
                // Get all methods, constructors, getters, and setters (everything that should have IL instructions).
                // Ignore stuff that works on the mono function pointer stuff, as it crashes the game.
                var methods = AccessTools.GetDeclaredMethods(type).Where(m => m.ReturnType.FullName != monoFunctionPointerClass).Cast<MethodBase>()
                    .Concat(AccessTools.GetDeclaredConstructors(type))
                    .Concat(AccessTools.GetDeclaredProperties(type)
                        .SelectMany(p => new[] { p.GetGetMethod(true), p.GetSetMethod(true) })
                        .Where(p => p != null && p.ReturnType.FullName != monoFunctionPointerClass))
                    .Where(m => m.HasMethodBody());

                foreach (var method in methods)
                {
                    try
                    {
                        foreach (var found in FindRng(method))
                        {
                            lock (log[found])
                                log[found].Add($"{type.FullName}:{method.Name}");
                        }
                    }
                    catch (Exception)
                    {
                        // ignored
                    }
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        internal static HashSet<StuffToSearch> FindRng(MethodBase baseMethod)
        {
            var instr = PatchProcessor.GetCurrentInstructions(baseMethod);
            var foundStuff = new HashSet<StuffToSearch>();

            if (baseMethod == GameComponentUpdate)
                foundStuff.Add(StuffToSearch.GameComponentUpdate);

            foreach (var ci in instr)
            {
                switch (ci.operand)
                {
                    // Constructors
                    case ConstructorInfo { DeclaringType: not null } ctor when ctor.DeclaringType == typeof(Random):
                        foundStuff.Add(StuffToSearch.SystemRng);
                        break;
                    case ConstructorInfo { DeclaringType: not null } ctor when ctor.DeclaringType == typeof(Thread) || ctor.DeclaringType == typeof(ThreadStart):
                        foundStuff.Add(StuffToSearch.Multithreading);
                        break;
                    case ConstructorInfo { DeclaringType: not null } ctor when ctor.DeclaringType == typeof(Stopwatch):
                        foundStuff.Add(StuffToSearch.Stopwatch);
                        break;
                    // Methods
                    case MethodInfo { DeclaringType: not null } method when method.DeclaringType == typeof(UnityEngine.Random):
                        foundStuff.Add(StuffToSearch.UnityRng);
                        break;
                    case MethodInfo { DeclaringType: not null } method when method.DeclaringType == typeof(GenView):
                        foundStuff.Add(StuffToSearch.GenView);
                        break;
                    // StartCoroutine, etc.
                    // We could check for the methods themselves, but it's much easier to just check for return
                    // as there'll probably not be all that many methods returning it.
                    case MethodInfo { DeclaringType: not null } method when method.ReturnType == typeof(Coroutine):
                        foundStuff.Add(StuffToSearch.Coroutines);
                        break;
                    // Player dependent stuff, like current camera position, current map, currently selected things
                    case MethodInfo { DeclaringType: not null } method when method.ReturnType == typeof(CameraDriver):
                        foundStuff.Add(StuffToSearch.CameraDriver);
                        break;
                    case MethodInfo method when method == FindCurrentMap || method == GameCurrentMap:
                        foundStuff.Add(StuffToSearch.CurrentMap);
                        break;
                    case MethodInfo method when method.DeclaringType == typeof(Selector):
                        foundStuff.Add(StuffToSearch.Selector);
                        break;
                    // Operating on time instead of ticks
                    case MethodInfo method when method.DeclaringType == typeof(Time):
                        foundStuff.Add(StuffToSearch.TimeManager);
                        break;
                }

                if (foundStuff.Count == MaxFoundStuff) break;
            }

            return foundStuff;
        }

        #endregion
    }
}