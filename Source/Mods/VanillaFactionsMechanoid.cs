using System;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Vanilla Factions Expanded - Mechanoids by Oskar Potocki, ISOREX, Sarg Bjornson, erdelf, Kikohi, Taranchuk, Kentington, Chowder</summary>
    /// <see href="https://github.com/Vanilla-Expanded/VanillaFactionsExpanded-Mechanoid"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2329011599"/>
    [MpCompatFor("OskarPotocki.VFE.Mechanoid")]
    public class VanillaFactionsMechanoid
    {
        public VanillaFactionsMechanoid(ModContentPack mod) => LongEventHandler.ExecuteWhenFinished(LatePatch);

        private static void LatePatch()
        {
            // Missile silo
            var type = AccessTools.TypeByName("VFEMech.MissileSilo");
            MP.RegisterSyncMethod(type, "StartFire");
            var configureNewTargetMethod = AccessTools.Method(type, "ConfigureNewTarget");
            MP.RegisterSyncMethod(configureNewTargetMethod);
            foreach (var method in MpCompat.RegisterLambdaMethod(type, "GetGizmos", 2, 3)) method.SetDebugOnly();

            MpCompat.harmony.Patch(configureNewTargetMethod,
                postfix: new HarmonyMethod(typeof(VanillaFactionsMechanoid), nameof(CloseWorldTargetter)));

            // Auto plant machines (planter/harvester)
            type = AccessTools.TypeByName("VFE.Mechanoids.Buildings.Building_AutoPlant");
            MpCompat.RegisterLambdaMethod(type, "GetGizmos", 0, 2, 3);

            // Indoctrination Pod
            type = AccessTools.TypeByName("VFEMech.Building_IndoctrinationPod");
            MP.RegisterSyncMethod(type, nameof(Building_Casket.EjectContents)); // Overrides the method from vanilla
            MpCompat.RegisterLambdaDelegate(type, "GetGizmos", 1, 3); // Set target ideo (both of them are identical)
            // TODO: Test float menus

            // Industrial apiary
            type = AccessTools.TypeByName("VFEMech.Building_IndustrialApiary");
            MpCompat.RegisterLambdaMethod(type, "GetGizmos", 0, 1).SetDebugOnly(); // Finish/add progress

            // Propaganda comp
            type = AccessTools.TypeByName("VFEMech.CompPropaganda");
            MpCompat.RegisterLambdaDelegate(type, "GetGizmos", 1, 3); // Set propaganda mode/set target ideo
        }

        private static void CloseWorldTargetter(bool __result)
        {
            // Force close the targetter to give the players a visual cue that it was successful
            // Otherwise, it would successfully mark the target but not give any indication
            if (MP.IsInMultiplayer && __result && Find.WorldTargeter.IsTargeting && Find.WorldTargeter.closeWorldTabWhenFinished)
                Find.WorldTargeter.StopTargeting();
        }
    }
}
