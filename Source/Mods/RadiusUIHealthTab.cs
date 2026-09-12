using System;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using UnityEngine;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Radius UI - Health Tab by Astryl</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3786129210"/>
    [MpCompatFor("astryl.RadiusUI.HealthTab")]
    internal class RadiusUIHealthTab
    {
        private static ISyncField syncMedCare;
        private static ISyncField syncSelfTend;

        public RadiusUIHealthTab(ModContentPack mod) => LongEventHandler.ExecuteWhenFinished(LatePatch);

        private static void LatePatch()
        {
            var syncFieldsType = AccessTools.TypeByName("Multiplayer.Client.SyncFields");
            syncMedCare = (ISyncField)AccessTools.Field(syncFieldsType, "SyncMedCare")?.GetValue(null);
            syncSelfTend = (ISyncField)AccessTools.Field(syncFieldsType, "SyncSelfTend")?.GetValue(null);

            // TriagePanel: Prevent background local doctor assignments from desyncing
            var triagePanelType = AccessTools.TypeByName("RadiusUI.HealthTab.TriagePanel");
            MpCompat.harmony.Patch(
                AccessTools.Method(triagePanelType, "Enforce"),
                prefix: new HarmonyMethod(typeof(RadiusUIHealthTab), nameof(PrefixEnforce)));

            // TriagePanel: Sync player-directed jobs
            MP.RegisterSyncMethod(triagePanelType, "IssueTend");
            MP.RegisterSyncMethod(triagePanelType, "IssueRescue");

            // GameComp_TendRules: Sync care rules and preferred medicine
            var tendRulesType = AccessTools.TypeByName("RadiusUI.HealthTab.GameComp_TendRules");
            if (tendRulesType != null)
            {
                MP.RegisterSyncMethod(tendRulesType, "Set");
                MP.RegisterSyncMethod(tendRulesType, "SetPreferredMed");
            }

            // HealthTabDrawer: Watch medCare & selfTend during inspector / overview drawing
            var drawerType = AccessTools.TypeByName("RadiusUI.HealthTab.HealthTabDrawer");
            MpCompat.harmony.Patch(
                AccessTools.Method(drawerType, "Draw", new[] { typeof(Rect), typeof(Pawn), typeof(Thing) }),
                prefix: new HarmonyMethod(typeof(RadiusUIHealthTab), nameof(PreDrawHealthTab)),
                postfix: new HarmonyMethod(typeof(RadiusUIHealthTab), nameof(PostDrawHealthTab)),
                finalizer: new HarmonyMethod(typeof(RadiusUIHealthTab), nameof(FinalizeDrawHealthTab)));

            // ModernDropdown: Watch medCare & selfTend during dropdown click executions
            var modernDropdownType = AccessTools.TypeByName("RadiusUI.HealthTab.ModernDropdown");
            MpCompat.harmony.Patch(
                AccessTools.Method(modernDropdownType, nameof(Window.DoWindowContents)),
                prefix: new HarmonyMethod(typeof(RadiusUIHealthTab), nameof(PreDoDropdownContents)),
                postfix: new HarmonyMethod(typeof(RadiusUIHealthTab), nameof(PostDoDropdownContents)),
                finalizer: new HarmonyMethod(typeof(RadiusUIHealthTab), nameof(FinalizeDoDropdownContents)));
        }

        private static bool PrefixEnforce()
        {
            // In MP, Enforce() is driven by client-local static s_manual pins and Find.CurrentMap.
            // Disabling Enforce in multiplayer prevents background autonomous doctor reassignment desyncs,
            // while manual "Tend now" and "Rescue to bed" clicks remain fully synced.
            return !MP.IsInMultiplayer;
        }

        private static void PreDrawHealthTab(Pawn pawn, out bool __state)
        {
            __state = false;
            // Enemy pawns, wild animals, and non-colony entities do not have playerSettings.
            // Watching medCare/selfTend on them throws a NullReferenceException in MpReflection getter.
            if (!MP.IsInMultiplayer || pawn?.playerSettings == null)
                return;

            MP.WatchBegin();
            __state = true;
            syncMedCare?.Watch(pawn);
            syncSelfTend?.Watch(pawn);
        }

        private static void PostDrawHealthTab(ref bool __state)
        {
            if (__state)
            {
                __state = false;
                MP.WatchEnd();
            }
        }

        private static Exception FinalizeDrawHealthTab(Exception __exception, ref bool __state)
        {
            if (__state)
            {
                __state = false;
                MP.WatchEnd();
            }
            return __exception;
        }

        private static void PreDoDropdownContents(out bool __state)
        {
            __state = false;
            if (!MP.IsInMultiplayer)
                return;

            MP.WatchBegin();
            __state = true;
            var pawns = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_OfPlayerFaction;
            if (pawns != null)
            {
                foreach (var p in pawns)
                {
                    if (p?.playerSettings != null)
                    {
                        syncMedCare?.Watch(p);
                        syncSelfTend?.Watch(p);
                    }
                }
            }
        }

        private static void PostDoDropdownContents(ref bool __state)
        {
            if (__state)
            {
                __state = false;
                MP.WatchEnd();
            }
        }

        private static Exception FinalizeDoDropdownContents(Exception __exception, ref bool __state)
        {
            if (__state)
            {
                __state = false;
                MP.WatchEnd();
            }
            return __exception;
        }
    }
}
