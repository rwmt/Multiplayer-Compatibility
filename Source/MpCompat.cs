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
        }

        static IEnumerable<ISyncMethod> RegisterLambdaMethod_Impl(Type parentType, string parentMethod, params int[] lambdaOrdinals)
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
            {
                var method = MpMethodUtil.GetLambda(parentType, parentMethod, methodType, null, ord);
                yield return MP.RegisterSyncDelegate(parentType, method.DeclaringType.Name, method.Name, fields);
            }
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