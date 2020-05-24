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
                .Where(t => t.HasAttribute<MpCompatForAttribute>())
                .SelectMany(t => (MpCompatForAttribute[]) t.GetCustomAttributes(typeof(MpCompatForAttribute), false),
                    resultSelector: (type, compat) => new { type, compat })
                .Join(LoadedModManager.RunningMods,
                    box => box.compat.PackageId.ToLower(),
                    mod => mod.PackageId.Replace("_steam", ""),
                    (box, mod) => new { box.type, mod });

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

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class MpCompatForAttribute : Attribute
    {
        public string PackageId { get; }

        public MpCompatForAttribute(string packageId)
        {
            this.PackageId = packageId;
        }

        public override object TypeId {
            get {
                return this;
            }
        }
    }
}