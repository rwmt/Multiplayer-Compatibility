using System;
using System.Linq;

using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    [StaticConstructorOnStartup]
    public class LatePatcher
    {
        internal static readonly Harmony harmony = new Harmony("rimworld.multiplayer.late_compat");

        static LatePatcher()
        {
            if (!MP.enabled) return;

            var queue = MpCompat.content.assemblies.loadedAssemblies
                .SelectMany(a => a.GetTypes())
                .Join(LoadedModManager.RunningMods,
                    type => type.TryGetAttribute<LateMpCompatForAttribute>()?.ModName,
                    mod => mod.Name,
                    (type, mod) => new { type, mod });

            foreach (var action in queue)
            {
                try
                {
                    action.type.GetMethod("LateCompat", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public).Invoke(null, new[] { action.mod });
                    //Activator.CreateInstance(action.type, action.mod);

                    Log.Message($"MPCompat :: Initialized late compatibility for {action.mod.Name}");
                }
                catch (Exception e)
                {
                    Log.Error($"MPCompat :: Exception late loading {action.mod.Name}: {e.InnerException}");
                }
            }

            MpCompat.harmony.PatchAll();
        }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class LateMpCompatForAttribute : Attribute
    {
        public string ModName { get; }

        public LateMpCompatForAttribute(string modName)
        {
            this.ModName = modName;
        }
    }
}
