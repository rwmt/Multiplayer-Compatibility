using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>
    /// Vanilla Races Expanded - Phytokin by Oskar Potocki, Sarg Bjornson, Allie, Erin, Sir Van, Reann Shepard
    /// <see href="https://github.com/Vanilla-Expanded/VanillaRacesExpanded-Phytokin"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2927323805"/>
    /// </summary>
    [MpCompatFor("vanillaracesexpanded.phytokin")]
    public class VanillaRacesPhytokin
    {
        public VanillaRacesPhytokin(ModContentPack mod)
        {
            // Plant saplingchild
            MpCompat.RegisterLambdaMethod("VanillaRacesExpandedPhytokin.HediffComp_Saplingchild", "CompGetGizmos", 0);

            // Dev mode gizmos
            // Reset dryad counter (in case of counter breaking)
            MpCompat.RegisterLambdaDelegate("VanillaRacesExpandedPhytokin.CompDryadCounter", "CompGetGizmosExtra", 0).SetDebugOnly();

            var type = AccessTools.TypeByName("VanillaRacesExpandedPhytokin.CompVariablePollutionPump");
            // Force pump now
            MP.RegisterSyncMethod(type, "Pump").SetDebugOnly(); // Also called while ticking
            // Set next pump time
            MpCompat.RegisterLambdaDelegate(type, "CompGetGizmosExtra", 1).SetDebugOnly();
            // Fix the mod using Find.CurrentMap instead of parent.Map
            // Can be safely removed it the following PR is accepted: https://github.com/Vanilla-Expanded/VanillaRacesExpanded-Phytokin/pull/2
            MpCompat.harmony.Patch(AccessTools.DeclaredMethod(type, "CompTick"),
                transpiler: new HarmonyMethod(typeof(VanillaRacesPhytokin), nameof(UseParentMap)));
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
                Log.Warning("Failed patching");
        }
    }
}