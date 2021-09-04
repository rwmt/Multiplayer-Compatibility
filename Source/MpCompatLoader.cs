using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    public static class MpCompatLoader
    {
        internal static void Load(ModContentPack content)
        {
            LoadConditional(content);

            foreach (var asm in content.assemblies.loadedAssemblies)
                InitCompatInAsm(asm, "compatibility");
        }
        
        static void LoadConditional(ModContentPack content)
        {
            var asmPath = ModContentPack
                .GetAllFilesForModPreserveOrder(content, "Conditional/", f => f.ToLower() == ".dll")
                .First(f => f.Item2.Name == "Compat_Conditional.dll").Item2;

            var asm = AssemblyDefinition.ReadAssembly(asmPath.FullName);

            foreach (var t in asm.MainModule.GetTypes().ToArray())
            {
                var attr = t.CustomAttributes
                    .FirstOrDefault(a => a.Constructor.DeclaringType.Name == nameof(MpCompatForAttribute));
                if (attr == null) continue;

                var modId = (string)attr.ConstructorArguments.First().Value;
                var mod = LoadedModManager.RunningMods.FirstOrDefault(m => m.PackageId.NoModIdSuffix() == modId);
                if (mod == null)
                    asm.MainModule.Types.Remove(t);
            }

            var stream = new MemoryStream();
            asm.Write(stream);

            var loadedAsm = AppDomain.CurrentDomain.Load(stream.ToArray());
            InitCompatInAsm(loadedAsm, "conditional compatibility");
        }

        static void InitCompatInAsm(Assembly asm, string what)
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
                    Log.Message($"MPCompat :: Initialized {what} for {action.mod.PackageId}");
                } catch(Exception e) {
                    Log.Error($"MPCompat :: Exception loading {action.mod.PackageId}: {e.InnerException}");
                }
            }
        }
    }
}