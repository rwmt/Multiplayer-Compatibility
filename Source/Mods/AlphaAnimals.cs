using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Alpha Animals by Sarg Bjornson</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=1541721856"/>
    /// contribution to Multiplayer Compatibility by Reshiram and Sokyran
    [MpCompatFor("Alpha Animals")]
    class AlphaBehavioursAndEvents
    {
        static MethodInfo RandRangeInt = typeof(Verse.Rand).GetMethod("Range", new Type[] { typeof(int), typeof(int) });
        static MethodInfo RandRangeFloat = typeof(Verse.Rand).GetMethod("Range", new Type[] { typeof(float), typeof(float) });
        static MethodInfo SystemNext = typeof(System.Random).GetMethod("Next", Array.Empty<Type>());
        static MethodInfo SystemNextMax = typeof(System.Random).GetMethod("Next", new Type[] { typeof(int) });
        static MethodInfo SystemNextMinMax = typeof(System.Random).GetMethod("Next", new Type[] { typeof(int), typeof(int) });
        static MethodInfo SystemNextDouble = typeof(System.Random).GetMethod("NextDouble");
        public AlphaBehavioursAndEvents(ModContentPack mod)
        {
            //RNG Fix
            {
                var rngFixMethods = new[] { //System.Random fixes
                    AccessTools.Method("AlphaBehavioursAndEvents.CompGasProducer:CompTick"),
                    AccessTools.Method("AlphaBehavioursAndEvents.CompAnimalProduct:InformGathered"),
                    AccessTools.Method("AlphaBehavioursAndEvents.CompInitialHediff:CompTickRare"),
                    AccessTools.Method("AlphaBehavioursAndEvents.Gas_Ocular:Tick"),
                    AccessTools.Method("AlphaBehavioursAndEvents.Hediff_Crushing:RandomFilthGenerator")
                };
                foreach (var method in rngFixMethods)
                {
                    MpCompat.harmony.Patch(method,
                        prefix: new HarmonyMethod(typeof(AlphaBehavioursAndEvents), nameof(FixRNGPre)),
                        postfix: new HarmonyMethod(typeof(AlphaBehavioursAndEvents), nameof(FixRNGPos)),
                        transpiler: new HarmonyMethod(typeof(AlphaBehavioursAndEvents), nameof(FixRNG))
                    );
                }
            }
        }
        static void FixRNGPre() => Rand.PushState();
        static void FixRNGPos() => Rand.PopState();
        public static IEnumerable<CodeInstruction> FixRNG(IEnumerable<CodeInstruction> instr)
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
    }
}