using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Corruption: Core by Cpt. Ohu, Updated by Ogliss</summary>
    /// <see href="https://github.com/Ogliss/Corruption.Core"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2547885439"/>
    [MpCompatFor("CptOhu.CorruptionCore")]
    public class CorruptionCore
    {
        // ITab_Pawn_Soul
        private static FastInvokeHandler pawnSoulITabSoulToShowGetter;

        // CompSoul
        private static AccessTools.FieldRef<object, object> compSoulFavourTrackerField;
        private static AccessTools.FieldRef<object, object> compSoulPrayerTrackerField;

        // Dialog_SetPawnPantheon
        private static ConstructorInfo setPawnPantheonDialogConstructor;
        private static AccessTools.FieldRef<object, ThingComp> setPawnPantheonDialogSoulField;

        // Soul_FavourTracker
        private static AccessTools.FieldRef<object, IList> soulFavourTrackerFavoursField;

        // Pawn_PrayerTracker
        private static AccessTools.FieldRef<object, ThingComp> prayerTrackerCompSoulField;
        private static ISyncField prayerTrackerAllowPrayingSync;
        private static ISyncField prayerTrackerShowPrayerSync;

        // FavourProgress
        private static AccessTools.FieldRef<object, float> favourProgressFavourValueField;

        public CorruptionCore(ModContentPack mod)
        {
            // ITab_Pawn_Soul - checkboxes to allow praying and show prayers
            var type = AccessTools.TypeByName("Corruption.Core.Soul.ITab_Pawn_Soul");
            pawnSoulITabSoulToShowGetter = MethodInvoker.GetHandler(AccessTools.PropertyGetter(type, "SoulToShow"));
            MP.RegisterSyncMethod(typeof(CorruptionCore), nameof(SyncFavourValue));
            MpCompat.harmony.Patch(AccessTools.Method(type, "FillTab"),
                prefix: new HarmonyMethod(typeof(CorruptionCore), nameof(PreFillTab)),
                postfix: new HarmonyMethod(typeof(CorruptionCore), nameof(PostFillTab)));

            type = AccessTools.TypeByName("Corruption.Core.Soul.CompSoul");
            compSoulFavourTrackerField = AccessTools.FieldRefAccess<object>(type, "FavourTracker");
            compSoulPrayerTrackerField = AccessTools.FieldRefAccess<object>(type, "PrayerTracker");

            type = AccessTools.TypeByName("Corruption.Core.Dialog_SetPawnPantheon");
            setPawnPantheonDialogConstructor = AccessTools.DeclaredConstructor(type);
            setPawnPantheonDialogSoulField = AccessTools.FieldRefAccess<ThingComp>(type, "soul");
            MP.RegisterSyncMethod(type, "SelectionChanged");
            MP.RegisterSyncWorker<object>(SyncDialogSetPawnPantheon, type);

            type = AccessTools.TypeByName("Corruption.Core.Soul.Soul_FavourTracker");
            soulFavourTrackerFavoursField = AccessTools.FieldRefAccess<IList>(type, "Favours");

            type = AccessTools.TypeByName("Corruption.Core.Gods.Pawn_PrayerTracker");
            prayerTrackerCompSoulField = AccessTools.FieldRefAccess<ThingComp>(type, "compSoul");
            prayerTrackerAllowPrayingSync = MP.RegisterSyncField(type, "AllowPraying");
            prayerTrackerShowPrayerSync = MP.RegisterSyncField(type, "ShowPrayer");
            MP.RegisterSyncWorker<object>(SyncPawnPrayerTracker, type);
            MpCompat.harmony.Patch(AccessTools.Method(type, "AdvancePrayer"),
                prefix: new HarmonyMethod(typeof(CorruptionCore), nameof(PreAdvancePrayer)),
                postfix: new HarmonyMethod(typeof(CorruptionCore), nameof(PostAdvancePrayer)));

            type = AccessTools.TypeByName("Corruption.Core.Soul.FavourProgress");
            favourProgressFavourValueField = AccessTools.FieldRefAccess<float>(type, "favourValue");
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
                    var prayerTracker = compSoulPrayerTrackerField(soul);
                    prayerTrackerAllowPrayingSync.Watch(prayerTracker);
                    prayerTrackerShowPrayerSync.Watch(prayerTracker);
                    __state[0] = true;


                    var favourTracker = compSoulFavourTrackerField(soul);
                    var favoursList = soulFavourTrackerFavoursField(favourTracker);

                    var favorValues = new float[favoursList.Count];
                    for (var i = 0; i < favoursList.Count; i++)
                        favorValues[i] = favourProgressFavourValueField(favoursList[i]);

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
                var favourTracker = compSoulFavourTrackerField(soul);

                var previousFavourList = (float[])__state[1];
                var favoursList = soulFavourTrackerFavoursField(favourTracker);

                // Check for changes, revert them (if any) and change them in synced method
                for (var i = 0; i < favoursList.Count; i++)
                {
                    var newValue = favourProgressFavourValueField(favoursList[i]);
                    var oldValue = previousFavourList[i];

                    if (newValue != oldValue)
                    {
                        favourProgressFavourValueField(favoursList[i]) = oldValue;
                        SyncFavourValue((ThingComp)soul, i, newValue);
                        // We could break here, but let's just keep going in case there's more than one changed
                        // There shouldn't be more than a single difference, but let's be safe
                    }
                }
            }
        }

        private static void SyncFavourValue(ThingComp souldComp, int favourIndex, float value)
        {
            var favourTracker = compSoulFavourTrackerField(souldComp);
            var favoursList = soulFavourTrackerFavoursField(favourTracker);
            favourProgressFavourValueField(favoursList[favourIndex]) = value;
        }

        private static void SyncDialogSetPawnPantheon(SyncWorker sync, ref object window)
        {
            if (sync.isWriting)
            {
                var compSoul = setPawnPantheonDialogSoulField(window);
                sync.Write(compSoul);
            }
            else
            {
                var compSoul = sync.Read<ThingComp>();
                window = setPawnPantheonDialogConstructor.Invoke(new object[] { compSoul, null });
            }
        }

        private static void SyncPawnPrayerTracker(SyncWorker sync, ref object tracker)
        {
            if (sync.isWriting)
                sync.Write(prayerTrackerCompSoulField(tracker));
            else
                tracker = compSoulPrayerTrackerField(sync.Read<ThingComp>());
        }

        private static void PreAdvancePrayer(object __instance)
        {
            if (MP.IsInMultiplayer)
                Rand.PushState(prayerTrackerCompSoulField(__instance).parent.GetHashCode() ^ Find.TickManager.TicksGame);
        }

        private static void PostAdvancePrayer()
        {
            if (MP.IsInMultiplayer)
                Rand.PopState();
        }
    }
}
