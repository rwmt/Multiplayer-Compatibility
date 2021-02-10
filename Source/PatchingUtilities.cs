using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Verse;

namespace Multiplayer.Compat
{
    static class PatchingUtilities
    {
        static void FixRNGPre() => Rand.PushState();
        static void FixRNGPos() => Rand.PopState();

        /// <summary>Patches out <see cref="System.Random"/> calls using <see cref="FixRNG(IEnumerable{CodeInstruction})"/>, and optionally surrounds the method with <see cref="Rand.PushState"/> and <see cref="Rand.PopState"/>.</summary>
        /// <param name="methods">Methods that needs patching</param>
        /// <param name="patchPushPop">Determines if the methods should be surrounded with push/pop calls</param>
        internal static void PatchSystemRand(string[] methods, bool patchPushPop = true)
        {
            foreach (var method in methods)
                PatchSystemRand(AccessTools.Method(method), patchPushPop);
        }

        /// <summary>Patches out <see cref="System.Random"/> calls using <see cref="FixRNG(IEnumerable{CodeInstruction})"/>, and optionally surrounds the method with <see cref="Rand.PushState"/> and <see cref="Rand.PopState"/>.</summary>
        /// <param name="methods">Methods that needs patching</param>
        /// <param name="patchPushPop">Determines if the methods should be surrounded with push/pop calls</param>
        internal static void PatchSystemRand(MethodBase[] methods, bool patchPushPop = true)
        {
            foreach (var method in methods)
                PatchSystemRand(method, patchPushPop);
        }

        /// <summary>Patches out <see cref="System.Random"/> calls using <see cref="FixRNG(IEnumerable{CodeInstruction})"/>, and optionally surrounds the method with <see cref="Rand.PushState"/> and <see cref="Rand.PopState"/>.</summary>
        /// <param name="methods">Methods that needs patching</param>
        /// <param name="patchPushPop">Determines if the methods should be surrounded with push/pop calls</param>
        internal static void PatchSystemRand(string method, bool patchPushPop = true)
            => PatchSystemRand(AccessTools.Method(method), patchPushPop);

        /// <summary>Patches out <see cref="System.Random"/> calls using <see cref="FixRNG(IEnumerable{CodeInstruction})"/>, and optionally surrounds the method with <see cref="Rand.PushState"/> and <see cref="Rand.PopState"/>.</summary>
        /// <param name="methods">Method that needs patching</param>
        /// <param name="patchPushPop">Determines if the method should be surrounded with push/pop calls</param>
        internal static void PatchSystemRand(MethodBase method, bool patchPushPop = true)
        {
            var transpiler = new HarmonyMethod(typeof(PatchingUtilities), nameof(FixRNG));

            if (patchPushPop)
                PatchPushPopRand(method, transpiler);
            else
                MpCompat.harmony.Patch(method, transpiler: transpiler);
        }

        /// <summary>Patches out <see cref="System.Random"/> calls using <see cref="FixRNG(IEnumerable{CodeInstruction})"/>, and optionally surrounds the method with <see cref="Rand.PushState"/> and <see cref="Rand.PopState"/>.</summary>
        /// <param name="type">Type with a constructor that needs patching</param>
        /// <param name="patchPushPop">Determines if the method should be surrounded with push/pop calls</param>
        internal static void PatchSystemRandCtor(string type, bool patchPushPop = true)
            => PatchSystemRand(AccessTools.Constructor(AccessTools.TypeByName(type)), patchPushPop);

        /// <summary>Patches out <see cref="System.Random"/> calls using <see cref="FixRNG(IEnumerable{CodeInstruction})"/>, and optionally surrounds the method with <see cref="Rand.PushState"/> and <see cref="Rand.PopState"/>.</summary>
        /// <param name="type">Type with a constructors that needs patching</param>
        /// <param name="patchPushPop">Determines if the methods should be surrounded with push/pop calls</param>
        internal static void PatchSystemRandCtor(string[] types, bool patchPushPop = true)
        {
            foreach (var method in types)
                PatchSystemRand(AccessTools.Constructor(AccessTools.TypeByName(method)), patchPushPop);
        }

        /// <summary>Surrounds method with <see cref="Rand.PushState"/> and <see cref="Rand.PopState"/>, as well as applies the transpiler (if provided).</summary>
        /// <param name="methods">Methods that needs patching (as string)</param>
        /// <param name="transpiler">Transpiler that will be applied to the method</param>
        internal static void PatchPushPopRand(string[] methods, HarmonyMethod transpiler = null)
        {
            foreach (var method in methods)
                PatchPushPopRand(AccessTools.Method(method), transpiler);
        }

