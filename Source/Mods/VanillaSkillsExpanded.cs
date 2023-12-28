using System.Collections;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Vanilla Skills Expanded by legodude17, Oskar Potocki</summary>
    /// <see href="https://github.com/Vanilla-Expanded/VanillaSkillsExpanded"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2854967442"/>
    [MpCompatFor("vanillaexpanded.skills")]
    public class VanillaSkillsExpanded
    {
        private static IDictionary expertiseTrackers;

        public VanillaSkillsExpanded(ModContentPack mod)
        {
            expertiseTrackers = AccessTools.StaticFieldRefAccess<IDictionary>("VSE.ExpertiseTrackers:trackers");

            var type = AccessTools.TypeByName("VSE.ExpertiseTracker");

            MP.RegisterSyncMethod(type, "AddExpertise");
            MP.RegisterSyncMethod(type, "ClearExpertise").SetDebugOnly();
            MP.RegisterSyncWorker<object>(SyncExpertiseTracker, type);
        }

        public static void SyncExpertiseTracker(SyncWorker sync, ref object expertiseTracker)
        {
            if (sync.isWriting)
            {
                var found = false;

                foreach (DictionaryEntry entry in expertiseTrackers)
                {
                    if (entry.Value != expertiseTracker) continue;

                    sync.Write(((Pawn_SkillTracker)entry.Key).pawn);
                    found = true;
                    break;
                }

                if (!found)
                    sync.Write<Pawn>(null);
            }
            else expertiseTracker = expertiseTrackers[sync.Read<Pawn>().skills];
        }
    }
}