using System;
using System.Linq;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Auto-Extract Genes by Nibato</summary>
    /// <see href="https://github.com/Nibato/AutoExtractGenes/"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2882834449"/>
    [MpCompatFor("Nibato.AutoExtractGenes")]
    public class AutoExtractGenes
    {
        private static Type autoExtractGenesCompType;
        private static ISyncField autoExtractGenesField;

        public AutoExtractGenes(ModContentPack mod)
        {
            autoExtractGenesCompType = AccessTools.TypeByName("AutoExtractGenes.AutoExtractGenesComp");
            autoExtractGenesField = MP.RegisterSyncField(autoExtractGenesCompType, "isEnabled");

            MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(HealthCardUtility), nameof(HealthCardUtility.DrawOverviewTab)),
                prefix: new HarmonyMethod(typeof(AutoExtractGenes), nameof(PreFillTab)),
                finalizer: new HarmonyMethod(typeof(AutoExtractGenes), nameof(PostFillTab)));
        }

        private static void PreFillTab(Pawn pawn)
        {
            if (!MP.IsInMultiplayer)
            {
                return;
            }
            MP.WatchBegin();
            var comp = pawn.comps.SingleOrDefault(comp => comp.GetType() == autoExtractGenesCompType);
            if (comp != null)
            {
                autoExtractGenesField.Watch(comp);
            }
        }

        private static void PostFillTab()
        {
            if (!MP.IsInMultiplayer)
            {
                return;
            }
            MP.WatchEnd();
        }
    }
}