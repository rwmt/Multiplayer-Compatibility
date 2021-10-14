using System;
using System.Linq;
using System.Reflection;
using Verse;

namespace Multiplayer.Compat
{
    public static class MpCompatLoader
    {
        internal static void Load(ModContentPack content)
        {
            foreach (var asm in content.assemblies.loadedAssemblies)
                InitCompatInAsm(asm);
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
                    Log.Error($"MPCompat :: Exception loading {action.mod.PackageId}: {e.InnerException}");
                }
            }
        }
    }
}