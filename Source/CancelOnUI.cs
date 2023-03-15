using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using Verse;

namespace Multiplayer.Compat
{
    public static class CancelOnUI
    {
        public static bool IsDoingUI { get; private set; } = false;

        private static bool isPatchApplied = false;

        public static void PatchMethod(params string[] methodName) => PatchMethod(methodName as IEnumerable<string>);

        public static void PatchMethod(IEnumerable<string> methodName)
            => PatchMethod(methodName
                .Select(m =>
                {
                    var method = AccessTools.DeclaredMethod(m) ?? AccessTools.Method(m);
                    if (method == null)
                        Log.Error($"({nameof(CancelOnUI)}) Could not find method {m}");
                    return method;
                })
                .Where(m => m != null));

        public static void PatchMethod(params MethodBase[] method) => PatchMethod(method as IEnumerable<MethodBase>);

        public static void PatchMethod(IEnumerable<MethodBase> methods)
        {
            Init();
            foreach (var method in methods)
                Patch(method);
        }

        private static void Patch(MethodBase method)
        {
            MpCompat.harmony.Patch(method,
                prefix: new HarmonyMethod(typeof(CancelOnUI), nameof(CancelDuringAlerts)));
        }

        public static void Init()
        {
            if (isPatchApplied) return;

            MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(UIRoot_Play), nameof(UIRoot_Play.UIRootOnGUI)),
                prefix: new HarmonyMethod(typeof(CancelOnUI), nameof(Prefix)),
                finalizer: new HarmonyMethod(typeof(CancelOnUI), nameof(Finalizer)));
            MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(UIRoot_Play), nameof(UIRoot_Play.UIRootUpdate)),
                prefix: new HarmonyMethod(typeof(CancelOnUI), nameof(Prefix)),
                finalizer: new HarmonyMethod(typeof(CancelOnUI), nameof(Finalizer)));

            isPatchApplied = true;
        }

        private static void Prefix() => IsDoingUI = true;

        private static void Finalizer() => IsDoingUI = false;

        private static bool CancelDuringAlerts() => MP.IsInMultiplayer && !IsDoingUI;
    }
}