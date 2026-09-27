using System;
using System.Collections.Generic;
using System.Reflection;
using Verse;
using HarmonyLib;
using Multiplayer.API;

namespace Multiplayer.Compat
{
    /// <summary>RunAndGun Continued</summary>
    /// <see href="https://github.com/MemeGoddess/RunAndGun"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3562365100"/>
    [MpCompatFor("memegoddess.RunAndGun")]
    class RunandGunContinued
    {
        static FieldInfo settingsField;
        static FieldInfo movementPenaltyHeavyField;
        static FieldInfo movementPenaltyLightField;
        static FieldInfo selectedWeaponsField;

        public RunandGunContinued(ModContentPack mod)
        {
            MpCompat.RegisterLambdaDelegate("RunAndGun.Harmony.Pawn_DraftController_GetGizmos_Patch", "Postfix", 1);
            PatchingUtilities.PatchUnityRand("RunAndGun.Harmony.MentalStateHandler_TryStartMentalState:shouldRunAndGun", false);

            var runAndGunModType = AccessTools.TypeByName("RunAndGun.RunAndGun");
            var settingsType = AccessTools.TypeByName("RunAndGun.Settings");
            if (runAndGunModType == null || settingsType == null)
            {
                Log.Warning("[MPCompat] RunAndGun: couldn't find RunAndGun.RunAndGun/RunAndGun.Settings, skipping TicksPerMove desync fix");
                return;
            }

            settingsField = AccessTools.Field(runAndGunModType, "settings");
            movementPenaltyHeavyField = AccessTools.Field(settingsType, "movementPenaltyHeavy");
            movementPenaltyLightField = AccessTools.Field(settingsType, "movementPenaltyLight");
            selectedWeaponsField = AccessTools.Field(settingsType, "selectedWeapons");

            if (settingsField == null || movementPenaltyHeavyField == null ||
                movementPenaltyLightField == null || selectedWeaponsField == null)
            {
                Log.Warning("[MPCompat] RunAndGun: couldn't find one or more Settings fields, skipping TicksPerMove desync fix");
                return;
            }

            var ticksPerMoveType = AccessTools.TypeByName("RunAndGun.Harmony.Pawn_TicksPerMove");
            var ticksPerMovePostfix = AccessTools.Method(ticksPerMoveType, "Postfix");
            if (ticksPerMovePostfix == null)
            {
                Log.Warning("[MPCompat] RunAndGun: couldn't find Pawn_TicksPerMove.Postfix, skipping TicksPerMove desync fix");
                return;
            }

            MpCompat.harmony.Patch(ticksPerMovePostfix,
                prefix: new HarmonyMethod(typeof(RunandGunContinued), nameof(UseSyncedSettingsPrefix)),
                postfix: new HarmonyMethod(typeof(RunandGunContinued), nameof(RestoreLocalSettingsPostfix)));
        }

        // Swaps the live settings values for the session-synced ones for the
        // duration of RunAndGun's own postfix, so every client computes the same
        // TicksPerMove result regardless of local config.
        private static void UseSyncedSettingsPrefix(out (int heavy, int light, object weapons)? __state)
        {
            __state = null;
            if (!MP.IsInMultiplayer)
                return;

            var synced = RunAndGunSyncedSettings.Instance;
            if (synced == null)
                return;

            var settings = settingsField.GetValue(null);
            if (settings == null)
                return;

            __state = (
                (int)movementPenaltyHeavyField.GetValue(settings),
                (int)movementPenaltyLightField.GetValue(settings),
                selectedWeaponsField.GetValue(settings)
            );

            movementPenaltyHeavyField.SetValue(settings, synced.movementPenaltyHeavy);
            movementPenaltyLightField.SetValue(settings, synced.movementPenaltyLight);
            selectedWeaponsField.SetValue(settings, synced.selectedWeaponsSnapshot);
        }

