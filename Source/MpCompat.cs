using System;
using System.Linq;

using Harmony;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    public class MpCompat : Mod
    {
        internal static readonly HarmonyInstance harmony = HarmonyInstance.Create("rimworld.multiplayer.compat");

        public MpCompat(ModContentPack content) : base(content)
        {
            if (!MP.enabled) return;

            var queue = content.assemblies.loadedAssemblies
                .SelectMany(a => a.GetTypes())
                .Join(LoadedModManager.RunningMods,
                    type => type.TryGetAttribute<MpCompatForAttribute>()?.ModName,
                    mod => mod.Name,
                    (type, mod) => new { type, mod });

            foreach(var action in queue) {
                try {
                    Activator.CreateInstance(action.type, action.mod);

                    Log.Message($"MPCompat :: Initialized compatibility for {action.mod.Name}");
                } catch(Exception e) {
                    Log.Error($"MPCompat :: Exception loading {action.mod.Name}: {e.InnerException}");
                }
            }

            harmony.PatchAll();
        }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class MpCompatForAttribute : Attribute
    {
        public string ModName { get; }

        public MpCompatForAttribute(string modName)
        {
            this.ModName = modName;
        }
    }
}