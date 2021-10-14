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

            MpCompatLoader.Load(content);
            harmony.PatchAll();
        }

        static IEnumerable<ISyncMethod> RegisterLambdaMethod_Impl(Type parentType, string parentMethod, params int[] lambdaOrdinals)
        {
            foreach (int ord in lambdaOrdinals)
            {
                var method = MpMethodUtil.GetLambda(parentType, parentMethod, MethodType.Normal, null, ord);
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
            foreach (int ord in lambdaOrdinals)
            {
                var method = MpMethodUtil.GetLambda(parentType, parentMethod, MethodType.Normal, null, ord);
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
}