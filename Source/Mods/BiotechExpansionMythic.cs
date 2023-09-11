using System;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Biotech Expansion - Mythic by Lennoxicon</summary>
    /// <see href="https://github.com/Lennoxite/bte-mythic"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2883216840"/>
    [MpCompatFor("biotexpans.mythic")]
    public class BiotechExpansionMythic
    {
        private static Type geneAurumType;
        private static Type geneReverenceType;

        private static ISyncField syncAurumAllowedField;
        private static ISyncField syncReverenceAllowedField;

        public BiotechExpansionMythic(ModContentPack mod)
        {
            var type = AccessTools.TypeByName("BTE_MY.CompCreateReveredMote");
            MP.RegisterSyncMethod(type, "GetMeditationSpots"); // Scan for meditation spots
            MP.RegisterSyncMethod(type, "AddProgress").SetDebugOnly(); // (Dev) add 100% progress (also called during ticking)

            // -/+ 10% resource
            MP.RegisterSyncMethod(AccessTools.DeclaredMethod("BTE_MY.AurumUtility:OffsetResource")).SetDebugOnly();
            MP.RegisterSyncMethod(AccessTools.DeclaredMethod("BTE_MY.ReverenceUtility:OffsetResource")).SetDebugOnly();

            geneAurumType = type = AccessTools.TypeByName("BTE_MY.Gene_Aurum");
            syncAurumAllowedField = MP.RegisterSyncField(type, "aurumFuelAllowed");

            geneReverenceType = type = AccessTools.TypeByName("BTE_MY.Gene_Reverence");
            syncReverenceAllowedField = MP.RegisterSyncField(type, "ReverenceFuelAllowed");

            LongEventHandler.ExecuteWhenFinished(LatePatch);
        }

        private static void LatePatch()
        {
            foreach (var typeName in new[] { "BTE_MY.GeneGizmo_ResourceAurum", "BTE_MY.GeneGizmo_ResourceReverence" })
            {
                MpCompat.harmony.Patch(AccessTools.DeclaredMethod($"{typeName}:{nameof(GeneGizmo_Resource.DrawLabel)}"),
                    prefix: new HarmonyMethod(typeof(BiotechExpansionMythic), nameof(PreDrawLabel)));
            }
        }

        private static void PreDrawLabel(GeneGizmo_Resource __instance)
        {
            if (!MP.IsInMultiplayer)
                return;

            var type = __instance.gene.GetType();
            if (type == geneAurumType)
                syncAurumAllowedField.Watch(__instance.gene);
            else if (type == geneReverenceType)
                syncReverenceAllowedField.Watch(__instance.gene);
        }
    }
}