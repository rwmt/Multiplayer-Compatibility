using HarmonyLib;
using Multiplayer.API;
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
            foreach (var method in MpCompat.RegisterSyncMethodsByIndex(type, "<GetGizmos>", 2, 3)) method.SetDebugOnly();

            MpCompat.harmony.Patch(configureNewTargetMethod,
                postfix: new HarmonyMethod(typeof(VanillaFactionsMechanoid), nameof(CloseWorldTargetter)));

            type = AccessTools.TypeByName("VFE.Mechanoids.Buildings.Building_AutoPlant");
            // Methods b__8_0, b__8_2, b__8_3. They are the first 3 methods in the file. The b__8_1 is the last one, and thus has index 3.
            MpCompat.RegisterSyncMethodsByIndex(type, "<GetGizmos>", 0, 1, 2);
        }

        private static void CloseWorldTargetter(bool __result)
        {
            // Force close the targetter to give the players a visual cue that it was successful
            // Otherwise, it would succesfully mark the target but not give any indication
            if (MP.IsInMultiplayer && __result && Find.WorldTargeter.IsTargeting && Find.WorldTargeter.closeWorldTabWhenFinished)
                Find.WorldTargeter.StopTargeting();
        }
    }
}
