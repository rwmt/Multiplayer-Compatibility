using System;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>RimHUD by Jaxe</summary>
    /// <see href="https://github.com/Jaxe-Dev/RimHUD"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=1508850027"/>
    [MpCompatFor("jaxe.rimhud")]
    public class RimHUD
    {
        public RimHUD(ModContentPack mod)
        {
            // The current RimHUD compat calls `Traverse.Method(string, object[])`, but due to ambiguous match
            // it fails to get the correct RegisterSyncMethod method. The fix would be to use the alternative
            // method which provides arguments for the method.
            // The compat likely broke due to changes in Harmony, likely to prevent it from arbitrarily
            // selecting a method when there was an ambiguous match (like how it happens right now).

            var type = AccessTools.TypeByName("RimHUD.Integration.Multiplayer.Mod_Multiplayer");

            // Currently has a pending PR to fix the compat
            // https://github.com/Jaxe-Dev/RimHUD/pull/12
            var newMethod = AccessTools.DeclaredMethod(
                typeof(Traverse), nameof(Traverse.Method), new []{ typeof(string), typeof(Type[]), typeof(object[]) });
            var instr = PatchProcessor.GetCurrentInstructions(
                AccessTools.DeclaredConstructor(type, Type.EmptyTypes));

            // Check which if the constructor calls `Traverse.Method(string, Type[], object[])`, which
            // is the simplest and most obvious fix and the on implemented in the PR.
            if (instr.Any(ci => ci.Calls(newMethod)))
            {
                Log.Warning("RimHUD doesn't need MP Compatibility patch, the original mod compat was fixed.");
                return;
            }

            // The mod technically has a few more sync methods, but those
            // aren't necessary as it's stuff MP syncs itself.
            MP.RegisterSyncMethod(AccessTools.DeclaredMethod(type, "SetSelfTend"));
        }
    }
}