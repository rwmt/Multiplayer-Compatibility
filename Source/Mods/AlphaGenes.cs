using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>
    /// Alpha Genes by Allie, Sarg
    /// <see href="https://github.com/juanosarg/AlphaGenes"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2891845502"/>
    /// </summary>
    [MpCompatFor("sarg.alphagenes")]
    internal class AlphaGenes
    {
        public AlphaGenes(ModContentPack mod)
        {
            // RNG
            {
                PatchingUtilities.PatchSystemRandCtor("AlphaGenes.CompAbilityOcularConversion", false);
                PatchingUtilities.PatchSystemRand("AlphaGenes.CompInsanityBlast:Apply", false);
                PatchingUtilities.PatchSystemRand("AlphaGenes.HediffComp_Parasites:Hatch", false);
            }

            // Abilities
            {
                MpCompat.RegisterLambdaMethod("AlphaGenes.Ability_MineralOverdrive", "GetGizmos", 0, 2);
                MpCompat.RegisterLambdaMethod("AlphaGenes.Ability_ReactiveArmour", "GetGizmos", 0, 2);
                MpCompat.RegisterLambdaDelegate("AlphaGenes.CompScorpionCounter", "CompGetGizmosExtra", 0).SetDebugOnly();
            }

            // Current map usage
            {
                // Debug stuff, may get changed or removed. And we don't fully care if it fails.
                try
                {
                    MpCompat.harmony.Patch(
                        AccessTools.DeclaredMethod("AlphaGenes.CompRandomItemSpawner:CompTick"),
                        transpiler: new HarmonyMethod(typeof(AlphaGenes), nameof(UseParentMap)));
                }
                catch (Exception e)
                {
                    Log.Warning($"Failed to patch method AlphaGenes.CompRandomItemSpawner:CompTick for dev mode item, the rest of the patch should still work.\n{e}");
                }
            }
        }

        private static IEnumerable<CodeInstruction> UseParentMap(IEnumerable<CodeInstruction> instr)
        {
            var instrArr = instr.ToArray();
            var patched = false;

            var targetMethod = AccessTools.PropertyGetter(typeof(Find), nameof(Find.CurrentMap));

            foreach (var ci in instrArr)
            {
                if (ci.opcode == OpCodes.Call && ci.operand is MethodInfo method && method == targetMethod)
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(ThingComp), nameof(ThingComp.parent)));

                    ci.opcode = OpCodes.Callvirt;
                    ci.operand = AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.Map));

                    patched = true;
                }
                
                yield return ci;
            }

            if (!patched)
                throw new Exception("Method was not patched");
        }
    }
}