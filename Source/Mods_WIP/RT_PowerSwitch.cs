using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Harmony;
using Multiplayer.API;
using Multiplayer.Client;
using RimWorld;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Power Switch by Ratys</summary>
    /// <remarks>Fails to work, desyncs on the tick itself.</remarks>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=728314859"/>
    /// <see href="https://github.com/Ratysz/RT_PowerSwitch"/>
    //[MpCompatFor("RT Power Switch")]
    public class RTPowerSwitchCompat
    {
        static Type CompRTPowerSwitchType;
        static MethodInfo CompTickMethod;
        static MethodInfo ProcessCellMethod;

        static ISyncMethod OmgPlzMethod;

        public RTPowerSwitchCompat(ModContentPack mod)
        {
            CompRTPowerSwitchType = AccessTools.TypeByName("RT_PowerSwitch.CompRTPowerSwitch");
            CompTickMethod = AccessTools.Method(CompRTPowerSwitchType, "CompTick");
            ProcessCellMethod = AccessTools.Method(CompRTPowerSwitchType, "ProcessCell");

            // compiler generated
            MP.RegisterSyncMethod(CompRTPowerSwitchType, "<CompGetGizmosExtra>b__15_1");

            OmgPlzMethod = MP.RegisterSyncMethod(typeof(RTPowerSwitchCompat), nameof(Omgplz));

            MpCompat.harmony.Patch(
                ProcessCellMethod,
                transpiler: new HarmonyMethod(typeof(RTPowerSwitchCompat), nameof(ProcessCellTranspile))
                );
        }

        static IEnumerable<CodeInstruction> ProcessCellTranspile(IEnumerable<CodeInstruction> e, MethodBase original)
        {
            MethodInfo UpdateFlickDesignationMethod = AccessTools.Method(typeof(FlickUtility), nameof(FlickUtility.UpdateFlickDesignation));

            MethodInfo SwitchOnMethod = AccessTools.Method(typeof(RTPowerSwitchCompat), nameof(SwitchOn));
            MethodInfo SwitchOffMethod = AccessTools.Method(typeof(RTPowerSwitchCompat), nameof(SwitchOff));

            var instructions = e.ToList();

            int bgn, end;
            var finder = new CodeFinder(original, instructions);

            bgn = finder.Forward(OpCodes.Stloc_S, 8) + 3;
            end = finder.Forward(OpCodes.Call, UpdateFlickDesignationMethod) + 1;

            instructions.RemoveRange(bgn, end - bgn);
            instructions.Insert(bgn,
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(CompRTPowerSwitchType, "compFlickable")),
                new CodeInstruction(OpCodes.Call, SwitchOnMethod));

            bgn = finder.Forward(OpCodes.Stloc_S, 9) + 3;
            end = finder.Forward(OpCodes.Call, UpdateFlickDesignationMethod) + 1;

            instructions.RemoveRange(bgn, end - bgn);
            instructions.Insert(bgn,
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(CompRTPowerSwitchType, "compFlickable")),
                new CodeInstruction(OpCodes.Call, SwitchOffMethod));

            return instructions;
        }

        static void SwitchOn(CompFlickable comp)
        {
            Log.Message("wants off");

            /*comp.wantSwitchOn = false;

            FlickUtility.UpdateFlickDesignation(comp.parent);*/

            // DESYNCS!!!!

            OmgPlzMethod.DoSync(comp);
        }

        static void Omgplz(CompFlickable comp)
        {
            comp.SwitchIsOn = false;
        }

        static void SwitchOff(CompFlickable comp)
        {
            Log.Message("wants on");

            /*comp.wantSwitchOn = true;

            FlickUtility.UpdateFlickDesignation(comp.parent);*/

            // DESYNCS!!!!
            comp.SwitchIsOn = true;
        }
    }
}
