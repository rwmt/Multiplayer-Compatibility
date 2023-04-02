using System;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Hemogen Extractor by Уверен?</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2903919607"/>
    [MpCompatFor("Uveren.HemogenExtractor")]
    public class HemogenExtractor
    {
        private static FastInvokeHandler getStoreSettingsMethod;
        private static FastInvokeHandler getParentStoreSettingsMethod;

        private static Type nutritionRefuelableType;
        private static HemogenExtractorContext hemogenExtractorFilter;

        public HemogenExtractor(ModContentPack mod)
        {
            MpCompat.RegisterLambdaMethod("HemogenExtractor.CompSpawnerHemogen", "CompGetGizmosExtra", 0, 1).SetDebugOnly();

            var type = nutritionRefuelableType = AccessTools.TypeByName("HemogenExtractor.CompNutritionRefuelable");
            getStoreSettingsMethod = MethodInvoker.GetHandler(AccessTools.DeclaredMethod(type, "GetStoreSettings"));
            getParentStoreSettingsMethod = MethodInvoker.GetHandler(AccessTools.DeclaredMethod(type, "GetParentStoreSettings"));
            MP.ThingFilters.RegisterThingFilterTarget(type);
            MP.ThingFilters.RegisterThingFilterListener(GetHemogenExtractorFilter);

            MpCompat.harmony.Patch(AccessTools.DeclaredMethod("HemogenExtractor.ITab_CustomNutrition:FillTab"),
                prefix: new HarmonyMethod(typeof(HemogenExtractor), nameof(PreFillTab)),
                postfix: new HarmonyMethod(typeof(HemogenExtractor), nameof(PostFillTab)));
        }

        private static void PreFillTab()
        {
            if (!MP.IsInMultiplayer || Find.Selector.SingleSelectedThing is not ThingWithComps thing)
                return;

            if (thing.comps.FirstOrDefault(c => c.GetType() == nutritionRefuelableType) is CompRefuelable comp)
                hemogenExtractorFilter = new HemogenExtractorContext(comp);
        }

        private static void PostFillTab() => hemogenExtractorFilter = null;

        private static ThingFilterContext GetHemogenExtractorFilter() => hemogenExtractorFilter;

        public record HemogenExtractorContext(CompRefuelable Obj) : ThingFilterContext
        {
            public CompRefuelable Obj { get; } = Obj;

            public override ThingFilter Filter => ((StorageSettings)getStoreSettingsMethod(Obj)).filter;
            public override ThingFilter ParentFilter => ((StorageSettings)getParentStoreSettingsMethod(Obj))?.filter;
        }
    }
}