        /// <summary>Surrounds method with <see cref="Rand.PushState"/> and <see cref="Rand.PopState"/>, as well as applies the transpiler (if provided).</summary>
        /// <param name="methods">Method that needs patching</param>
        /// <param name="transpiler">Transpiler that will be applied to the method</param>
        internal static void PatchPushPopRand(MethodBase[] methods, HarmonyMethod transpiler = null)
        {
            foreach (var method in methods)
                PatchPushPopRand(method, transpiler);
        }

        /// <summary>Surrounds method with <see cref="Rand.PushState"/> and <see cref="Rand.PopState"/>, as well as applies the transpiler (if provided).</summary>
        /// <param name="method">Method that needs patching</param>
        /// <param name="transpiler">Transpiler that will be applied to the method</param>
        internal static void PatchPushPopRand(string method, HarmonyMethod transpiler = null)
            => PatchPushPopRand(AccessTools.Method(method), transpiler);

        /// <summary>Surrounds method with <see cref="Rand.PushState"/> and <see cref="Rand.PopState"/>, as well as applies the transpiler (if provided).</summary>
        /// <param name="method">Method that needs patching</param>
        /// <param name="transpiler">Transpiler that will be applied to the method</param>
        internal static void PatchPushPopRand(MethodBase method, HarmonyMethod transpiler = null)
        {
            MpCompat.harmony.Patch(method,
                prefix: new HarmonyMethod(typeof(PatchingUtilities), nameof(FixRNGPre)),
                postfix: new HarmonyMethod(typeof(PatchingUtilities), nameof(FixRNGPos)),
                transpiler: transpiler
            );
        }

        /// <summary>Patches out <see cref="UnityEngine.Random"/> calls using <see cref="FixUnityRNG(IEnumerable{CodeInstruction})"/>, and optionally surrounds the method with <see cref="Rand.PushState"/> and <see cref="Rand.PopState"/>.</summary>
        /// <param name="methods">Methods that needs patching</param>
        /// <param name="patchPushPop">Determines if the methods should be surrounded with push/pop calls</param>
        internal static void PatchUnityRand(string[] methods, bool patchPushPop = true)
        {
            foreach (var method in methods)
                PatchUnityRand(AccessTools.Method(method), patchPushPop);
        }

        /// <summary>Patches out <see cref="UnityEngine.Random"/> calls using <see cref="FixUnityRNG(IEnumerable{CodeInstruction})"/>, and optionally surrounds the method with <see cref="Rand.PushState"/> and <see cref="Rand.PopState"/>.</summary>
        /// <param name="methods">Methods that needs patching</param>
        /// <param name="patchPushPop">Determines if the methods should be surrounded with push/pop calls</param>
        internal static void PatchUnityRand(MethodBase[] methods, bool patchPushPop = true)
        {
            foreach (var method in methods)
                PatchUnityRand(method, patchPushPop);
        }

        /// <summary>Patches out <see cref="UnityEngine.Random"/> calls using <see cref="FixUnityRNG(IEnumerable{CodeInstruction})"/>, and optionally surrounds the method with <see cref="Rand.PushState"/> and <see cref="Rand.PopState"/>.</summary>
        /// <param name="methods">Methods that needs patching</param>
        /// <param name="patchPushPop">Determines if the methods should be surrounded with push/pop calls</param>
        internal static void PatchUnityRand(string method, bool patchPushPop = true)
            => PatchUnityRand(AccessTools.Method(method), patchPushPop);

        /// <summary>Patches out <see cref="UnityEngine.Random"/> calls using <see cref="FixUnityRNG(IEnumerable{CodeInstruction})"/>, and optionally surrounds the method with <see cref="Rand.PushState"/> and <see cref="Rand.PopState"/>.</summary>
        /// <param name="methods">Method that needs patching</param>
        /// <param name="patchPushPop">Determines if the method should be surrounded with push/pop calls</param>
        internal static void PatchUnityRand(MethodBase method, bool patchPushPop = true)
        {
            var transpiler = new HarmonyMethod(typeof(PatchingUtilities), nameof(FixUnityRNG));

            if (patchPushPop)
                PatchPushPopRand(method, transpiler);
            else
                MpCompat.harmony.Patch(method, transpiler: transpiler);
        }

