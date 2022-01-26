using HarmonyLib;
using System;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Immortals by fridgeBaron</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=1984905966"/>
    /// contribution to Multiplayer Compatibility by Reshiram and Sokyran
    [MpCompatFor("fridgeBaron.Immortals")]
    class Immortals
    {
        private static Type immortalComponent;
        private static bool shouldCall = false;

        public Immortals(ModContentPack mod)
        {
            immortalComponent = AccessTools.TypeByName("Immortals.Immortal_Component");
            MpCompat.harmony.Patch(AccessTools.Method("Immortals.Immortal_Component:GameComponentUpdate"), prefix: new HarmonyMethod(typeof(Immortals), nameof(GameComponentUpdatePrefix)));
            MpCompat.harmony.Patch(AccessTools.Method("Verse.GameComponent:GameComponentTick"), prefix: new HarmonyMethod(typeof(Immortals), nameof(GameComponentTickPrefix)));
        }

        //Patch GameComponentTick to Call GameComponentUpdate instead, shouldCall true to make it actually run
        private static void GameComponentTickPrefix(GameComponent __instance)
        {
            //Check if it's our component - ImmortalComponent does not override this method
            if (__instance.GetType() == immortalComponent)
            {
                shouldCall = true;
                __instance.GameComponentUpdate();
                shouldCall = false;
            }
        }

        //Patch GameComponentUpdate to not be called by itself which desyncs, but only when we call it from GameComponentTick, who the hell uses that anyways besides debug???
        private static bool GameComponentUpdatePrefix()
        {
            return shouldCall;
        }
    }
}