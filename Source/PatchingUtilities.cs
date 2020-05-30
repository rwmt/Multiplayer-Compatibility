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
        private static ConstructorInfo SystemRandConstructor = typeof(System.Random).GetConstructor(Array.Empty<Type>());
        #endregion

        /// <summary>Method intended for patching <see cref="System.Random"/> calls out of instructions. Does not support <see cref="System.Random.NextBytes(byte[])"/>.</summary>
        /// <param name="instr">Instructions that need patching</param>
        /// Currently, it only works with methods that are accessing System.Random calls for fields in classes or initialize System.Random once in the method
        /// Contribution to Multiplayer Compatibility by Sokyran and Reshiram
        /// Please report issues with this method with .dll or .exe files (along with information on where to find the problematic code) or IL instructions related to the call
        internal static IEnumerable<CodeInstruction> FixRNG(IEnumerable<CodeInstruction> instr)
        {
            OpCode? localRandomOpCode = null;
            object localRandomOperand = null;

            for (int i = 0; i < instr.Count(); i++)
            {
                var ci = instr.ElementAt(i);

                // Checking if random is defined inside of the method (there's high chance it won't work if random is defined more than once)
                if (!localRandomOpCode.HasValue && ci.opcode == OpCodes.Newobj && ci.operand is ConstructorInfo constructor && constructor == SystemRandConstructor)
                {
                    // Check if there's at least one more instruction (just in case)
                    if (i + 1 < instr.Count())
                        // Get equivalent Ldloc to our Stloc (assuming it's Stloc in the first place), so we can patch all apperances of it
                        GetLdlocFromStloc(instr.ElementAt(i + 1), out localRandomOpCode, out localRandomOperand);
                    // If we fou the random, then skip the next instructions (stloc)
                    // We still need to include them in case any code branches jump to them, to prevent jumping to non-existant instruction
                    // An alternative approach would be much more complicated to implement, as it would require patching any jumps to another instruction
                    if (localRandomOpCode.HasValue)
                    {
                        i++;
                        ci.opcode = OpCodes.Nop;
                        ci.operand = null;
                        yield return ci;
                        ci = instr.ElementAt(i);
                        ci.opcode = OpCodes.Nop;
                        ci.operand = null;
                    }
                    // If what we found isn't random, then just treat the instructions as normal
                    yield return ci;
                }
                // Looking for calls to System.Random
                else if (ci.opcode == OpCodes.Callvirt && ci.operand is MethodInfo operand)
                {
                    // Patching System.Next() as Verse.Rand.Range(0, int.MaxValue)
                    if (operand == SystemNext)
                    {
                        int result = -1;
                        if (localRandomOpCode.HasValue)
                        {
                            // Replace loading local random field with our first parameter
                            result = PatchEarlierInstruction(instr, i, localRandomOpCode.Value, OpCodes.Ldc_I4_0, localRandomOperand);
                            // Add a parameter for max value (since we don't have instructions to replace anymore)
                            if (result != -1)
                                yield return new CodeInstruction(opcode: OpCodes.Ldc_I4, operand: int.MaxValue);
                        }
                        if (result == -1)
                        {
                            // Replace random loading from field into our rand parameters
                            var pos = PatchEarlierInstruction(instr, i, OpCodes.Ldfld, OpCodes.Ldc_I4, typeof(System.Random), int.MaxValue);
                            result = PatchEarlierInstruction(instr, pos, OpCodes.Ldarg_0, OpCodes.Ldc_I4_0);
                        }

                        if (result != -1)
                        {
                            ci.opcode = OpCodes.Call;
                            ci.operand = RandRangeInt;
                        }
                    }
                    // Patching System.Next(max) as Verse.Rand.Range(0, max)
                    else if (operand == SystemNextMax)
                    {
                        int result = -1;
                        if (localRandomOpCode.HasValue)
                        {
                            // Replace loading local random field with our first parameter
                            result = PatchEarlierInstruction(instr, i, localRandomOpCode.Value, OpCodes.Ldc_I4_0, localRandomOperand);
                        }
                        if (result == -1)
                        {
                            // Replace random loading from field into our first parameter and nop (as one of the parameters is provided)
                            var pos = PatchEarlierInstruction(instr, i, OpCodes.Ldfld, OpCodes.Ldc_I4_0, typeof(System.Random));
                            result = PatchEarlierInstruction(instr, pos, OpCodes.Ldarg_0, OpCodes.Nop);
                        }

                        if (result != -1)
                        {
                            ci.opcode = OpCodes.Call;
                            ci.operand = RandRangeInt;
                        }
                    }
                    // Patching System.Next(min, max) as Verse.Rand.Range(min, max)
                    else if (operand == SystemNextMinMax)
                    {
                        int result = -1;
                        if (localRandomOpCode.HasValue)
                        {
                            // Replace loading local random field with nop (as we have both of our parameters)
                            result = PatchEarlierInstruction(instr, i, localRandomOpCode.Value, OpCodes.Nop, localRandomOperand);
                        }
                        if (result == -1)
                        {
                            // Replace random loading from field into nop (as the parameters are already provided)
                            var pos = PatchEarlierInstruction(instr, i, OpCodes.Ldfld, OpCodes.Nop, typeof(System.Random));
                            result = PatchEarlierInstruction(instr, pos, OpCodes.Ldarg_0, OpCodes.Nop);
                        }

                        if (result != -1)
                        {
                            ci.opcode = OpCodes.Call;
                            ci.operand = RandRangeInt;
                        }
                    }
                    // Patching System.NextDouble() as Verse.Rand.Range(0f, 1f)
                    else if (operand == SystemNextDouble)
                    {
                        int result = -1;
                        if (localRandomOpCode.HasValue)
                        {
                            // Replace loading local random field with our first parameter
                            result = PatchEarlierInstruction(instr, i, localRandomOpCode.Value, OpCodes.Ldc_R4, localRandomOperand, 0f);
                            // Add a parameter for max value (since we don't have instructions to replace anymore)
                            if (result != -1)
                                yield return new CodeInstruction(opcode: OpCodes.Ldc_R4, operand: 1f);
                        }
                        if (result == -1)
                        {
                            // Replace random loading from field into our rand parameters
                            var pos = PatchEarlierInstruction(instr, i, OpCodes.Ldfld, OpCodes.Ldc_R4, typeof(System.Random), 1f);
                            result = PatchEarlierInstruction(instr, pos, OpCodes.Ldarg_0, OpCodes.Ldc_R4, null, 0f);
                        }

                        if (result != -1)
                        {
                            ci.opcode = OpCodes.Call;
                            ci.operand = RandRangeFloat;
                        }
                    }
                    // We return the same instrucion, although with changed opcode and operand (if possible) to avoid issues with jumps
                    // This shouldn't really be a possibility here, but it's better to be safe just in case
                    yield return ci;
                }
                else yield return ci;
            }
        }

        private static int PatchEarlierInstruction(IEnumerable<CodeInstruction> instr, int pos, OpCode codeFind, OpCode codeReplace, object operandFind = null, object operandReplace = null)
        {
            while (pos > 0)
            {
                pos--;

                var currentOperation = instr.ElementAt(pos);
                if (currentOperation.opcode == codeFind)
                {
                    if (operandFind == null || (operandFind is Type type && currentOperation.operand is FieldInfo info && info.FieldType == type) || (currentOperation.operand == operandFind))
                    {
                        currentOperation.opcode = codeReplace;
                        currentOperation.operand = operandReplace;
                        return pos;
                    }
                }
            }

            return -1;
        }

        private static void GetLdlocFromStloc(CodeInstruction instruction, out OpCode? randomCode, out object operand)
        {
            randomCode = null;
            operand = null;

            var opcode = instruction.opcode;

            if (opcode == OpCodes.Stloc_0)
                randomCode = OpCodes.Ldloc_0;
            else if (opcode == OpCodes.Stloc_1)
                randomCode = OpCodes.Ldloc_1;
            else if (opcode == OpCodes.Stloc_2)
                randomCode = OpCodes.Ldloc_2;
            else if (opcode == OpCodes.Stloc_3)
                randomCode = OpCodes.Ldloc_3;
            else if (opcode == OpCodes.Stloc_S)
            {
                randomCode = OpCodes.Ldloc_S;
                operand = instruction.operand;
            }
            else if (opcode == OpCodes.Stloc)
            {
                randomCode = OpCodes.Ldloc;
                operand = instruction.operand;
            }
        }
        #endregion
    }
}
