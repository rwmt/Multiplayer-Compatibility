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
                transpiler: new HarmonyMethod(typeof(PickupAndHaul), nameof(PotentialWorkThingsGlobalTranspiler)));
            
            // Sorts carriedThings
            MpCompat.harmony.Patch(AccessTools.Method("PickUpAndHaul.JobDriver_UnloadYourHauledInventory:FirstUnloadableThing"),
                transpiler: new HarmonyMethod(typeof(PickupAndHaul), nameof(FirstUnloadableThingTranspiler)));
        }

        private static IEnumerable<CodeInstruction> PotentialWorkThingsGlobalTranspiler(IEnumerable<CodeInstruction> instr)
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
                throw new Exception("Failed patching Pickup and Haul: PotentialWorkThingsGlobal");
        }

        private static IEnumerable<CodeInstruction> FirstUnloadableThingTranspiler(IEnumerable<CodeInstruction> instr)
        {
            var patched = false;
            foreach(var ci in instr)
            {
                yield return ci;
                if(!patched && ci.operand is MethodInfo m && m.Name.Contains("ThenBy"))
                {
                    yield return CodeInstruction.Call(typeof(PickupAndHaul), nameof(SortByThingIDNumber));
                    patched = true;
                }
            }

            if (!patched)
                throw new Exception("Failed patching Pickup and Haul: FirstUnloadableThing");
        }

        private static IOrderedEnumerable<Thing> SortByThingIDNumber(IOrderedEnumerable<Thing> carriedThings) => carriedThings.ThenBy(x => x.thingIDNumber);
    }
}