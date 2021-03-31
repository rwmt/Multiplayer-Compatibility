using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using Verse;

namespace Multiplayer.Compat
{
    [MpCompatFor("CptOhu.CorruptionCore")]
    public class CorruptionCore
    {
        // ITab_Pawn_Soul
        private static MethodInfo pawnSoulITabSoulToShowGetter;

        // CompSoul
        private static FieldInfo compSoulFavourTrackerField;
        private static ISyncField compSoulAllowPrayingSync;
        private static ISyncField compSoulShowPrayerSync;

        // Dialog_SetPawnPantheon
        private static Type setPawnPantheonDialogType;
        private static FieldInfo setPawnPantheonDialogSoulField;

        // Soul_FavourTracker
        private static FieldInfo soulFavourTrackerFavoursField;

        // FavourProgress
        private static FieldInfo favourProgressFavourValueField;

        public CorruptionCore(ModContentPack mod)
        {
            // ITab_Pawn_Soul - checkboxes to allow praying and show prayers
            var type = AccessTools.TypeByName("Corruption.Core.Soul.ITab_Pawn_Soul");
            pawnSoulITabSoulToShowGetter = AccessTools.PropertyGetter(type, "SoulToShow");
            MP.RegisterSyncMethod(typeof(CorruptionCore), nameof(SyncFavourValue));
            MpCompat.harmony.Patch(AccessTools.Method(type, "FillTab"),
                prefix: new HarmonyMethod(typeof(CorruptionCore), nameof(PreFillTab)),
                postfix: new HarmonyMethod(typeof(CorruptionCore), nameof(PostFillTab)));

            type = AccessTools.TypeByName("Corruption.Core.Soul.CompSoul");
            compSoulFavourTrackerField = AccessTools.Field(type, "FavourTracker");
            compSoulAllowPrayingSync = MP.RegisterSyncField(type, "PrayerTracker/AllowPraying");
            compSoulShowPrayerSync = MP.RegisterSyncField(type, "PrayerTracker/ShowPrayer");

            setPawnPantheonDialogType = AccessTools.TypeByName("Corruption.Core.Dialog_SetPawnPantheon");
            setPawnPantheonDialogSoulField = AccessTools.Field(setPawnPantheonDialogType, "soul");
            MP.RegisterSyncMethod(setPawnPantheonDialogType, "SelectionChanged");
            MP.RegisterSyncWorker<object>(SyncDialogSetPawnPantheon, setPawnPantheonDialogType);

            type = AccessTools.TypeByName("Corruption.Core.Soul.Soul_FavourTracker");
            soulFavourTrackerFavoursField = AccessTools.Field(type, "Favours");

            type = AccessTools.TypeByName("Corruption.Core.Soul.FavourProgress");
            favourProgressFavourValueField = AccessTools.Field(type, "favourValue");
        }

        private static void PreFillTab(ITab __instance, ref object[] __state)
        {
            if (MP.IsInMultiplayer)
            {
                __state = new object[2];

                var soul = pawnSoulITabSoulToShowGetter.Invoke(__instance, Array.Empty<object>());

                if (soul != null)
                {
                    MP.WatchBegin();
                    compSoulAllowPrayingSync.Watch(soul);
                    compSoulShowPrayerSync.Watch(soul);
                    __state[0] = true;


                    var favourTracker = compSoulFavourTrackerField.GetValue(soul);
                    var favoursList = (IList)soulFavourTrackerFavoursField.GetValue(favourTracker);

                    var favorValues = new float[favoursList.Count];
                    for (int i = 0; i < favoursList.Count; i++)
                        favorValues[i] = (float)favourProgressFavourValueField.GetValue(favoursList[i]);

                    __state[1] = favorValues;
                }
                else __state[0] = false;
            }
        }

        private static void PostFillTab(ITab __instance, ref object[] __state)
        {
            if (MP.IsInMultiplayer && (bool)__state[0])
            {
                MP.WatchEnd();

                // Since we got non-null value in prefix, assume same here
                var soul = pawnSoulITabSoulToShowGetter.Invoke(__instance, Array.Empty<object>());
                var favourTracker = compSoulFavourTrackerField.GetValue(soul);

                var previousFavourList = (float[])__state[1];
                var favoursList = (IList)soulFavourTrackerFavoursField.GetValue(favourTracker);

                // Check for changes, revert them (if any) and change them in synced method
                for (int i = 0; i < favoursList.Count; i++)
                {
                    var newValue = (float)favourProgressFavourValueField.GetValue(favoursList[i]);
                    var oldValue = previousFavourList[i];

                    if (newValue != oldValue)
                    {
                        favourProgressFavourValueField.SetValue(favoursList[i], oldValue);
                        SyncFavourValue((ThingComp)soul, i, newValue);
                        // We could break here, but let's just keep going in case there's more than one changed
                        // There shouldn't be more than a single difference, but let's be safe
                    }
                }
            }
        }

        private static void SyncFavourValue(ThingComp souldComp, int favourIndex, float value)
        {
            var favourTracker = compSoulFavourTrackerField.GetValue(souldComp);
            var favoursList = (IList)soulFavourTrackerFavoursField.GetValue(favourTracker);
            favourProgressFavourValueField.SetValue(favoursList[favourIndex], value);
        }

        private static void SyncDialogSetPawnPantheon(SyncWorker sync, ref object window)
        {
            if (sync.isWriting)
            {
                var compSoul = setPawnPantheonDialogSoulField.GetValue(window);
                sync.Write((ThingComp)compSoul);
            }
            else
            {
                var compSoul = sync.Read<ThingComp>();
                window = Activator.CreateInstance(setPawnPantheonDialogType, compSoul, null);
            }
        }
    }
}
