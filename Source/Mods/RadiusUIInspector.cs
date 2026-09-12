using System;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using UnityEngine;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Radius UI - Inspector by Astryl</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3786131805"/>
    [MpCompatFor("astryl.RadiusUI.Inspector")]
    internal class RadiusUIInspector
    {
        private static ISyncField syncMedCare;
        private static ISyncField syncSelfTend;
        private static ISyncField syncHostilityResponse;

        public RadiusUIInspector(ModContentPack mod) => LongEventHandler.ExecuteWhenFinished(LatePatch);

        private static void LatePatch()
        {
            var syncFieldsType = AccessTools.TypeByName("Multiplayer.Client.SyncFields");
            syncMedCare = (ISyncField)AccessTools.Field(syncFieldsType, "SyncMedCare")?.GetValue(null);
            syncSelfTend = (ISyncField)AccessTools.Field(syncFieldsType, "SyncSelfTend")?.GetValue(null);
            syncHostilityResponse = (ISyncField)AccessTools.Field(syncFieldsType, "SyncHostilityResponse")?.GetValue(null);

            // StoragePanel: Provide ThingFilterContext so Multiplayer synchronizes storage filter settings
            var storagePanelType = AccessTools.TypeByName("RadiusUIInspector.StoragePanel");
            MpCompat.harmony.Patch(
                AccessTools.Method(storagePanelType, "Draw", new[] { typeof(Rect), typeof(IStoreSettingsParent) }),
                prefix: new HarmonyMethod(typeof(RadiusUIInspector), nameof(PreDrawStoragePanel)),
                postfix: new HarmonyMethod(typeof(RadiusUIInspector), nameof(PostDrawStoragePanel)));

            // ControlsPanel: Watch medical care, self-tend, and hostility response
            var controlsPanelType = AccessTools.TypeByName("RadiusUIInspector.ControlsPanel");
            MpCompat.harmony.Patch(
                AccessTools.Method(controlsPanelType, "DrawMedical", new[] { typeof(Pawn), typeof(float), typeof(float), typeof(float) }),
                prefix: new HarmonyMethod(typeof(RadiusUIInspector), nameof(PreDrawMedical)),
                postfix: new HarmonyMethod(typeof(RadiusUIInspector), nameof(PostDrawMedical)));
        }

        private static void PreDrawStoragePanel(IStoreSettingsParent parent)
        {
            if (!MP.IsInMultiplayer || parent == null)
                return;

            MP.SetThingFilterContext(new StorageFilterWrapper(parent));
        }

        private static void PostDrawStoragePanel()
        {
            if (MP.IsInMultiplayer)
                MP.SetThingFilterContext(null);
        }

        private static void PreDrawMedical(Pawn pawn)
        {
            if (!MP.IsInMultiplayer || pawn == null)
                return;

            MP.WatchBegin();
            syncMedCare?.Watch(pawn);
            syncSelfTend?.Watch(pawn);
            syncHostilityResponse?.Watch(pawn);
        }

        private static void PostDrawMedical()
        {
            if (MP.IsInMultiplayer)
                MP.WatchEnd();
        }

        private record StorageFilterWrapper(IStoreSettingsParent Parent) : ThingFilterContext
        {
            public override ThingFilter Filter => Parent?.GetStoreSettings()?.filter;
            public override ThingFilter ParentFilter => Parent?.GetParentStoreSettings()?.filter;
        }
    }
}
