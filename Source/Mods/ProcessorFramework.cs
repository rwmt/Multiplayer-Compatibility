using System;
using System.Collections;
using System.Linq;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Processor Framework by Syrchalis</summary>
    /// <see href="https://github.com/Syrchalis/ProcessorFramework"/>
    /// <see href="https://steamcommunity.com/workshop/filedetails/?id=2633514537"/>
    [MpCompatFor("syrchalis.processor.framework")]
    internal class ProcessorFramework
    {
        private static AccessTools.FieldRef<object, IDictionary> compProcessorEnabledProcessesField;
        private static AccessTools.FieldRef<object, IList> compProcessorActiveProcessesField;
        private static AccessTools.FieldRef<object, ThingComp> activeProcessProcessorField;

        public ProcessorFramework(ModContentPack mod)
            => LongEventHandler.ExecuteWhenFinished(LatePatch);

        public void LatePatch()
        {
            var type = AccessTools.TypeByName("ProcessorFramework.CompProcessor");
            compProcessorEnabledProcessesField = AccessTools.FieldRefAccess<IDictionary>(type, "enabledProcesses");
            compProcessorActiveProcessesField = AccessTools.FieldRefAccess<IList>(type, "activeProcesses");
            MP.RegisterSyncMethod(type, "EnableAllProcesses");
            MP.RegisterSyncMethod(type, "ToggleProcess");
            MP.RegisterSyncMethod(type, "ToggleIngredient");

            type = AccessTools.TypeByName("ProcessorFramework.ProcessorFramework_Utility");
            MP.RegisterSyncMethod(type, "SetEmptyNow").SetContext(SyncContext.MapSelected);
            // MpCompat.RegisterLambdaDelegate(type, "DebugOptions", 3, 4, 5, 7, 9).SetDebugOnly(); Not being synced?

            type = AccessTools.TypeByName("ProcessorFramework.Command_Quality");
            MP.RegisterSyncMethod(type, "ChangeQuality").SetContext(SyncContext.MapSelected);

            type = AccessTools.TypeByName("ProcessorFramework.ITab_ProcessorContents");
            MpCompat.RegisterLambdaDelegate(type, "GetProgressQualityDropdowns", 0);

            type = AccessTools.TypeByName("ProcessorFramework.ActiveProcess");
            activeProcessProcessorField = AccessTools.FieldRefAccess<ThingComp>(type, "processor");
            MP.RegisterSyncWorker<object>(SyncActiveProcess, type);

            type = AccessTools.TypeByName("ProcessorFramework.ITab_ProcessSelection");
            MP.RegisterSyncMethod(typeof(ProcessorFramework), nameof(SyncedClear));
            MpCompat.harmony.Patch(AccessTools.Method(type, "FillTab"),
                prefix: new HarmonyMethod(typeof(ProcessorFramework), nameof(PreFillTab)),
                postfix: new HarmonyMethod(typeof(ProcessorFramework), nameof(PostFillTab)));
        }

        private static void SyncActiveProcess(SyncWorker sync, ref object process)
        {
            if (sync.isWriting)
            {
                var comp = activeProcessProcessorField(process);
                var index = compProcessorActiveProcessesField(comp).IndexOf(process);
                sync.Write(index);
                if (index >= 0) 
                    sync.Write(comp);
            }
            else
            {
                var index = sync.Read<int>();
                if (index >= 0)
                    process = compProcessorActiveProcessesField(sync.Read<ThingComp>())[index];
            }
        }

        private static void PreFillTab(IEnumerable ___processorComps, ref DictionaryEntry[][] __state)
        {
            if (___processorComps == null)
            {
                __state = null;
                return;
            }

            var arr = ___processorComps as object[] ?? ___processorComps.Cast<object>().ToArray();

            __state = new DictionaryEntry[arr.Length][];
            for (var i = 0; i < arr.Length; i++)
            {
                var dict = compProcessorEnabledProcessesField(arr[i]);
                if (dict == null)
                    __state[i] = Array.Empty<DictionaryEntry>();
                else
                {
                    __state[i] = new DictionaryEntry[dict.Count];
                    dict.CopyTo(__state[i], 0);
                }
            }
        }

        private static void PostFillTab(IEnumerable ___processorComps, ref DictionaryEntry[][] __state)
        {
            if (__state == null)
                return;

            var arr = ___processorComps as ThingComp[] ?? ___processorComps.Cast<ThingComp>().ToArray();

            for (var i = 0; i < arr.Length; i++)
            {
                if (__state[i].Length > 0 && compProcessorEnabledProcessesField(arr[i]).Count == 0)
                {
                    for (i = 0; i < arr.Length; i++)
                    {
                        var dict = compProcessorEnabledProcessesField(arr[i]);
                        dict.Clear();
                        foreach (var entry in __state[i])
                            dict.Add(entry.Key, entry.Value);
                    }

                    SyncedClear(arr);
                    return;
                }
            }
        }

        private static void SyncedClear(ThingComp[] comps)
        {
            foreach (var comp in comps) 
                compProcessorEnabledProcessesField(comp).Clear();
        }
    }
}