        #region System RNG transpiler
        private static readonly ConstructorInfo SystemRandConstructor = typeof(System.Random).GetConstructor(Array.Empty<Type>());
        private static readonly ConstructorInfo RandRedirectorConstructor = typeof(RandRedirector).GetConstructor(Array.Empty<Type>());

        /// <summary>Transpiler that replaces all calls to <see cref="System.Random"/> constructor with calls to <see cref="RandRedirector"/> constructor</summary>
        internal static IEnumerable<CodeInstruction> FixRNG(IEnumerable<CodeInstruction> instr)
        {
            foreach (var ci in instr)
            {
                if (ci.opcode == OpCodes.Newobj && ci.operand is ConstructorInfo constructorInfo && constructorInfo == SystemRandConstructor)
                    ci.operand = RandRedirectorConstructor;

                yield return ci;
            }
        }

        /// <summary>This class allows replacing any <see cref="System.Random"/> calls with <see cref="Verse.Rand"/> calls</summary>
        public class RandRedirector : Random
        {
            public override int Next() => Rand.Range(0, int.MaxValue);

            public override int Next(int maxValue) => Rand.Range(0, maxValue);

            public override int Next(int minValue, int maxValue) => Rand.Range(minValue, maxValue);

            public override void NextBytes(byte[] buffer)
            {
                for (int i = 0; i < buffer.Length; i++)
                    buffer[i] = (byte)Rand.RangeInclusive(0, 255);
            }

            public override double NextDouble() => Rand.Range(0f, 1f);
        }
        #endregion

        #region Unity RNG transpiler
        private static readonly MethodInfo UnityRandomRangeInt = AccessTools.Method(typeof(UnityEngine.Random), nameof(UnityEngine.Random.Range), new[] { typeof(int), typeof(int) });
        private static readonly MethodInfo UnityRandomRangeIntObsolete = AccessTools.Method(typeof(UnityEngine.Random), nameof(UnityEngine.Random.RandomRange), new[] { typeof(int), typeof(int) });
        private static readonly MethodInfo UnityRandomRangeFloat = AccessTools.Method(typeof(UnityEngine.Random), nameof(UnityEngine.Random.Range), new[] { typeof(float), typeof(float) });
        private static readonly MethodInfo UnityRandomRangeFloatObsolete = AccessTools.Method(typeof(UnityEngine.Random), nameof(UnityEngine.Random.RandomRange), new[] { typeof(float), typeof(float) });
        private static readonly MethodInfo UnityRandomValue = AccessTools.PropertyGetter(typeof(UnityEngine.Random), nameof(UnityEngine.Random.value));
        private static readonly MethodInfo UnityInsideUnitCircle = AccessTools.PropertyGetter(typeof(UnityEngine.Random), nameof(UnityEngine.Random.insideUnitCircle));

        private static readonly MethodInfo VerseRandomRangeInt = AccessTools.Method(typeof(Rand), nameof(Rand.Range), new[] { typeof(int), typeof(int) });
        private static readonly MethodInfo VerseRandomRangeFloat = AccessTools.Method(typeof(Rand), nameof(Rand.Range), new[] { typeof(float), typeof(float) });
        private static readonly MethodInfo VerseRandomValue = AccessTools.PropertyGetter(typeof(Rand), nameof(Rand.Value));
        private static readonly MethodInfo VerseInsideUnitCircle = AccessTools.PropertyGetter(typeof(Rand), nameof(Rand.InsideUnitCircle));

        internal static IEnumerable<CodeInstruction> FixUnityRNG(IEnumerable<CodeInstruction> instr)
        {
            foreach (var ci in instr)
            {
                if (ci.opcode == OpCodes.Call && ci.operand is MethodInfo method)
                {
                    if (method == UnityRandomRangeInt || method == UnityRandomRangeIntObsolete)
                        ci.operand = VerseRandomRangeInt;
                    else if (method == UnityRandomRangeFloat || method == UnityRandomRangeFloatObsolete)
                        ci.operand = VerseRandomRangeFloat;
                    else if (method == UnityRandomValue)
                        ci.operand = VerseRandomValue;
                    else if (method == UnityInsideUnitCircle)
                        ci.operand = VerseInsideUnitCircle;
                }

                yield return ci;
            }
        }
        #endregion
    }
}
