using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

        public static IEnumerable<MethodInfo> MethodsByIndex(Type type, string prefix, params int[] index)
        {
            return type.GetMethods(AccessTools.allDeclared)
                .Where(delegate (MethodInfo m) {
                    return m.Name.StartsWith(prefix, StringComparison.Ordinal);
                })
                .Where((m, i) => index.Contains(i));
        }

        public static IEnumerable<ISyncMethod> RegisterSyncMethodsByIndex(Type type, string prefix, params int[] index) {
            foreach(var method in MethodsByIndex(type, prefix, index)) {
                yield return MP.RegisterSyncMethod(method);
            }
        }

        public static MethodInfo MethodByIndex(Type type, string prefix, int index) {
            return MethodsByIndex(type, prefix, index).First();
        }

        public static ISyncMethod RegisterSyncMethodByIndex(Type type, string prefix, int index) {
            return MP.RegisterSyncMethod(MethodByIndex(type, prefix, index));
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