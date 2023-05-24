using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Multiplayer.Compat
{
    public static class PatchingUtilities
    {
        #region RNG Patching

        static void FixRNGPre() => Rand.PushState();
        static void FixRNGPos() => Rand.PopState();

        /// <summary>Patches out <see cref="System.Random"/> calls using <see cref="FixRNG(IEnumerable{CodeInstruction})"/>, and optionally surrounds the method with <see cref="Rand.PushState"/> and <see cref="Rand.PopState"/>.</summary>
        /// <param name="methods">Methods that needs patching</param>
        /// <param name="patchPushPop">Determines if the methods should be surrounded with push/pop calls</param>
        public static void PatchSystemRand(IEnumerable<string> methods, bool patchPushPop = true)
        {
            foreach (var method in methods)
                PatchSystemRand(AccessTools.DeclaredMethod(method) ?? AccessTools.Method(method), patchPushPop);
        }

        /// <summary>Patches out <see cref="System.Random"/> calls using <see cref="FixRNG(IEnumerable{CodeInstruction})"/>, and optionally surrounds the method with <see cref="Rand.PushState"/> and <see cref="Rand.PopState"/>.</summary>
        /// <param name="methods">Methods that needs patching</param>
        /// <param name="patchPushPop">Determines if the methods should be surrounded with push/pop calls</param>
        public static void PatchSystemRand(IEnumerable<MethodBase> methods, bool patchPushPop = true)
        {
            foreach (var method in methods)
                PatchSystemRand(method, patchPushPop);
        }

        /// <summary>Patches out <see cref="System.Random"/> calls using <see cref="FixRNG(IEnumerable{CodeInstruction})"/>, and optionally surrounds the method with <see cref="Rand.PushState"/> and <see cref="Rand.PopState"/>.</summary>
        /// <param name="method">Methods that needs patching</param>
        /// <param name="patchPushPop">Determines if the methods should be surrounded with push/pop calls</param>
        public static void PatchSystemRand(string method, bool patchPushPop = true)
            => PatchSystemRand(AccessTools.DeclaredMethod(method) ?? AccessTools.Method(method), patchPushPop);

        /// <summary>Patches out <see cref="System.Random"/> calls using <see cref="FixRNG(IEnumerable{CodeInstruction})"/>, and optionally surrounds the method with <see cref="Rand.PushState"/> and <see cref="Rand.PopState"/>.</summary>
        /// <param name="method">Method that needs patching</param>
        /// <param name="patchPushPop">Determines if the method should be surrounded with push/pop calls</param>
        public static void PatchSystemRand(MethodBase method, bool patchPushPop = true)
        {
            var transpiler = new HarmonyMethod(typeof(PatchingUtilities), nameof(FixRNG));

            if (patchPushPop)
                PatchPushPopRand(method, transpiler);
            else
                MpCompat.harmony.Patch(method, transpiler: transpiler);
        }

        /// <summary>Patches out <see cref="System.Random"/> calls using <see cref="FixRNG(IEnumerable{CodeInstruction})"/>, and optionally surrounds the method with <see cref="Rand.PushState"/> and <see cref="Rand.PopState"/>.</summary>
        /// <param name="typeName">Type with a parameterless constructor that needs patching</param>
        /// <param name="patchPushPop">Determines if the method should be surrounded with push/pop calls</param>
        public static void PatchSystemRandCtor(string typeName, bool patchPushPop = true)
        {
            var type = AccessTools.TypeByName(typeName);
            PatchSystemRand(AccessTools.DeclaredConstructor(type) ?? AccessTools.Constructor(type), patchPushPop);
        }

        /// <summary>Patches out <see cref="System.Random"/> calls using <see cref="FixRNG(IEnumerable{CodeInstruction})"/>, and optionally surrounds the method with <see cref="Rand.PushState"/> and <see cref="Rand.PopState"/>.</summary>
        /// <param name="typeNames">Type with a parameterless constructors that needs patching</param>
        /// <param name="patchPushPop">Determines if the methods should be surrounded with push/pop calls</param>
        public static void PatchSystemRandCtor(IEnumerable<string> typeNames, bool patchPushPop = true)
        {
            foreach (var typeName in typeNames)
            {
                var type = AccessTools.TypeByName(typeName);
                PatchSystemRand(AccessTools.DeclaredConstructor(type) ?? AccessTools.Constructor(type), patchPushPop);
            }
        }

        /// <summary>Surrounds method with <see cref="Rand.PushState"/> and <see cref="Rand.PopState"/>, as well as applies the transpiler (if provided).</summary>
        /// <param name="methods">Methods that needs patching (as string)</param>
        /// <param name="transpiler">Transpiler that will be applied to the method</param>
        public static void PatchPushPopRand(IEnumerable<string> methods, HarmonyMethod transpiler = null)
        {
            foreach (var method in methods)
                PatchPushPopRand(AccessTools.DeclaredMethod(method) ?? AccessTools.Method(method), transpiler);
        }

        /// <summary>Surrounds method with <see cref="Rand.PushState"/> and <see cref="Rand.PopState"/>, as well as applies the transpiler (if provided).</summary>
        /// <param name="methods">Method that needs patching</param>
        /// <param name="transpiler">Transpiler that will be applied to the method</param>
        public static void PatchPushPopRand(IEnumerable<MethodBase> methods, HarmonyMethod transpiler = null)
        {
            foreach (var method in methods)
                PatchPushPopRand(method, transpiler);
        }

        /// <summary>Surrounds method with <see cref="Rand.PushState"/> and <see cref="Rand.PopState"/>, as well as applies the transpiler (if provided).</summary>
        /// <param name="method">Method that needs patching</param>
        /// <param name="transpiler">Transpiler that will be applied to the method</param>
        public static void PatchPushPopRand(string method, HarmonyMethod transpiler = null)
            => PatchPushPopRand(AccessTools.DeclaredMethod(method) ?? AccessTools.Method(method), transpiler);

        /// <summary>Surrounds method with <see cref="Rand.PushState"/> and <see cref="Rand.PopState"/>, as well as applies the transpiler (if provided).</summary>
        /// <param name="method">Method that needs patching</param>
        /// <param name="transpiler">Transpiler that will be applied to the method</param>
        public static void PatchPushPopRand(MethodBase method, HarmonyMethod transpiler = null)
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
        public static void PatchUnityRand(IEnumerable<string> methods, bool patchPushPop = true)
        {
            foreach (var method in methods)
                PatchUnityRand(AccessTools.DeclaredMethod(method) ?? AccessTools.Method(method), patchPushPop);
        }

        /// <summary>Patches out <see cref="UnityEngine.Random"/> calls using <see cref="FixUnityRNG(IEnumerable{CodeInstruction})"/>, and optionally surrounds the method with <see cref="Rand.PushState"/> and <see cref="Rand.PopState"/>.</summary>
        /// <param name="methods">Methods that needs patching</param>
        /// <param name="patchPushPop">Determines if the methods should be surrounded with push/pop calls</param>
        public static void PatchUnityRand(IEnumerable<MethodBase> methods, bool patchPushPop = true)
        {
            foreach (var method in methods)
                PatchUnityRand(method, patchPushPop);
        }

        /// <summary>Patches out <see cref="UnityEngine.Random"/> calls using <see cref="FixUnityRNG(IEnumerable{CodeInstruction})"/>, and optionally surrounds the method with <see cref="Rand.PushState"/> and <see cref="Rand.PopState"/>.</summary>
        /// <param name="method">Methods that needs patching</param>
        /// <param name="patchPushPop">Determines if the methods should be surrounded with push/pop calls</param>
        public static void PatchUnityRand(string method, bool patchPushPop = true)
            => PatchUnityRand(AccessTools.DeclaredMethod(method) ?? AccessTools.Method(method), patchPushPop);

        /// <summary>Patches out <see cref="UnityEngine.Random"/> calls using <see cref="FixUnityRNG(IEnumerable{CodeInstruction})"/>, and optionally surrounds the method with <see cref="Rand.PushState"/> and <see cref="Rand.PopState"/>.</summary>
        /// <param name="method">Method that needs patching</param>
        /// <param name="patchPushPop">Determines if the method should be surrounded with push/pop calls</param>
        public static void PatchUnityRand(MethodBase method, bool patchPushPop = true)
        {
            var transpiler = new HarmonyMethod(typeof(PatchingUtilities), nameof(FixUnityRNG));

            if (patchPushPop)
                PatchPushPopRand(method, transpiler);
            else
                MpCompat.harmony.Patch(method, transpiler: transpiler);
        }

        #endregion

        #region System RNG transpiler
        private static readonly ConstructorInfo SystemRandConstructor = typeof(System.Random).GetConstructor(Type.EmptyTypes);
        private static readonly ConstructorInfo SystemRandSeededConstructor = typeof(System.Random).GetConstructor(new[] { typeof(int) });
        private static readonly ConstructorInfo RandRedirectorConstructor = typeof(RandRedirector).GetConstructor(Type.EmptyTypes);
        private static readonly ConstructorInfo RandRedirectorSeededConstructor = typeof(RandRedirector).GetConstructor(new[] { typeof(int) });

        /// <summary>Transpiler that replaces all calls to <see cref="System.Random"/> constructor with calls to <see cref="RandRedirector"/> constructor</summary>
        internal static IEnumerable<CodeInstruction> FixRNG(IEnumerable<CodeInstruction> instr, MethodBase original)
        {
            var anythingPatched = false;
            
            foreach (var ci in instr)
            {
                if (ci.opcode == OpCodes.Newobj && ci.operand is ConstructorInfo constructorInfo)
                {
                    if (constructorInfo == SystemRandConstructor)
                    {
                        ci.operand = RandRedirectorConstructor;
                        anythingPatched = true;
                    }
                    else if (constructorInfo == SystemRandSeededConstructor)
                    {
                        ci.operand = RandRedirectorSeededConstructor;
                        anythingPatched = true;
                    }
                }

                yield return ci;
            }

            if (!anythingPatched) Log.Warning($"No System RNG was patched for method: {original?.FullDescription() ?? "(unknown method)"}");
        }

        /// <summary>This class allows replacing any <see cref="System.Random"/> calls with <see cref="Verse.Rand"/> calls</summary>
        public class RandRedirector : Random
        {
            private static RandRedirector instance;
            public static RandRedirector Instance => instance ??= new RandRedirector();
            
            public RandRedirector()
            { }

            public RandRedirector(int seed) : base(seed)
            { }

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

        internal static IEnumerable<CodeInstruction> FixUnityRNG(IEnumerable<CodeInstruction> instr, MethodBase original)
        {
            var anythingPatched = false;
            
            foreach (var ci in instr)
            {
                if (ci.opcode == OpCodes.Call && ci.operand is MethodInfo method)
                {
                    if (method == UnityRandomRangeInt || method == UnityRandomRangeIntObsolete)
                    {
                        ci.operand = VerseRandomRangeInt;
                        anythingPatched = true;
                    }
                    else if (method == UnityRandomRangeFloat || method == UnityRandomRangeFloatObsolete)
                    {
                        ci.operand = VerseRandomRangeFloat;
                        anythingPatched = true;
                    }
                    else if (method == UnityRandomValue)
                    {
                        ci.operand = VerseRandomValue;
                        anythingPatched = true;
                    }
                    else if (method == UnityInsideUnitCircle)
                    {
                        ci.operand = VerseInsideUnitCircle;
                        anythingPatched = true;
                    }
                }

                yield return ci;
            }

            if (!anythingPatched) Log.Warning($"No Unity RNG was patched for method: {original?.FullDescription() ?? "(unknown method)"}");
        }
        #endregion

        #region Cancel on UI

        /// <summary>
        /// <para>Returns <see langword="true"/> during ticking and synced commands.</para>
        /// <para>Should be used to prevent any gameplay-related changes from being executed from UI or other unsafe contexts.</para>
        /// <para>Requires calling <see cref="InitCancelOnUI"/> or any <see cref="PatchCancelMethodOnUI"/> method, otherwise it will always be <see langword="false"/>.</para>
        /// </summary>
        public static bool ShouldCancel => (bool)(shouldCancelUiMethod?.Invoke(null) ?? false);

        private static FastInvokeHandler shouldCancelUiMethod;

        /// <summary>Patches the method to cancel the call if it ends up being called from the UI</summary>
        /// <param name="methodNames">Names (type colon name) of the methods to patch</param>
        public static void PatchCancelMethodOnUI(params string[] methodNames) => PatchCancelMethodOnUI(methodNames as IEnumerable<string>);

        /// <summary>Patches the method to cancel the call if it ends up being called from the UI</summary>
        /// <param name="methodNames">Names (type colon name) of the methods to patch</param>
        public static void PatchCancelMethodOnUI(IEnumerable<string> methodNames)
            => PatchCancelMethodOnUI(methodNames
                .Select(m =>
                {
                    var method = AccessTools.DeclaredMethod(m) ?? AccessTools.Method(m);
                    if (method == null)
                        Log.Error($"({nameof(PatchingUtilities)}) Could not find method {m}");
                    return method;
                })
                .Where(m => m != null));

        /// <summary>Patches the method to cancel the call if it ends up being called from the UI</summary>
        /// <param name="methods">Methods to patch</param>
        public static void PatchCancelMethodOnUI(params MethodBase[] methods) => PatchCancelMethodOnUI(methods as IEnumerable<MethodBase>);

        /// <summary>Patches the method to cancel the call if it ends up being called from the UI</summary>
        /// <param name="methods">Methods to patch</param>
        public static void PatchCancelMethodOnUI(IEnumerable<MethodBase> methods)
        {
            InitCancelOnUI();
            foreach (var method in methods)
                Patch(method);
        }

        private static void Patch(MethodBase method)
        {
            MpCompat.harmony.Patch(method,
                prefix: new HarmonyMethod(typeof(PatchingUtilities), nameof(CancelDuringAlerts)));
        }

        /// <summary>
        /// <para>Gets access to Multiplayer.Client.AppendMoodThoughtsPatch:Cancel getter to check if execution should be cancelled during alerts.</para>
        /// <para>Called automatically from any <see cref="PatchCancelMethodOnUI"/> method.</para>
        /// </summary>
        public static void InitCancelOnUI()
            => shouldCancelUiMethod ??= MethodInvoker.GetHandler(AccessTools.PropertyGetter("Multiplayer.Client.AppendMoodThoughtsPatch:Cancel"));

        private static bool CancelDuringAlerts() => !ShouldCancel;

        #endregion

        #region Find.CurrentMap replacer

        public static void ReplaceCurrentMapUsage(MethodBase method)
        {
            if (method != null)
                MpCompat.harmony.Patch(method, transpiler: new HarmonyMethod(typeof(PatchingUtilities), nameof(ReplaceCurrentMapUsageTranspiler)));
        }

        private static IEnumerable<CodeInstruction> ReplaceCurrentMapUsageTranspiler(IEnumerable<CodeInstruction> instr, MethodBase baseMethod)
        {
            var helper = new CurrentMapPatchHelper(baseMethod);
            var patched = false;

            foreach (var ci in instr)
            {
                yield return ci;

                // Process current instruction and (if we got new instructions to insert) - yield return them as well.
                foreach (var newInstr in helper.ProcessCurrentInstruction(ci))
                {
                    yield return newInstr;
                    patched = true;
                }
            }

            var name = (baseMethod.DeclaringType?.Namespace).NullOrEmpty() ? baseMethod.Name : $"{baseMethod.DeclaringType!.Name}.{baseMethod.Name}";

            if (!helper.IsSupported)
                Log.Warning($"Unsupported type, can't patch current map usage for {name}");
            else if (!patched)
                Log.Warning($"Failed patching current map usage for {name}");
#if DEBUG
            else
                Log.Warning($"Successfully patched the current map usage for {name}");
#endif
        }

        private class CurrentMapPatchHelper
        {
            private enum CurrentMapUserType
            {
                ThingComp,
                HediffComp,
                GameCondition,
                IncidentParmsArg,
                UnsupportedType,
            }

            private static readonly MethodInfo targetMethod = AccessTools.PropertyGetter(typeof(Find), nameof(Find.CurrentMap));

            private readonly CurrentMapUserType currentType;
            private readonly MethodBase baseMethod;
            private readonly int argIndex;

            public bool IsSupported => currentType != CurrentMapUserType.UnsupportedType;

            public CurrentMapPatchHelper(MethodBase baseMethod)
            {
                this.baseMethod = baseMethod ?? throw new ArgumentNullException(nameof(baseMethod));
                currentType = GetMapForMethod(this.baseMethod, out argIndex);
            }

            public IEnumerable<CodeInstruction> ProcessCurrentInstruction(CodeInstruction ci)
            {
                if (currentType == CurrentMapUserType.UnsupportedType)
                    yield break;

                if (ci.opcode != OpCodes.Call || ci.operand is not MethodInfo method || method != targetMethod)
                    yield break;

                switch (currentType)
                {
                    case CurrentMapUserType.ThingComp:
                    {
                        // Replace the current instruction with call to `this`
                        ci.opcode = OpCodes.Ldarg_0;
                        ci.operand = null;

                        // Get the parent field
                        yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.DeclaredField(typeof(ThingComp), nameof(ThingComp.parent)));
                        // Call the parent's Map getter
                        yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.Map)));

                        break;
                    }
                    case CurrentMapUserType.HediffComp:
                    {
                        // Replace the current instruction with call to `this`
                        ci.opcode = OpCodes.Ldarg_0;
                        ci.operand = null;

                        // Call the comp's Pawn getter (which is just short for getting parent field followed by pawn field)
                        yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(HediffComp), nameof(HediffComp.Pawn)));
                        // Call the pawn's Map getter
                        yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(Pawn), nameof(Pawn.Map)));
                        break;
                    }
                    case CurrentMapUserType.GameCondition:
                    {
                        // Replace the current instruction with call to `this`
                        ci.opcode = OpCodes.Ldarg_0;
                        ci.operand = null;

                        // Call the `SingleMap` getter
                        yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(GameCondition), nameof(GameCondition.SingleMap)));
                        break;
                    }
                    case CurrentMapUserType.IncidentParmsArg:
                    {
                        var instr = GetLdargForIndex(method, argIndex);
                        if (instr != null)
                        {
                            // Replace the current instruction with call to the specific argument
                            ci.opcode = instr.opcode;
                            ci.operand = instr.operand;

                            // Load the `IncidentParms.target` field
                            yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.DeclaredField(typeof(IncidentParms), nameof(IncidentParms.target)));

                            // Cast the IIncidentTarget to Map
                            yield return new CodeInstruction(OpCodes.Castclass, typeof(Map));
                        }

                        break;
                    }
                }
            }

            private static CurrentMapUserType GetMapForMethod(MethodBase method, out int argIndex)
            {
                argIndex = -1;

                if (method.DeclaringType == null)
                    return CurrentMapUserType.UnsupportedType;

                if (typeof(ThingComp).IsAssignableFrom(method.DeclaringType))
                {
                    if (!method.IsStatic)
                        return CurrentMapUserType.ThingComp;
                }
                else if (typeof(HediffComp).IsAssignableFrom(method.DeclaringType))
                {
                    if (!method.IsStatic)
                        return CurrentMapUserType.HediffComp;
                }
                else if (typeof(GameCondition).IsAssignableFrom(method.DeclaringType))
                {
                    if (!method.IsStatic)
                        return CurrentMapUserType.GameCondition;
                }
                else
                {
                    argIndex = method.GetParameters().FirstIndexOf(p => typeof(IncidentParms).IsAssignableFrom(p.ParameterType));
                    if (argIndex >= 0)
                        return CurrentMapUserType.IncidentParmsArg;
                }

                return CurrentMapUserType.UnsupportedType;
            }

            private static CodeInstruction GetLdargForIndex(MethodBase method, int index)
            {
                if (index < 0)
                    return null;

                // For non-static method, arg 0 is `this`
                if (!method.IsStatic)
                    index++;

                return index switch
                {
                    0 => new CodeInstruction(OpCodes.Ldarg_0),
                    1 => new CodeInstruction(OpCodes.Ldarg_1),
                    2 => new CodeInstruction(OpCodes.Ldarg_2),
                    3 => new CodeInstruction(OpCodes.Ldarg_3),
                    <= byte.MaxValue => new CodeInstruction(OpCodes.Ldarg_S, index),
                    <= ushort.MaxValue => new CodeInstruction(OpCodes.Ldarg, index),
                    _ => null
                };
            }
        }

        #endregion
    }
}
