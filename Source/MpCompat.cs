using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    public class MpCompat : Mod
    {
        public static readonly Harmony harmony = new Harmony("rimworld.multiplayer.compat");

        public MpCompat(ModContentPack content) : base(content)
        {
            DebugActions.content = content;

            if (!MP.enabled) {
                Log.Warning("MPCompat :: Multiplayer is disabled.");
                return;
            }

            MpCompatLoader.Load(content);
            harmony.PatchAll();
            MultiplayerReconnectFix.Apply();
        }

        static IEnumerable<ISyncMethod> RegisterLambdaMethod_Impl(Type parentType, string parentMethod, MethodType methodType, params int[] lambdaOrdinals)
        {
            foreach (int ord in lambdaOrdinals)
            {
                var method = MpMethodUtil.GetLambda(parentType, parentMethod, methodType, null, ord);
                yield return MP.RegisterSyncMethod(method);
            }
        }

        public static ISyncMethod[] RegisterLambdaMethod(Type parentType, string parentMethod, MethodType methodType, params int[] lambdaOrdinals)
        {
            return RegisterLambdaMethod_Impl(parentType, parentMethod, methodType, lambdaOrdinals).ToArray();
        }

        public static ISyncMethod[] RegisterLambdaMethod(Type parentType, string parentMethod, params int[] lambdaOrdinals)
        {
            return RegisterLambdaMethod_Impl(parentType, parentMethod, MethodType.Normal, lambdaOrdinals).ToArray();
        }

        public static ISyncMethod[] RegisterLambdaMethod(string parentType, string parentMethod, MethodType methodType = MethodType.Normal, params int[] lambdaOrdinals)
        {
            return RegisterLambdaMethod_Impl(AccessTools.TypeByName(parentType), parentMethod, methodType, lambdaOrdinals).ToArray();
        }

        public static ISyncMethod[] RegisterLambdaMethod(string parentType, string parentMethod, params int[] lambdaOrdinals)
        {
            return RegisterLambdaMethod_Impl(AccessTools.TypeByName(parentType), parentMethod, MethodType.Normal, lambdaOrdinals).ToArray();
        }

        static IEnumerable<ISyncDelegate> RegisterLambdaDelegate_Impl(Type parentType, string parentMethod, MethodType methodType, string[] fields, params int[] lambdaOrdinals)
        {
            foreach (int ord in lambdaOrdinals)
                yield return RegisterLambdaDelegateInternal(parentType, parentMethod, methodType, fields, ord);
        }

        /// <summary>
        /// Registers a compiler-generated delegate. Uses nested-type lookup only for direct children of
        /// <paramref name="parentType"/>; otherwise registers by <see cref="MethodInfo"/> (iterator / state-machine lambdas).
        /// </summary>
        internal static ISyncDelegate RegisterLambdaDelegateInternal(Type parentType, string parentMethod, MethodType methodType, string[] fields, int ord, Type[] parentArgs = null)
        {
            var method = MpMethodUtil.GetLambda(parentType, parentMethod, methodType, parentArgs, ord);
            var declaringType = method.DeclaringType;
            if (declaringType is { IsNested: true } && declaringType.DeclaringType == parentType)
                return MP.RegisterSyncDelegate(parentType, declaringType.Name, method.Name, fields);

            return RegisterSyncDelegateDirect(method, fields);
        }

        static ISyncDelegate RegisterSyncDelegateDirect(MethodInfo method, string[] fields)
        {
            var syncType = AccessTools.TypeByName("Multiplayer.Client.Sync");
            var direct = syncType == null
                ? null
                : AccessTools.Method(syncType, "RegisterSyncDelegate", [typeof(MethodInfo), typeof(string[])]);
            if (direct != null)
                return (ISyncDelegate)direct.Invoke(null, [method, fields]);

            if (fields is { Length: > 0 })
                throw new Exception($"Cannot register {method.DeclaringType}::{method.Name} with closure fields");

            throw new Exception($"Multiplayer.Client.Sync.RegisterSyncDelegate(MethodInfo) not found for {method.DeclaringType}::{method.Name}");
        }

        public static ISyncDelegate[] RegisterLambdaDelegate(Type parentType, string parentMethod, params int[] lambdaOrdinals)
        {
            return RegisterLambdaDelegate_Impl(parentType, parentMethod, MethodType.Normal, null, lambdaOrdinals).ToArray();
        }

        public static ISyncDelegate[] RegisterLambdaDelegate(Type parentType, string parentMethod, string[] fields, params int[] lambdaOrdinals)
        {
            return RegisterLambdaDelegate_Impl(parentType, parentMethod, MethodType.Normal, fields, lambdaOrdinals).ToArray();
        }

        public static ISyncDelegate[] RegisterLambdaDelegate(Type parentType, string parentMethod, MethodType methodType, params int[] lambdaOrdinals)
        {
            return RegisterLambdaDelegate_Impl(parentType, parentMethod, methodType, null, lambdaOrdinals).ToArray();
        }

        public static ISyncDelegate[] RegisterLambdaDelegate(Type parentType, string parentMethod, MethodType methodType, string[] fields, params int[] lambdaOrdinals)
        {
            return RegisterLambdaDelegate_Impl(parentType, parentMethod, methodType, fields, lambdaOrdinals).ToArray();
        }

        public static ISyncDelegate[] RegisterLambdaDelegate(string parentType, string parentMethod, params int[] lambdaOrdinals)
        {
            return RegisterLambdaDelegate_Impl(AccessTools.TypeByName(parentType), parentMethod, MethodType.Normal, null, lambdaOrdinals).ToArray();
        }

        public static ISyncDelegate[] RegisterLambdaDelegate(string parentType, string parentMethod, string[] fields, params int[] lambdaOrdinals)
        {
            return RegisterLambdaDelegate_Impl(AccessTools.TypeByName(parentType), parentMethod, MethodType.Normal, fields, lambdaOrdinals).ToArray();
        }

        public static ISyncDelegate[] RegisterLambdaDelegate(string parentType, string parentMethod, MethodType methodType, params int[] lambdaOrdinals)
        {
            return RegisterLambdaDelegate_Impl(AccessTools.TypeByName(parentType), parentMethod, methodType, null, lambdaOrdinals).ToArray();
        }

        public static ISyncDelegate[] RegisterLambdaDelegate(string parentType, string parentMethod, MethodType methodType, string[] fields, params int[] lambdaOrdinals)
        {
            return RegisterLambdaDelegate_Impl(AccessTools.TypeByName(parentType), parentMethod, methodType, fields, lambdaOrdinals).ToArray();
        }
    }
}