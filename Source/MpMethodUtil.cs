using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;

namespace Multiplayer.Compat
{
    public static class MpMethodUtil
    {
        #region Lambdas

        // From Multiplayer.Client.Util, modified to handle generics.
        const string DisplayClassPrefix = "<>c__DisplayClass";
        const string SharedDisplayClass = "<>c";
        const string LambdaMethodInfix = "b__";
        const string LocalFunctionInfix = "g__";
        const string EnumerableStateMachineInfix = "d__";

        public static IEnumerable<MethodInfo> GetLambda(Type parentType, string parentMethod = null, MethodType parentMethodType = MethodType.Normal, Type[] parentArgs = null, params int[] lambdaOrdinals)
            => GetLambdaGeneric(parentType, parentMethod, parentMethodType, parentArgs, null, null, lambdaOrdinals);

        public static IEnumerable<MethodInfo> GetLambdaGeneric(Type parentType, string parentMethod = null, MethodType parentMethodType = MethodType.Normal, Type[] parentArgs = null, Type[] genericParentMethodArgs = null, Type[] genericNestedTypeArgs = null, params int[] lambdaOrdinals)
        {
            foreach (int ord in lambdaOrdinals)
            {
                yield return MpMethodUtil.GetLambdaGeneric(parentType, parentMethod, parentMethodType, parentArgs, genericParentMethodArgs, genericNestedTypeArgs, ord);
            }
        }

        public static MethodInfo GetLambda(Type parentType, string parentMethod = null, MethodType parentMethodType = MethodType.Normal, Type[] parentArgs = null, int lambdaOrdinal = 0)
            => GetLambdaGeneric(parentType, parentMethod, parentMethodType, parentArgs, null, null, lambdaOrdinal);

        public static MethodInfo GetLambdaGeneric(Type parentType, string parentMethod = null, MethodType parentMethodType = MethodType.Normal, Type[] parentArgs = null, Type[] genericParentMethodArgs = null, Type[] genericNestedTypeArgs = null, int lambdaOrdinal = 0)
        {
            var parent = GetMethod(parentType, parentMethod, parentMethodType, parentArgs);
            if (parent == null)
                throw new Exception($"Couldn't find parent method ({parentMethodType}) {parentType}::{parentMethod}");
            if (genericParentMethodArgs != null && parent is MethodInfo m)
                parent = m.MakeGenericMethod(genericParentMethodArgs);

            var parentId = GetMethodDebugId(parent);

            // Example: <>c__DisplayClass10_
            var displayClassPrefix = $"{DisplayClassPrefix}{parentId}_";

            // Example: <FillTab>b__0
            var lambdaNameShort = $"<{parent.Name}>{LambdaMethodInfix}{lambdaOrdinal}";

            // Capturing lambda
            var lambda = parentType.GetNestedTypes(AccessTools.all).
                Where(t =>
                {
                    if (genericNestedTypeArgs is { Length: > 0 })
                    {
                        if (!t.IsGenericType)
                            return false;
                        if (t.GetGenericArguments().Length != genericNestedTypeArgs.Length)
                            return false;
                    }

                    return t.Name.StartsWith(displayClassPrefix);
                }).
                Select(t => genericNestedTypeArgs == null ? t : t.MakeGenericType(genericNestedTypeArgs)).
                SelectMany(AccessTools.GetDeclaredMethods).
                FirstOrDefault(m => m.Name == lambdaNameShort);

            // Example: <FillTab>b__10_0
            var lambdaNameFull = $"<{parent.Name}>{LambdaMethodInfix}{parentId}_{lambdaOrdinal}";

            // Non-capturing lambda
            lambda ??= AccessTools.Method(parentType, lambdaNameFull);

            // Non-capturing cached lambda
            if (lambda == null && AccessTools.Inner(parentType, SharedDisplayClass) is { } sharedDisplayClass)
                lambda = AccessTools.Method(sharedDisplayClass, lambdaNameFull);

            if (lambda == null)
                throw new Exception($"Couldn't find lambda {lambdaOrdinal} in parent method {parentType}::{parent.Name} (parent method id: {parentId})");

            return lambda;
        }

        public static MethodInfo GetLocalFunc(Type parentType, string parentMethod = null, MethodType parentMethodType = MethodType.Normal, Type[] parentArgs = null, string localFunc = null)
            => GetLocalFuncGeneric(parentType, parentMethod, parentMethodType, parentArgs, null, null, localFunc);

        public static MethodInfo GetLocalFuncGeneric(Type parentType, string parentMethod = null, MethodType parentMethodType = MethodType.Normal, Type[] parentArgs = null, Type[] genericParentMethodArgs = null, Type[] genericNestedTypeArgs = null, string localFunc = null)
        {
            var parent = GetMethod(parentType, parentMethod, parentMethodType, parentArgs);
            if (parent == null)
                throw new Exception($"Couldn't find parent method ({parentMethodType}) {parentType}::{parentMethod}");
            if (genericParentMethodArgs != null && parent is MethodInfo m)
                parent = m.MakeGenericMethod(genericParentMethodArgs);

            var parentId = GetMethodDebugId(parent);

            // Example: <>c__DisplayClass10_
            var displayClassPrefix = $"{DisplayClassPrefix}{parentId}_";

            // Example: <DoWindowContents>g__Start|
            var localFuncPrefix = $"<{parentMethod}>{LocalFunctionInfix}{localFunc}|";

            // Example: <DoWindowContents>g__Start|10
            var localFuncPrefixWithId = $"<{parentMethod}>{LocalFunctionInfix}{localFunc}|{parentId}";

            var candidates = parentType.GetNestedTypes(AccessTools.all).
                Where(t =>
                {
                    if (genericNestedTypeArgs is { Length: > 0 })
                    {
                        if (!t.IsGenericType)
                            return false;
                        if (t.GetGenericArguments().Length != genericNestedTypeArgs.Length)
                            return false;
                    }

                    return t.Name.StartsWith(displayClassPrefix);
                }).
                Select(t => genericNestedTypeArgs == null ? t : t.MakeGenericType(genericNestedTypeArgs)).
                SelectMany(AccessTools.GetDeclaredMethods).
                Where(m => m.Name.StartsWith(localFuncPrefix)).
                Concat(AccessTools.GetDeclaredMethods(parentType).Where(m => m.Name.StartsWith(localFuncPrefixWithId))).
                ToArray();

            if (candidates.Length == 0)
                throw new Exception($"Couldn't find local function {localFunc} in parent method {parentType}::{parent.Name} (parent method id: {parentId})");

            if (candidates.Length > 1)
                throw new Exception($"Ambiguous local function {localFunc} in parent method {parentType}::{parent.Name} (parent method id: {parentId})");

            return candidates[0];
        }

