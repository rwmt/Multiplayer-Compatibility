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

        public HemogenExtractor(ModContentPack mod)
        {
            MpCompat.RegisterLambdaMethod("HemogenExtractor.CompSpawnerHemogen", "CompGetGizmosExtra", 0, 1).SetDebugOnly();

            var type = nutritionRefuelableType = AccessTools.TypeByName("HemogenExtractor.CompNutritionRefuelable");
            getStoreSettingsMethod = MethodInvoker.GetHandler(AccessTools.DeclaredMethod(type, "GetStoreSettings"));
            getParentStoreSettingsMethod = MethodInvoker.GetHandler(AccessTools.DeclaredMethod(type, "GetParentStoreSettings"));

            MpCompat.harmony.Patch(AccessTools.DeclaredMethod("HemogenExtractor.ITab_CustomNutrition:FillTab"),
                prefix: new HarmonyMethod(typeof(HemogenExtractor), nameof(PreFillTab)),
                finalizer: new HarmonyMethod(typeof(HemogenExtractor), nameof(PostFillTab)));
        }

        private static void PreFillTab()
        {
            if (!MP.IsInMultiplayer || Find.Selector.SingleSelectedThing is not ThingWithComps thing)
                return;

            if (thing.comps.FirstOrDefault(c => c.GetType() == nutritionRefuelableType) is CompRefuelable comp)
                MP.SetThingFilterContext(new HemogenExtractorContext(comp));
        }

        private static void PostFillTab() => MP.SetThingFilterContext(null);

        [MpCompatRequireMod("Uveren.HemogenExtractor")]
        public record HemogenExtractorContext(CompRefuelable Obj) : ThingFilterContext
        {
            public CompRefuelable Obj { get; } = Obj;

            public override ThingFilter Filter => ((StorageSettings)getStoreSettingsMethod(Obj)).filter;
            public override ThingFilter ParentFilter => ((StorageSettings)getParentStoreSettingsMethod(Obj))?.filter;
        }
    }
}