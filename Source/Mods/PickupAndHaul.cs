using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Pick Up And Haul by Mehni</summary>
    /// <see href="https://github.com/Mehni/PickUpAndHaul/"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=1279012058"/>
    [MpCompatFor("Mehni.PickUpAndHaul")]
    public class PickupAndHaul
    {
        public PickupAndHaul(ModContentPack mod)
        {
            // Sorts the ListerHaulables list from UI, causes issues
            MpCompat.harmony.Patch(AccessTools.Method("PickUpAndHaul.WorkGiver_HaulToInventory:PotentialWorkThingsGlobal"),
                transpiler: new HarmonyMethod(typeof(PickupAndHaul), nameof(Transpiler)));
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instr)
        {
            var target = AccessTools.Method(typeof(ListerHaulables), nameof(ListerHaulables.ThingsPotentiallyNeedingHauling));
            var newListCtor = AccessTools.Constructor(typeof(List<Thing>), new[] { typeof(IEnumerable<Thing>) });

            var patched = false;
            foreach (var ci in instr)
            {
                yield return ci;

                if (ci.opcode == OpCodes.Callvirt && ci.operand is MethodInfo method && method == target)
                {
                    yield return new CodeInstruction(OpCodes.Newobj, newListCtor);
                    patched = true;
                }
            }

            if (!patched)
                throw new Exception("Failed patching Pickup and Haul");
        }
    }
}