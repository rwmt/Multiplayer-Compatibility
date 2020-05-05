using System;
using System.Linq;

using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    public class MpCompat : Mod
    {
        internal static readonly Harmony harmony = new Harmony("rimworld.multiplayer.compat");

        public MpCompat(ModContentPack content) : base(content)
        {
            if (!MP.enabled) return;

            var queue = content.assemblies.loadedAssemblies
                .SelectMany(a => a.GetTypes())
                .Join(LoadedModManager.RunningMods,
                    type => type.TryGetAttribute<MpCompatForAttribute>()?.PackageId.ToLower(),
                    mod => mod.PackageId.Replace("_steam", ""),
                    (type, mod) => new { type, mod });

            foreach(var action in queue) {
                try {
                    Activator.CreateInstance(action.type, action.mod);

                    Log.Message($"MPCompat :: Initialized compatibility for {action.mod.PackageId}");
                } catch(Exception e) {
                    Log.Error($"MPCompat :: Exception loading {action.mod.PackageId}: {e.InnerException}");
                }
            }

            harmony.PatchAll();
        }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class MpCompatForAttribute : Attribute
    {
        public string PackageId { get; }

        public MpCompatForAttribute(string packageId)
        {
            this.PackageId = packageId;
        }
    }
}