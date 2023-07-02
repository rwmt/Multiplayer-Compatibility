using System;
using System.Collections;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Minify Everything by Erdelf</summary>
    /// <see href="https://github.com/erdelf/MinifyEverything"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=872762753"/>
    [MpCompatFor("erdelf.MinifyEverything")]
    public class MinifyEverything
    {
        public MinifyEverything(ModContentPack mod) => LongEventHandler.ExecuteWhenFinished(LatePatch);

        public static void LatePatch()
        {
            var type = AccessTools.TypeByName("MinifyEverything.MinifyEverything");

            MpCompat.harmony.Patch(AccessTools.Method(type, "DoStuff"),
                prefix: new HarmonyMethod(typeof(MinifyEverything), nameof(PreDoStuff)));
        }

        // Cancel the coroutine in MP and redirect the action to LongEventHandler
        private static bool PreDoStuff(Action action, ref IEnumerator __result)
        {
            if (!MP.IsInMultiplayer) return true;
            // Return a fake, empty coroutine as a result, so we don't get any errors
            __result = FakeCoroutine();
            LongEventHandler.ExecuteWhenFinished(action);
            return false;
        }

        private static IEnumerator FakeCoroutine()
        {
            yield return null;
        }
    }
}
