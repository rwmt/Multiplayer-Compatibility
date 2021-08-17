using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
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
                    mod => mod.PackageId.Replace("_steam", "").Replace("_copy", ""),
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

        const string DisplayClassPrefix = "<>c__DisplayClass";
        const string SharedDisplayClass = "<>c";
        const string LambdaMethodInfix = "b__";
        const string LocalFunctionInfix = "g__";
        const string EnumerableStateMachineInfix = "d__";

        // Adapted from MpUtil.GetLambda
        static IEnumerable<MethodInfo> GetLambda(Type parentType, string parentMethod = null, MethodType parentMethodType = MethodType.Normal, Type[] parentArgs = null, params int[] lambdaOrdinals)
        {
            var parent = GetMethod(parentType, parentMethod, parentMethodType, parentArgs);
            if (parent == null)
                throw new Exception($"Couldn't find parent method ({parentMethodType}) {parentType}::{parentMethod}");

            var parentId = GetMethodDebugId(parent);

            // Example: <>c__DisplayClass10_
            var displayClassPrefix = $"{DisplayClassPrefix}{parentId}_";

            foreach (int lambdaOrdinal in lambdaOrdinals) 
            {
                // Example: <FillTab>b__0
                var lambdaNameShort = $"<{parent.Name}>{LambdaMethodInfix}{lambdaOrdinal}";

                // Capturing lambda
                var lambda = parentType.GetNestedTypes(AccessTools.all).
                    Where(t => t.Name.StartsWith(displayClassPrefix)).
                    SelectMany(t => AccessTools.GetDeclaredMethods(t)).
                    FirstOrDefault(m => m.Name == lambdaNameShort);

                // Example: <FillTab>b__10_0
                var lambdaNameFull = $"<{parent.Name}>{LambdaMethodInfix}{parentId}_{lambdaOrdinal}";

                // Non-capturing lambda
                lambda ??= AccessTools.Method(parentType, lambdaNameFull);

                // Non-capturing cached lambda
                if (lambda == null && AccessTools.Inner(parentType, SharedDisplayClass) is Type sharedDisplayClass)
                    lambda = AccessTools.Method(sharedDisplayClass, lambdaNameFull);

                if (lambda == null)
                    throw new Exception($"Couldn't find lambda {lambdaOrdinal} in parent method {parentType}::{parent.Name} (parent method id: {parentId})");

                yield return lambda;
            }
        }

        // Copied from Harmony.PatchProcessor
        public static MethodBase GetMethod(Type type, string methodName, MethodType methodType, Type[] args)
        {
            if (type == null) return null;

            switch (methodType)
            {
                case MethodType.Normal:
                    if (methodName == null)
                        return null;
                    return AccessTools.DeclaredMethod(type, methodName, args);

                case MethodType.Getter:
                    if (methodName == null)
                        return null;
                    return AccessTools.DeclaredProperty(type, methodName).GetGetMethod(true);

                case MethodType.Setter:
                    if (methodName == null)
                        return null;
                    return AccessTools.DeclaredProperty(type, methodName).GetSetMethod(true);

                case MethodType.Constructor:
                    return AccessTools.DeclaredConstructor(type, args);

                case MethodType.StaticConstructor:
                    return AccessTools.GetDeclaredConstructors(type)
                        .Where(c => c.IsStatic)
                        .FirstOrDefault();
            }

            return null;
        }

        // Copied from MpUtil.GetMethodDebugId
        public static int GetMethodDebugId(MethodBase method)
        {
            string cur = null;

            try
            {
                // Try extract the debug id from the method body
                foreach (var inst in PatchProcessor.GetOriginalInstructions(method))
                {
                    // Example class names: <>c__DisplayClass10_0 or <CompGetGizmosExtra>d__7
                    if (inst.opcode == OpCodes.Newobj
                        && inst.operand is MethodBase m
                        && (cur = m.DeclaringType.Name) != null)
                    {
                        if (cur.StartsWith(DisplayClassPrefix))
                            return int.Parse(cur.Substring(DisplayClassPrefix.Length).Until('_'));
                        else if (cur.Contains(EnumerableStateMachineInfix))
                            return int.Parse(cur.After('>').Substring(EnumerableStateMachineInfix.Length));
                    }
                    // Example method names: <FillTab>b__10_0 or <DoWindowContents>g__Start|55_1
                    else if (
                        (inst.opcode == OpCodes.Ldftn || inst.opcode == OpCodes.Call)
                        && inst.operand is MethodBase f
                        && (cur = f.Name) != null
                        && cur.StartsWith("<")
                        && cur.After('>').CharacterCount('_') == 3)
                    {
                        if (cur.Contains(LambdaMethodInfix))
                            return int.Parse(cur.After('>').Substring(LambdaMethodInfix.Length).Until('_'));
                        else if (cur.Contains(LocalFunctionInfix))
                            return int.Parse(cur.After('|').Until('_'));
                    }
                }
            }
            catch (Exception e)
            {
                throw new Exception($"Extracting debug id for {method.DeclaringType}::{method.Name} failed at {cur} with: {e.Message}");
            }

            throw new Exception($"Couldn't determine debug id for parent method {method.DeclaringType}::{method.Name}");
        }

        static IEnumerable<ISyncMethod> RegisterLambdaMethod_Impl(Type parentType, string parentMethod, params int[] lambdaOrdinals)
        {
            foreach(var method in GetLambda(parentType, parentMethod, MethodType.Normal, null, lambdaOrdinals))
            {
                yield return MP.RegisterSyncMethod(method);
            }
        }

        public static ISyncMethod[] RegisterLambdaMethod(Type parentType, string parentMethod, params int[] lambdaOrdinals)
        {
            return RegisterLambdaMethod_Impl(parentType, parentMethod, lambdaOrdinals).ToArray();
        }

        public static ISyncMethod[] RegisterLambdaMethod(string parentType, string parentMethod, params int[] lambdaOrdinals)
        {
            return RegisterLambdaMethod_Impl(AccessTools.TypeByName(parentType), parentMethod, lambdaOrdinals).ToArray();
        }

        static IEnumerable<ISyncDelegate> RegisterLambdaDelegate_Impl(Type parentType, string parentMethod, params int[] lambdaOrdinals)
        {
            foreach(var method in GetLambda(parentType, parentMethod, MethodType.Normal, null, lambdaOrdinals))
            {
                yield return MP.RegisterSyncDelegate(parentType, method.DeclaringType.Name, method.Name);
            }
        }

        public static ISyncDelegate[] RegisterLambdaDelegate(Type parentType, string parentMethod, params int[] lambdaOrdinals)
        {
            return RegisterLambdaDelegate_Impl(parentType, parentMethod, lambdaOrdinals).ToArray();
        }

        public static ISyncDelegate[] RegisterLambdaDelegate(string parentType, string parentMethod, params int[] lambdaOrdinals)
        {
            return RegisterLambdaDelegate_Impl(AccessTools.TypeByName(parentType), parentMethod, lambdaOrdinals).ToArray();
        }

        [Obsolete]
        public static IEnumerable<MethodInfo> MethodsByIndex(Type type, string prefix, params int[] index)
        {
            return type.GetMethods(AccessTools.allDeclared)
                .Where(delegate (MethodInfo m) {
                    return m.Name.StartsWith(prefix, StringComparison.Ordinal);
                })
                .Where((m, i) => index.Contains(i));
        }

        [Obsolete]
        public static IEnumerable<ISyncMethod> RegisterSyncMethodsByIndex(Type type, string prefix, params int[] index) {
            if (index.Length == 1) {
                return new[] {
                    RegisterSyncMethodByIndex(type, prefix, index[0])
                };
            }

            var methods = MethodsByIndex(type, prefix, index).ToList();
            var handles = new List<ISyncMethod>(methods.Count);

            foreach(var method in methods) {
                handles.Add(MP.RegisterSyncMethod(method));
            }

            return handles;
        }

        [Obsolete]
        public static MethodInfo MethodByIndex(Type type, string prefix, int index) {
            return MethodsByIndex(type, prefix, index).First();
        }

        [Obsolete]
        public static ISyncMethod RegisterSyncMethodByIndex(Type type, string prefix, int index) {
            return MP.RegisterSyncMethod(MethodByIndex(type, prefix, index));
        }

        /// <summary>Get the first method in the given type that matches the specified signature, return null if failed.</summary>
        /// <param name="type">The type of the target method</param>
        /// <param name="paramsType">The list of types of the target method's parameter</param>
        public static MethodInfo GetFirstMethodBySignature(Type type, Type[] paramsType)
        {
            foreach (MethodInfo mi in AccessTools.GetDeclaredMethods(type))
            {
                List<Type> foundParamsType = new List<Type>();
                if (mi.GetParameters().Length != 0)
                {
                    foreach (ParameterInfo pi in mi.GetParameters())
                    {
                        foundParamsType.Add(pi.ParameterType);
                    }
                }
                if (paramsType.All(foundParamsType.Contains) && paramsType.Count() == foundParamsType.Count) { return mi; }
            }
            return null;
        }

        /// <summary>Get the first method in the given type that matches the specified signature, return null if failed.</summary>
        /// <param name="type">The type of the target method</param>
        /// <param name="paramsType">The list of types of the target method's parameter</param>
        /// <param name="returnType">The return type of the target method</param>
        public static MethodInfo GetFirstMethodBySignature(Type type, Type[] paramsType, Type returnType)
        {
            foreach (MethodInfo mi in AccessTools.GetDeclaredMethods(type))
            {
                List<Type> foundParamsType = new List<Type>();
                if (mi.GetParameters().Length != 0)
                {
                    foreach (ParameterInfo pi in mi.GetParameters())
                    {
                        foundParamsType.Add(pi.ParameterType);
                    }
                }
                if (paramsType.All(foundParamsType.Contains) && paramsType.Count() == foundParamsType.Count && returnType == mi.ReturnType) { return mi; }
            }
            return null;
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