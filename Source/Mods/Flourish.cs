using System;
using HarmonyLib;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Flourish by Astryl</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3793154978"/>
    [MpCompatFor("astryl.flourish")]
    internal class Flourish
    {
        public Flourish(ModContentPack mod) => LongEventHandler.ExecuteWhenFinished(LatePatch);

        private static void LatePatch()
        {
            var tryIdleFlavour = AccessTools.DeclaredMethod("Flourish.FlourishGame:TryIdleFlavour");
            if (tryIdleFlavour != null)
            {
                MpCompat.harmony.Patch(
                    tryIdleFlavour,
                    prefix: new HarmonyMethod(typeof(Flourish), nameof(TryIdleFlavour_Prefix)),
                    finalizer: new HarmonyMethod(typeof(Flourish), nameof(TryIdleFlavour_Finalizer))
                );
            }
            else
            {
                Log.Warning("[Multiplayer Compat] Flourish: Could not find FlourishGame.TryIdleFlavour method.");
            }
        }

        // Isolate Rand state during camera-dependent idle flair checks so the shared simulation seed is never desynchronized
        private static void TryIdleFlavour_Prefix()
        {
            Rand.PushState();
        }

        private static void TryIdleFlavour_Finalizer()
        {
            Rand.PopState();
        }
    }
}
