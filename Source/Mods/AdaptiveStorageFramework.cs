using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Adaptive Storage Framework by bbradson</summary>
    /// <see href="https://github.com/bbradson/Adaptive-Storage-Framework"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3033901359"/>
    [MpCompatFor("adaptive.storage.framework")]
    class AdaptiveStorageFramework
    {
        private static readonly MethodInfo IsInMainThreadGetter
            = AccessTools.PropertyGetter(typeof(UnityData), nameof(UnityData.IsInMainThread));

        public AdaptiveStorageFramework(ModContentPack mod)
        {
            // AS's ThingExtensions cctor reads DefDatabase<ThingDef>
            LongEventHandler.ExecuteWhenFinished(() => MpCompatPatchLoader.LoadPatch(this));
        }

        // Mirrors bbradson/Adaptive-Storage-Framework#30
        [MpCompatTranspiler("AdaptiveStorage.StorageRenderer", "SetPrintDataDirty")]
        private static IEnumerable<CodeInstruction> SetPrintDataDirty_Transpiler(IEnumerable<CodeInstruction> insts)
        {
            foreach (var inst in insts)
            {
                if (inst.Calls(IsInMainThreadGetter))
                    yield return new CodeInstruction(OpCodes.Call,
                        AccessTools.Method(typeof(AdaptiveStorageFramework), nameof(IsInMainThreadAndNotMp)));
                else
                    yield return inst;
            }
        }

        private static bool IsInMainThreadAndNotMp() => UnityData.IsInMainThread && !MP.enabled;
    }
}