        private static void RestoreLocalSettingsPostfix((int heavy, int light, object weapons)? __state)
        {
            if (__state == null)
                return;

            var settings = settingsField.GetValue(null);
            if (settings == null)
                return;

            movementPenaltyHeavyField.SetValue(settings, __state.Value.heavy);
            movementPenaltyLightField.SetValue(settings, __state.Value.light);
            selectedWeaponsField.SetValue(settings, __state.Value.weapons);
        }
    }


    public class RunAndGunSyncedSettings : GameComponent
    {
        public static RunAndGunSyncedSettings Instance;

        public int movementPenaltyHeavy = 40;
        public int movementPenaltyLight = 10;

        // defName -> isSelected, mirroring RunAndGun.Settings.selectedWeapons so the
        // light/heavy classification can't diverge between clients either.
        public Dictionary<string, bool> selectedWeaponsRaw = new Dictionary<string, bool>();

        [System.NonSerialized]
        private object cachedSnapshot;
        [System.NonSerialized]
        private bool captured;

        public object selectedWeaponsSnapshot
        {
            get
            {
                if (cachedSnapshot == null)
                    cachedSnapshot = BuildSnapshotDictionary();
                return cachedSnapshot;
            }
        }

        public RunAndGunSyncedSettings(Game game) { }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            Instance = this;

            if (captured)
                return;

            if (!MP.IsInMultiplayer || MP.IsHosting)
                CaptureFromLocalSettings();

            captured = true;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref movementPenaltyHeavy, "movementPenaltyHeavy", 40);
            Scribe_Values.Look(ref movementPenaltyLight, "movementPenaltyLight", 10);
            Scribe_Collections.Look(ref selectedWeaponsRaw, "selectedWeaponsRaw", LookMode.Value, LookMode.Value);
            if (selectedWeaponsRaw == null)
                selectedWeaponsRaw = new Dictionary<string, bool>();
            cachedSnapshot = null;
        }

        private void CaptureFromLocalSettings()
        {
            var settingsType = AccessTools.TypeByName("RunAndGun.Settings");
            var runAndGunModType = AccessTools.TypeByName("RunAndGun.RunAndGun");
            if (settingsType == null || runAndGunModType == null)
                return;

            var settings = AccessTools.Field(runAndGunModType, "settings")?.GetValue(null);
            if (settings == null)
                return;

            movementPenaltyHeavy = (int)AccessTools.Field(settingsType, "movementPenaltyHeavy").GetValue(settings);
            movementPenaltyLight = (int)AccessTools.Field(settingsType, "movementPenaltyLight").GetValue(settings);

            var selectedWeapons = AccessTools.Field(settingsType, "selectedWeapons").GetValue(settings)
                as System.Collections.IDictionary;
            selectedWeaponsRaw = new Dictionary<string, bool>();
            if (selectedWeapons != null)
            {
                var weaponRecordType = AccessTools.TypeByName("RunAndGun.WeaponRecord");
                var isSelectedField = AccessTools.Field(weaponRecordType, "isSelected");
                foreach (System.Collections.DictionaryEntry entry in selectedWeapons)
                {
                    var record = entry.Value;
                    if (record != null && isSelectedField != null)
                        selectedWeaponsRaw[(string)entry.Key] = (bool)isSelectedField.GetValue(record);
                }
            }

            cachedSnapshot = null;
        }

        private object BuildSnapshotDictionary()
        {
            var settingsType = AccessTools.TypeByName("RunAndGun.Settings");
            var weaponRecordType = AccessTools.TypeByName("RunAndGun.WeaponRecord");
            if (settingsType == null || weaponRecordType == null)
                return null;

            var dictType = typeof(Dictionary<,>).MakeGenericType(typeof(string), weaponRecordType);
            var dict = Activator.CreateInstance(dictType);
            var addMethod = dictType.GetMethod("Add");
            var isSelectedField = AccessTools.Field(weaponRecordType, "isSelected");

            foreach (var kv in selectedWeaponsRaw)
            {
                var record = Activator.CreateInstance(weaponRecordType);
                isSelectedField.SetValue(record, kv.Value);
                addMethod.Invoke(dict, new object[] { kv.Key, record });
            }

            return dict;
        }
    }
}
