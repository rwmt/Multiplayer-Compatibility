using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using System.Reflection;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>AutomaticForging by I</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3687750532"/>
    [MpCompatFor("i.automaticforaging")]
    public class AutomaticForaging
    {
        static ISyncField FieldRepeatCount;
        static ISyncField FieldTargetCount;
        static ISyncField FieldPauseWhenSatisfied;
        static ISyncField FieldUnpauseWhenYouHave;
        static ISyncField FieldGatherRadius;

        static FieldInfo FieldPlantFilter;
        static FieldInfo FieldFixedPlantFilter;

        public AutomaticForaging(ModContentPack mod)
        {
            LongEventHandler.ExecuteWhenFinished(LatePatch);
        }
        void LatePatch()
        {
            // Gizmos
            var type = AccessTools.TypeByName("AutomaticForaging.Building_ForageSpot");
            MpCompat.RegisterLambdaMethod(type, "GetGizmos", 1);

            // ITabs
            type = AccessTools.TypeByName("AutomaticForaging.ITab_ForageSpot");
            MP.RegisterSyncWorker<ITab>(SyncITab_ForgeSpot, type, shouldConstruct: true);

            MpCompat.harmony.Patch(AccessTools.Method(type, nameof(ITab.FillTab)),
                prefix: new HarmonyMethod(typeof(ITab_ForgeSpot_Patch), nameof(ITab_ForgeSpot_Patch.Prefix)),
                postfix: new HarmonyMethod(typeof(ITab_ForgeSpot_Patch), nameof(ITab_ForgeSpot_Patch.Postfix))
                );

            MpCompat.RegisterLambdaDelegate(type, "FillTab", 0, 1, 2, 3, 4, 5, 6).SetContext(SyncContext.MapSelected);
            MpCompat.RegisterLambdaDelegate(type, "FillIncludeGroupOptions", 6);
            MpCompat.RegisterLambdaDelegate(type, "FillSlotGroupStoreOptions", 1);



            type = AccessTools.TypeByName("AutomaticForaging.ITab_ForagePlants");

            MpCompat.harmony.Patch(AccessTools.Method(type, nameof(ITab.FillTab)),
                prefix: new HarmonyMethod(typeof(ITab_ForgePlants_Patch), nameof(ITab_ForgePlants_Patch.Prefix)),
                postfix: new HarmonyMethod(typeof(ITab_ForgePlants_Patch), nameof(ITab_ForgePlants_Patch.Postfix))
                );

            type = AccessTools.TypeByName("AutomaticForaging.Building_ForageSpot");
            FieldRepeatCount = MP.RegisterSyncField(type, "repeatCount");
            FieldTargetCount = MP.RegisterSyncField(type, "targetCount");
            FieldPauseWhenSatisfied = MP.RegisterSyncField(type, "pauseWhenSatisfied");
            FieldUnpauseWhenYouHave = MP.RegisterSyncField(type, "unpauseWhenYouHave");
            FieldGatherRadius = MP.RegisterSyncField(type, "gatherRadius");

            FieldPlantFilter = AccessTools.Field(AccessTools.TypeByName("AutomaticForaging.Building_ForageSpot"), "plantFilter");
            FieldFixedPlantFilter = AccessTools.Field(AccessTools.TypeByName("AutomaticForaging.Building_ForageSpot"), "fixedPlantFilter");


        }

        void SyncITab_ForgeSpot(SyncWorker sync, ref ITab tab) { }

        class ITab_ForgeSpot_Patch
        {
            public static void Prefix(ITab __instance)
            {
                MP.WatchBegin();
                Building spot = __instance.SelThing as Building;
                FieldRepeatCount.Watch(spot);
                FieldTargetCount.Watch(spot);
                FieldPauseWhenSatisfied.Watch(spot);
                FieldUnpauseWhenYouHave.Watch(spot);
                FieldGatherRadius.Watch(spot);
            }
            public static void Postfix()
            {
                MP.WatchEnd();
            }
        }
        class ITab_ForgePlants_Patch
        {
            public static void Prefix(ITab __instance)
            {
                Building spot = __instance.SelThing as Building;
                MP.SetThingFilterContext(new ForgePlantsWrapper(spot));
            }
            public static void Postfix()
            {
                MP.SetThingFilterContext(null);
            }
        }
        private record ForgePlantsWrapper(Building ForageSpot) : ThingFilterContext
        {
            public override ThingFilter Filter => FieldPlantFilter.GetValue(ForageSpot) as ThingFilter;
            public override ThingFilter ParentFilter => FieldFixedPlantFilter.GetValue(ForageSpot) as ThingFilter;

        }
    }
}