        // Based on https://github.com/dotnet/roslyn/blob/main/src/Compilers/CSharp/Portable/Symbols/Synthesized/GeneratedNameKind.cs
        // and https://github.com/dotnet/roslyn/blob/main/src/Compilers/CSharp/Portable/Symbols/Synthesized/GeneratedNames.cs
        public static int GetMethodDebugId(MethodBase method)
        {
            string cur = null;

            try
            {
                // Try extract the debug id from the method body
                foreach (var inst in PatchProcessor.GetOriginalInstructions(method))
                {
                    // Example class names: <>c__DisplayClass10_0 or <CompGetGizmosExtra>d__7 or <>c__DisplayClass0_0`1
                    if (inst.opcode == OpCodes.Newobj
                        && inst.operand is MethodBase m
                        && (cur = m.DeclaringType.Name) != null)
                    {
                        // Strip generic data
                        if (cur.Contains('`'))
                            cur = cur.Until('`');

                        if (cur.StartsWith(DisplayClassPrefix))
                            return int.Parse(cur.Substring(DisplayClassPrefix.Length).Until('_'));
                        else if (cur.Contains(EnumerableStateMachineInfix))
                            return int.Parse(cur.After('>').Substring(EnumerableStateMachineInfix.Length));
                    }
                    // Example method names: <FillTab>b__10_0 or <DoWindowContents>g__Start|55_1 or <GetFloatMenuOptions>b__0`1
                    else if (
                        (inst.opcode == OpCodes.Ldftn || inst.opcode == OpCodes.Call)
                        && inst.operand is MethodBase f
                        && (cur = f.Name) != null
                        && cur.StartsWith("<")
                        && cur.After('>').CharacterCount('_') == 3)
                    {
                        // Strip generic data
                        if (cur.Contains('`'))
                            cur = cur.Until('`');

                        if (cur.Contains(LambdaMethodInfix))
                            return int.Parse(cur.After('>').Substring(LambdaMethodInfix.Length).Until('_'));
                        else if (cur.Contains(LocalFunctionInfix))
                            return int.Parse(cur.After('|').Until('_'));
                    }
                }
            }
            catch (Exception e)
            {
                throw new Exception($"Extracting debug id for {method.DeclaringType}::{method.Name} failed at {cur} with: {e}");
            }

            throw new Exception($"Couldn't determine debug id for parent method {method.DeclaringType}::{method.Name}");
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
                        return AccessTools.DeclaredIndexer(type, args).GetGetMethod(true);
                    return AccessTools.DeclaredProperty(type, methodName).GetGetMethod(true);

                case MethodType.Setter:
                    if (methodName == null)
                        return AccessTools.DeclaredIndexer(type, args).GetSetMethod(true);
                    return AccessTools.DeclaredProperty(type, methodName).GetSetMethod(true);

                case MethodType.Constructor:
                    return AccessTools.DeclaredConstructor(type, args);

                case MethodType.StaticConstructor:
                    return AccessTools.GetDeclaredConstructors(type).FirstOrDefault(c => c.IsStatic);

                case MethodType.Enumerator:
                    if (methodName == null)
                        return null;
                    return AccessTools.EnumeratorMoveNext(AccessTools.DeclaredMethod(type, methodName, args));

                case MethodType.Async:
                    if (methodName == null)
                        return null;
                    return AccessTools.AsyncMoveNext(AccessTools.DeclaredMethod(type, methodName, args));

                default:
                    return null;
            }
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

        #endregion

        #region MethodOf

        /// <summary>
        /// <para>Taken from Fishery</para>
        /// <para>Original source: <see href="https://github.com/bbradson/Fishery/blob/af759661bdc404b85309ad5d8ca3d36607fc79d3/Source/FisheryLib/Aliases.cs#L18"/></para>
        /// <para>This method will return a <see cref="MethodInfo"/> of a delegate which was passed in as an argument.</para>
        /// <para>Only works on static methods. <see cref="SymbolExtensions"/> supports non-static method, but is less convenient and will return base method when trying to pass an overriden method.</para>
        /// </summary>
        /// <example>
        /// <code>
        /// var staticMethod = MethodOf(() => MethodOf(TestClass.TestStaticMethod);
        /// var staticMethodWithConflict = MethodOf(() => MethodOf(new Action&lt;int&gt;(TestClass.TestStaticMethodWithNameConflicts));
        /// </code>
        /// </example>
        /// <param name="method">The delegate from which to get the <see cref="MethodInfo"/> for.</param>
        /// <returns>The target method for the delegate.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MethodInfo MethodOf(Delegate method) => method.Method;

        #endregion
    }
}
