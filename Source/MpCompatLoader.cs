using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Mono.Cecil;
using Verse;

namespace Multiplayer.Compat
{
    public static class MpCompatLoader
    {
        internal static void Load(ModContentPack content)
        {
            LoadConditional(content);

            foreach (var asm in content.assemblies.loadedAssemblies)
                InitCompatInAsm(asm);

            ClearCaches();
        }
        
        static void LoadConditional(ModContentPack content)
        {
            var asmPath = ModContentPack
                .GetAllFilesForModPreserveOrder(content, "Referenced/", f => f.ToLower() == ".dll")
                .FirstOrDefault(f => f.Item2.Name == "Multiplayer_Compat_Referenced.dll")?.Item2;

            if (asmPath == null)
            {
                return;
            }

            var asm = AssemblyDefinition.ReadAssembly(asmPath.FullName);

            foreach (var t in asm.MainModule.GetTypes().ToArray())
            {
                var attr = t.CustomAttributes
                    .Where(a => a.Constructor.DeclaringType.Name is nameof(MpCompatForAttribute) or nameof(MpCompatRequireModAttribute))
                    .ToArray();
                if (!attr.Any()) continue;

                var anyMod = attr.Any(a =>
                {
                    var modId = ((string)a.ConstructorArguments.First().Value).ToLower();
                    var mod = LoadedModManager.RunningMods.FirstOrDefault(m => m.PackageId.NoModIdSuffix() == modId);
                    return mod != null;
                });
                
                if (!anyMod)
                    asm.MainModule.Types.Remove(t);
            }

            var stream = new MemoryStream();
            asm.Write(stream);

            var loadedAsm = AppDomain.CurrentDomain.Load(stream.ToArray());
            content.assemblies.loadedAssemblies.Add(loadedAsm);
        }

        static void InitCompatInAsm(Assembly asm)
        {
            var queue = asm.GetTypes()
                .Where(t => t.HasAttribute<MpCompatForAttribute>())
                .SelectMany(
                    t => (MpCompatForAttribute[]) t.GetCustomAttributes(typeof(MpCompatForAttribute), false),
                    (type, compat) => new { type, compat }
                )
                .Join(LoadedModManager.RunningMods,
                    box => box.compat.PackageId.ToLower(),
                    mod => mod.PackageId.NoModIdSuffix(),
                    (box, mod) => new { box.type, mod });

            foreach (var action in queue) 
            {
                try {
                    Activator.CreateInstance(action.type, action.mod);
                    Log.Message($"MPCompat :: Initialized compatibility for {action.mod.PackageId}");
                } catch(Exception e) {
                    Log.Error($"MPCompat :: Exception loading {action.mod.PackageId}: {e.InnerException ?? e}");
                }
            }
        }

        static void ClearCaches()
        {
            // Clear the GenTypes cache first, as MP will use it to create its own cache (built through GenTypes.AllTypes call if null)
            GenTypes.ClearCache();

            // As we're adding the new assembly, the classes added by it aren't included by the MP GenTypes AllSubclasses/AllSubclassesNonAbstract optimization
            // GenTypes.ClearCache() on its own won't work, as MP isn't doing anything when it's called.
            var mpType = AccessTools.TypeByName("Multiplayer.Client.Util.TypeCache") ?? AccessTools.TypeByName("Multiplayer.Client.Multiplayer");
            ((IDictionary)AccessTools.Field(mpType, "subClasses").GetValue(null)).Clear();
            ((IDictionary)AccessTools.Field(mpType, "subClassesNonAbstract").GetValue(null)).Clear();
            ((IDictionary)AccessTools.Field(mpType, "implementations").GetValue(null)).Clear();
            AccessTools.Method(mpType, "CacheTypeHierarchy").Invoke(null, Array.Empty<object>());

            // Clear/re-init the list of ISyncSimple implementations.
            AccessTools.Method("Multiplayer.Client.ImplSerialization:Init").Invoke(null, Array.Empty<object>());
            // Clear/re-init the localDefInfos dictionary so it contains the classes added from referenced assembly.
            AccessTools.Method("Multiplayer.Client.MultiplayerData:CollectDefInfos").Invoke(null, Array.Empty<object>());
        }
    }
}