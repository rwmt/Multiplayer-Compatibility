using System;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Vanilla Traits Expanded by Oskar Potocki, ISOREX, Sarg Bjornson, erdelf, Kikohi, Taranchuk, Kentington, Chowder</summary>
    /// <see href="https://github.com/AndroidQuazar/VanillaFactionsExpanded-Mechanoid"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2329011599"/>
    [MpCompatFor("OskarPotocki.VFE.Mechanoid")]
    public class VanillaFactionsMechanoid
    {
        public VanillaFactionsMechanoid(ModContentPack mod) => LongEventHandler.ExecuteWhenFinished(LatePatch);

        private static void LatePatch()
        {
            var type = AccessTools.TypeByName("VFEMech.MissileSilo");
            MP.RegisterSyncMethod(type, "StartFire");
            var configureNewTargetMethod = AccessTools.Method(type, "ConfigureNewTarget");
            MP.RegisterSyncMethod(configureNewTargetMethod);
            foreach (var method in MpCompat.RegisterLambdaMethod(type, "GetGizmos", 2, 3)) method.SetDebugOnly();

            MpCompat.harmony.Patch(configureNewTargetMethod,
                postfix: new HarmonyMethod(typeof(VanillaFactionsMechanoid), nameof(CloseWorldTargetter)));

            type = AccessTools.TypeByName("VFE.Mechanoids.Buildings.Building_AutoPlant");
            MpCompat.RegisterLambdaMethod(type, "GetGizmos", 0, 2, 3);

            MpCompat.harmony.Patch(AccessTools.Method("VFE.Mechanoids.PlaceWorkers.PlaceWorker_AutoPlant:DrawGhost"),
                prefix: new HarmonyMethod(typeof(VanillaFactionsMechanoid), nameof(PreDrawGhost)));
        }

        private static void CloseWorldTargetter(bool __result)
        {
            // Force close the targetter to give the players a visual cue that it was successful
            // Otherwise, it would successfully mark the target but not give any indication
            if (MP.IsInMultiplayer && __result && Find.WorldTargeter.IsTargeting && Find.WorldTargeter.closeWorldTabWhenFinished)
                Find.WorldTargeter.StopTargeting();
        }

        private static void PreDrawGhost(ref Thing thing)
        {
            // Fix the bug (which causes error spam) in the original mod if the building is a blueprint or a building frame
            // This error spam seems capable of causing desyncs
            switch (thing)
            {
                case Blueprint_Build or Frame when thing.def.entityDefToBuild is ThingDef def:
                    thing = (Thing)Activator.CreateInstance(def.thingClass);
                    thing.def = def;
                    break;
                case Blueprint_Build or Frame:
                    thing = null;
                    break;
                // Handle the case of (re)installing in case another mods lets this be reinstalled
                case Blueprint_Install install:
                    thing = install.ThingToInstall;
                    break;
            }
        }
    }
}
