using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using Verse;

namespace Multiplayer.Compat
{
    internal static class DebugActions
    {
        private const string CategoryName = "Multiplayer Compatibility";

        [DebugAction(CategoryName, "Log unpatched RNG", allowedGameStates = AllowedGameStates.Entry)]
        public static void LogUnpatchedRng() => LogUnpatchedRng(false);

        [DebugAction(CategoryName, "Log unpatched RNG + checked types", allowedGameStates = AllowedGameStates.Entry)]
        public static void LogUnpatchedRngLogAll() => LogUnpatchedRng(true);

        public static void LogUnpatchedRng(bool logAllCheckedClasses)
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
                nameof(Microsoft),
                nameof(Mono),
                nameof(MonoMod),
                nameof(Ionic),
                nameof(NVorbis),
                nameof(RuntimeAudioClipLoader),
                nameof(JetBrains),
                nameof(AOT),
                "DynDelegate",
                "I18N",
                "LiteNetLib",
                "RestSharp",
                "JetBrains",
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

            var systemRngLog = new List<string>();
            var unityRngLog = new List<string>();
            List<string> logAllClasses = null;
            if (logAllCheckedClasses)
                logAllClasses = new List<string>();

            Parallel.ForEach(types, t => FindUnpatchedInType(t, unsupportedTypes, systemRngLog, unityRngLog, logAllClasses));

            if (systemRngLog.Any() || unityRngLog.Any())
            {
                Log.Warning("== Potentially unpatched RNG found. ==");
                Log.Warning("Please note, it doesn't always need syncing, or might even break if synced, depending on how the mod uses it. It could also be patched in an alternative way.");

                if (systemRngLog.Any())
                {
                    Log.Warning("== Unpatched System RNG: ==");
                    Log.Message(systemRngLog.Join(delimiter: "\n"));
                }
                if (unityRngLog.Any())
                {
                    Log.Warning("== Unpatched Unity RNG: ==");
                    Log.Message(unityRngLog.Join(delimiter: "\n"));
                }
            }
            else Log.Warning("== No unpatched RNG found ==");

            if (logAllClasses != null && logAllClasses.Any())
            {
                Log.Warning("== All checked classes: ==");
                Log.Message(logAllClasses.Join(delimiter: "\n"));
            }
        }

        public static void FindUnpatchedInType(Type type, string[] unsupportedTypes, List<string> systemRngLog, List<string> unityRngLog, List<string> logAllClasses = null)
        {            // Don't mind all the try/catch blocks, I went for maximum safety
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

            try
            {
                // Get all methods, constructors, getters, and setters (everything that should have IL instructions)
                var methods = AccessTools.GetDeclaredMethods(type).Cast<MethodBase>()
                    .Concat(AccessTools.GetDeclaredConstructors(type))
                    .Concat(AccessTools.GetDeclaredProperties(type).SelectMany(p => new[] { p.GetGetMethod(true), p.GetSetMethod(true) }).Where(p => p != null));

                foreach (var method in methods)
                {
                    try
                    {
                        MpCompat.harmony.Patch(method,
                            transpiler: new HarmonyMethod(typeof(DebugActions), nameof(FindRng)));
                    }
                    catch (Exception e) when ((e?.InnerException ?? e) is PatchingCancelledException cancelled)
                    {
                        if (cancelled.foundSystemRng)
                        {
                            lock (systemRngLog)
                                systemRngLog.Add($"{type.FullName}:{method.Name}");
                        }

                        if (cancelled.foundUnityRng)
                        {
                            lock (unityRngLog)
                                unityRngLog.Add($"{type.FullName}:{method.Name}");
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

        internal static IEnumerable<CodeInstruction> FindRng(IEnumerable<CodeInstruction> instr)
        {
            var foundSystemRand = false;
            var foundUnityRand = false;

            foreach (var ci in instr)
            {
                if (ci.operand is ConstructorInfo { DeclaringType: { } } ctor &&
                    ctor.DeclaringType == typeof(System.Random))
                {
                    foundSystemRand = true;
                    if (foundUnityRand) break;
                }
                else if (ci.operand is MethodInfo { DeclaringType: { } } method &&
                         method.DeclaringType == typeof(UnityEngine.Random))
                {
                    foundUnityRand = true;
                    if (foundSystemRand) break;
                }
            }

            throw new PatchingCancelledException(foundSystemRand, foundUnityRand);
        }

        internal class PatchingCancelledException : Exception
        {
            public bool foundSystemRng;
            public bool foundUnityRng;

            public PatchingCancelledException(bool foundSystemRng, bool foundUnityRng)
            {
                this.foundSystemRng = foundSystemRng;
                this.foundUnityRng = foundUnityRng;
            }
        }
    }
}
