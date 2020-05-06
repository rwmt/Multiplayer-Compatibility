using System;
using System.Collections.Generic;
using System.Linq;
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

        /// <summary>Surrounds method with <see cref="Rand.PushState"/> and <see cref="Rand.PopState"/> as well as patches out <see cref="System.Random"/> calls using <see cref="FixRNG(IEnumerable{CodeInstruction})"/>.</summary>
        /// <param name="methods">Methods that needs patching (as string)</param>
        internal static void PatchSystemRand(string[] methods)
        {
            foreach (var method in methods)
            {
                PatchSystemRand(AccessTools.Method(method));
            }
        }

        /// <summary>Surrounds method with <see cref="Rand.PushState"/> and <see cref="Rand.PopState"/> as well as patches out <see cref="System.Random"/> calls using <see cref="FixRNG(IEnumerable{CodeInstruction})"/>.</summary>
        /// <param name="methods">Methods that needs patching</param>
        internal static void PatchSystemRand(MethodInfo[] methods)
        {
            foreach (var method in methods)
            {
                PatchSystemRand(method);
            }
        }

        /// <summary>Surrounds method with <see cref="Rand.PushState"/> and <see cref="Rand.PopState"/> as well as patches out <see cref="System.Random"/> calls using <see cref="FixRNG(IEnumerable{CodeInstruction})"/>.</summary>
        /// <param name="method">Method that needs patching</param>
        internal static void PatchSystemRand(MethodInfo method) => PatchPushPopRand(method, new HarmonyMethod(typeof(PatchingUtilities), nameof(FixRNG)));

        /// <summary>Surrounds method with <see cref="Rand.PushState"/> and <see cref="Rand.PopState"/>, as well as applies the transpiler (if provided).</summary>
        /// <param name="methods">Methods that needs patching (as string)</param>
        /// <param name="transpiler">Transpiler that will be applied to the method</param>
        internal static void PatchPushPopRand(string[] methods, HarmonyMethod transpiler = null)
        {
            foreach (var method in methods)
            {
                PatchPushPopRand(AccessTools.Method(method), transpiler);
            }
        }

        /// <summary>Surrounds method with <see cref="Rand.PushState"/> and <see cref="Rand.PopState"/>, as well as applies the transpiler (if provided).</summary>
        /// <param name="methods">Method that needs patching</param>
        /// <param name="transpiler">Transpiler that will be applied to the method</param>
        internal static void PatchPushPopRand(MethodInfo[] methods, HarmonyMethod transpiler = null)
        {
            foreach (var method in methods)
            {
                PatchPushPopRand(method, transpiler);
            }
        }

        /// <summary>Surrounds method with <see cref="Rand.PushState"/> and <see cref="Rand.PopState"/>, as well as applies the transpiler (if provided).</summary>
        /// <param name="method">Method that needs patching</param>
        /// <param name="transpiler">Transpiler that will be applied to the method</param>
        internal static void PatchPushPopRand(MethodInfo method, HarmonyMethod transpiler = null)
        {
            MpCompat.harmony.Patch(method,
                prefix: new HarmonyMethod(typeof(PatchingUtilities), nameof(FixRNGPre)),
                postfix: new HarmonyMethod(typeof(PatchingUtilities), nameof(FixRNGPos)),
                transpiler: transpiler
            );
        }

        #region RNG transpiler
        #region Method references
        private static MethodInfo RandRangeInt = typeof(Verse.Rand).GetMethod("Range", new Type[] { typeof(int), typeof(int) });
        private static MethodInfo RandRangeFloat = typeof(Verse.Rand).GetMethod("Range", new Type[] { typeof(float), typeof(float) });
        private static MethodInfo SystemNext = typeof(System.Random).GetMethod("Next", Array.Empty<Type>());
        private static MethodInfo SystemNextMax = typeof(System.Random).GetMethod("Next", new Type[] { typeof(int) });
        private static MethodInfo SystemNextMinMax = typeof(System.Random).GetMethod("Next", new Type[] { typeof(int), typeof(int) });
        private static MethodInfo SystemNextDouble = typeof(System.Random).GetMethod("NextDouble");
        #endregion

        /// <summary>Method intended for patching <see cref="System.Random"/> calls out of instructions. Does not support <see cref="System.Random.NextBytes(byte[])"/>.</summary>
        /// <param name="instr">Instructions that need patching</param>
        /// Currently, it only works with methods that are accessing System.Random calls for fields in classes.
        /// Contribution to Multiplayer Compatibility by Sokyran and Reshiram
        /// Please report issues with this method with .dll or .exe files (along with information on where to find the problematic code) or IL instructions related to the call
        internal static IEnumerable<CodeInstruction> FixRNG(IEnumerable<CodeInstruction> instr)
        {
            for (int i = 0; i < instr.Count(); i++)
            {
                var ci = instr.ElementAt(i);
                if (ci.opcode == OpCodes.Callvirt && ci.operand is MethodInfo operand)
                {
                    int PatchEarlierInstruction(int pos, OpCode codeFind, OpCode codeReplace, Type fieldType = null, object operandReplace = null)
                    {
                        while (pos > 0)
                        {
                            pos--;

                            var currentOperation = instr.ElementAt(pos);
                            if (currentOperation.opcode == codeFind)
                            {
                                if (fieldType == null || (currentOperation.operand is FieldInfo info && info.FieldType == fieldType))
                                {
                                    currentOperation.opcode = codeReplace;
                                    currentOperation.operand = operandReplace;
                                    return pos;
                                }
                            }
                        }

                        return 0;
                    }
                    if (operand == SystemNext)
                    {
                        var pos = PatchEarlierInstruction(i, OpCodes.Ldfld, OpCodes.Ldc_I4, typeof(System.Random), int.MaxValue);
                        PatchEarlierInstruction(pos, OpCodes.Ldarg_0, OpCodes.Ldc_I4_0);
                        yield return new CodeInstruction(opcode: OpCodes.Call, operand: RandRangeInt);
                    }
                    else if (operand == SystemNextMax)
                    {
                        var pos = PatchEarlierInstruction(i, OpCodes.Ldfld, OpCodes.Ldc_I4_0, typeof(System.Random));
                        PatchEarlierInstruction(pos, OpCodes.Ldarg_0, OpCodes.Nop);
                        yield return new CodeInstruction(opcode: OpCodes.Call, operand: RandRangeInt);
                    }
                    else if (operand == SystemNextMinMax)
                    {
                        var pos = PatchEarlierInstruction(i, OpCodes.Ldfld, OpCodes.Nop, typeof(System.Random));
                        PatchEarlierInstruction(pos, OpCodes.Ldarg_0, OpCodes.Nop);
                        yield return new CodeInstruction(opcode: OpCodes.Call, operand: RandRangeInt);
                    }
                    else if (operand == SystemNextDouble)
                    {
                        var pos = PatchEarlierInstruction(i, OpCodes.Ldfld, OpCodes.Ldc_R4, typeof(System.Random), 1f);
                        PatchEarlierInstruction(pos, OpCodes.Ldarg_0, OpCodes.Ldc_R4, null, 0f);
                        yield return new CodeInstruction(opcode: OpCodes.Call, operand: RandRangeFloat);
                    }
                    else yield return ci;
                }
                else yield return ci;
            }
        }
        #endregion
    }
}
