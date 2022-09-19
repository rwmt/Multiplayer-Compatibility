using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Medical Tab by Fluffy</summary>
    /// <see href="https://github.com/fluffy-mods/MedicalTab"/>
    /// <see href="https://steamcommunity.com/workshop/filedetails/?id=715565817"/>
    [MpCompatFor("Fluffy.MedicalTab")]
    internal class MedicalTab
    {
        private static ISyncField syncMedCare;
        private static ISyncField[] syncDefaultCare;

        private delegate MainTabWindow_PawnTable GetMainTab();
        private static GetMainTab mainTabGetter;

        public MedicalTab(ModContentPack mod)
        {
            var type = AccessTools.TypeByName("Multiplayer.Client.SyncFields");
            syncMedCare = (ISyncField)AccessTools.Field(type, "SyncMedCare").GetValue(null);
            syncDefaultCare = (ISyncField[])AccessTools.Field(type, "SyncDefaultCare").GetValue(null);

            type = AccessTools.TypeByName("Fluffy.MainTabWindow_Medical");
            mainTabGetter = AccessTools.MethodDelegate<GetMainTab>(AccessTools.PropertyGetter(type, "Instance"));

            type = AccessTools.TypeByName("Fluffy.PawnColumnWorker_MedicalCare");
            MpCompat.harmony.Patch(AccessTools.Method(type, nameof(PawnColumnWorker.DoHeader)),
                prefix: new HarmonyMethod(typeof(MedicalTab), nameof(PreDoHeader)),
                postfix: new HarmonyMethod(typeof(MedicalTab), nameof(StopWatch)));
            MpCompat.harmony.Patch(AccessTools.Method(type, nameof(PawnColumnWorker.DoCell)),
                prefix: new HarmonyMethod(typeof(MedicalTab), nameof(PreDoCell)),
                postfix: new HarmonyMethod(typeof(MedicalTab), nameof(StopWatch)));

            type = AccessTools.TypeByName("Fluffy.PawnColumnWorker_SelfTend");
            // Last parameter (PawnTable) does not need sync, but whatever - no "SkipParameter" method
            MP.RegisterSyncMethod(type, "SetValue");
        }

        private static void PreDoHeader()
        {
            if (!MP.IsInMultiplayer)
                return;

            MP.WatchBegin();
            foreach (var syncField in syncDefaultCare)
                syncField.Watch();
            foreach (var pawn in mainTabGetter().table.PawnsListForReading)
                syncMedCare.Watch(pawn);
        }

        private static void PreDoCell(Pawn pawn)
        {
            if (!MP.IsInMultiplayer)
                return;

            MP.WatchBegin();
            syncMedCare.Watch(pawn);
        }

        private static void StopWatch()
        {
            if (MP.IsInMultiplayer)
                MP.WatchEnd();
        }
    }
}
