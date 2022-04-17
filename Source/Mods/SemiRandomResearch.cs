using HarmonyLib;
using Multiplayer.API;
using RimWorld.Planet;
using System;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Semi Random Research by Captain Muscles & PrissDimmieBana</summary>
    /// <remarks>Sync interface & stuff</remarks>
    /// <see href="https://github.com/CaptainMuscles/CM_Semi_Random_Research"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2375902187"/>
    [MpCompatFor("CaptainMuscles.SemiRandomResearch")]
    public class SemiRandomResearch
    {
        private static Type researchTrackerType;
        private static ISyncField currentAvailableProjectsField;
        private static ISyncField currentProjectField;
        private static ISyncField autoResearchField;
        private static ISyncField rerolledField;
        private static Type nextResearchType;
        public SemiRandomResearch(ModContentPack mod)
        {
            // Sync ResearchTracker fields
            {
                researchTrackerType = AccessTools.TypeByName("CM_Semi_Random_Research.ResearchTracker");

                currentAvailableProjectsField = MP.RegisterSyncField(researchTrackerType, "currentAvailableProjects");
                currentProjectField = MP.RegisterSyncField(researchTrackerType, "currentProject");
                autoResearchField = MP.RegisterSyncField(researchTrackerType, "autoResearch");
                rerolledField = MP.RegisterSyncField(researchTrackerType, "rerolled");

                MpCompat.harmony.Patch(AccessTools.Method(researchTrackerType, "GetCurrentlyAvailableProjects"),
                    prefix: new HarmonyMethod(typeof(SemiRandomResearch), nameof(CurrentAvailableProjectsPrefix)),
                    postfix: new HarmonyMethod(typeof(SemiRandomResearch), nameof(WatchStopPostfix)));
                MpCompat.harmony.Patch(AccessTools.Method(researchTrackerType, "SetCurrentProject"),
                    prefix: new HarmonyMethod(typeof(SemiRandomResearch), nameof(SetCurrentProjectPrefix)),
                    postfix: new HarmonyMethod(typeof(SemiRandomResearch), nameof(WatchStopPostfix)));
                MpCompat.harmony.Patch(AccessTools.Method(researchTrackerType, "Reroll"),
                    prefix: new HarmonyMethod(typeof(SemiRandomResearch), nameof(RerollPrefix)),
                    postfix: new HarmonyMethod(typeof(SemiRandomResearch), nameof(WatchStopPostfix)));

                nextResearchType = AccessTools.TypeByName("CM_Semi_Random_Research.MainTabWindow_NextResearch");
                MpCompat.harmony.Patch(AccessTools.Method(nextResearchType, "DoWindowContents"),
                    prefix: new HarmonyMethod(typeof(SemiRandomResearch), nameof(DoWindowContentsPrefix)),
                    postfix: new HarmonyMethod(typeof(SemiRandomResearch), nameof(WatchStopPostfix)));
            }
        }

        private static void WatchStopPostfix()
        {
            if (MP.IsInMultiplayer)
                MP.WatchEnd();
        }

        private static void CurrentAvailableProjectsPrefix(WorldComponent __instance)
        {
            if (!MP.IsInMultiplayer)
                return;

            MP.WatchBegin();

            if (__instance != null) {
                currentAvailableProjectsField.Watch(__instance);
            }
        }

        private static void SetCurrentProjectPrefix(WorldComponent __instance)
        {
            if (!MP.IsInMultiplayer)
                return;

            MP.WatchBegin();

            if (__instance != null)
            {
                currentAvailableProjectsField.Watch(__instance);
                currentProjectField.Watch(__instance);
            }
        }

        private static void RerollPrefix(WorldComponent __instance)
        {
            if (!MP.IsInMultiplayer)
                return;

            MP.WatchBegin();

            if (__instance != null)
            {
                currentAvailableProjectsField.Watch(__instance);
                currentProjectField.Watch(__instance);
                rerolledField.Watch(__instance);
            }
        }

        private static void DoWindowContentsPrefix()
        {
            if (!MP.IsInMultiplayer)
                return;

            var rt = Current.Game.World.GetComponent(researchTrackerType);

            MP.WatchBegin();

            if (rt != null)
                autoResearchField.Watch(rt);
        }
    }
}

