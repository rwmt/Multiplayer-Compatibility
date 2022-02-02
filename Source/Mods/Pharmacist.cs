using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Pharmacist by Fluffy</summary>
    /// <see href="https://github.com/fluffy-mods/Pharmacist"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=1365242717"/>
    [MpCompatFor("Fluffy.Pharmacist")]
    internal class Pharmacist
    {
        private delegate void SetDefaults();

        private static SetDefaults setDefaultsMethod;
        private static AccessTools.FieldRef<object> medicalCareField;
        private static ISyncField diseaseMarginField;
        private static ISyncField minorWoundsThresholdField;
        private static ISyncField diseaseThresholdField;

        public Pharmacist(ModContentPack mod)
        {
            var type = AccessTools.TypeByName("Pharmacist.PharmacistSettings");
            var outer = type;
            setDefaultsMethod = AccessTools.MethodDelegate<SetDefaults>(AccessTools.Method(type, "SetDefaults"));
            medicalCareField = AccessTools.StaticFieldRefAccess<object>(AccessTools.Field(type, "medicalCare"));
            
            type = AccessTools.Inner(outer, "MedicalCare");
            diseaseMarginField = MP.RegisterSyncField(type, "_diseaseMargin");
            minorWoundsThresholdField = MP.RegisterSyncField(type, "_minorWoundsThreshold");
            diseaseThresholdField = MP.RegisterSyncField(type, "_diseaseThreshold");
            MP.RegisterSyncWorker<object>(SyncMedicalCare, type);

            type = AccessTools.TypeByName("Pharmacist.MainTabWindow_Pharmacist");
            MpCompat.RegisterLambdaDelegate(type, "DrawCareSelectors", 0, 1, 2);
            MpCompat.harmony.Patch(AccessTools.Method(type, "DrawOptions"),
                prefix: new HarmonyMethod(typeof(Pharmacist), nameof(PreDrawOptions)),
                postfix: new HarmonyMethod(typeof(Pharmacist), nameof(PostDrawOptions)));
        }

        private static void PreDrawOptions()
        {
            if (!MP.IsInMultiplayer)
                return;

            MP.WatchBegin();
            var target = medicalCareField();
            diseaseMarginField.Watch(target);
            minorWoundsThresholdField.Watch(target);
            diseaseThresholdField.Watch(target);
        }

        private static void PostDrawOptions()
        {
            if (MP.IsInMultiplayer)
                MP.WatchEnd();
        }

        private static void SyncMedicalCare(SyncWorker sync, ref object obj)
        {
            if (sync.isWriting) return;
            
            obj = medicalCareField();
            if (obj != null) return;

            setDefaultsMethod();
            obj = medicalCareField();
        }
    }
}
