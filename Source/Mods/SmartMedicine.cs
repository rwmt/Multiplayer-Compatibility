using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Smart Medicine by Uuugggg</summary>
    /// <see href="https://github.com/alextd/Rimworld-SmartMedicine"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=1309994319"/>
    [MpCompatFor("Uuugggg.SmartMedicine")]
    public class SmartMedicine
    {
        private static bool shouldPreventCall = true;
        private static MethodInfo stockUpSettingsMethod;
        private static MethodInfo stockUpPasteMethod;
        private static MethodInfo getCompMethod;
        private static FieldInfo copiedPawnField;

        public SmartMedicine(ModContentPack mod)
        {
            // Stock up medicine/drugs
            {
                var type = AccessTools.TypeByName("SmartMedicine.StockUpUtility");
                stockUpPasteMethod = AccessTools.Method(type, "StockUpPasteSettings");
                stockUpSettingsMethod = AccessTools.Method(type, "StockUpSettings");

                MpCompat.harmony.Patch(AccessTools.Method(type, "SetStockCount", new[] { typeof(Pawn), typeof(ThingDef), typeof(int) }),
                    prefix: new HarmonyMethod(typeof(SmartMedicine), nameof(PreSetStockCount)));
                MpCompat.harmony.Patch(stockUpPasteMethod, 
                    prefix: new HarmonyMethod(typeof(SmartMedicine), nameof(PrePasteSettings)));

                // Mod methods to sync
                MP.RegisterSyncMethod(AccessTools.Method(type, "StockUpStop", new[] { typeof(Pawn), typeof(ThingDef) }));
                MP.RegisterSyncMethod(AccessTools.Method(type, "StockUpClearSettings"));
                // Our methods to sync
                MP.RegisterSyncMethod(typeof(SmartMedicine), nameof(SyncedSetStockCount));
                MP.RegisterSyncMethod(typeof(SmartMedicine), nameof(SyncedPasteSettings));

                // We'll need the access to copiedPawn field to modify it when pasting
                type = AccessTools.TypeByName("SmartMedicine.SmartMedicineGameComp");
                getCompMethod = AccessTools.Method(type, "Get");
                copiedPawnField = AccessTools.Field(type, "copiedPawn");
            }

            // Set wound target tend quality
            {
                var type = AccessTools.TypeByName("SmartMedicine.HediffRowPriorityCare");

                MP.RegisterSyncDelegate(type, "<>c__DisplayClass5_0", "<LabelButton>b__0");
                MP.RegisterSyncDelegate(type, "<>c__DisplayClass5_1", "<LabelButton>b__1");
            }
        }

        private static bool PreSetStockCount(Pawn pawn, ThingDef thingDef, int count)
        {
            if (!MP.IsInMultiplayer)
                return true;

            var dict = (Dictionary<ThingDef, int>)stockUpSettingsMethod.Invoke(null, new object[] { pawn });

            // Make sure there's an actual change here, or else it'll end up spamming the sync method
            // That's the main reason why this method exists - if we only sync original one, it'll end up spamming calls
            if (!dict.TryGetValue(thingDef, out var current) || count != current)
                SyncedSetStockCount(pawn, thingDef, count);

            return false;
        }

        private static void SyncedSetStockCount(Pawn pawn, ThingDef thingDef, int count)
        {
            var dict = (Dictionary<ThingDef, int>)stockUpSettingsMethod.Invoke(null, new object[] { pawn });
            dict[thingDef] = count;
        }

        private static bool PrePasteSettings(Pawn pawn)
        {
            if (MP.IsInMultiplayer && shouldPreventCall)
            {
                shouldPreventCall = true;
                // Get the pawn to copy, and "share" it with everyone to sync up
                var comp = getCompMethod.Invoke(null, Array.Empty<object>());
                var copiedPawn = (Pawn)copiedPawnField.GetValue(comp);
                SyncedPasteSettings(pawn, copiedPawn);
                return false;
            }

            shouldPreventCall = true;
            return true;
        }

        private static void SyncedPasteSettings(Pawn pawn, Pawn copiedPawn)
        {
            var comp = getCompMethod.Invoke(null, Array.Empty<object>());
            // Get original copied pawn (or null if none) to restore later, and replace it with the synced one for now
            var originalPawn = copiedPawnField.GetValue(comp);
            copiedPawnField.SetValue(comp, copiedPawn);

            // Call the actual method, and make sure it's not cancelled/redirected here
            shouldPreventCall = false;
            stockUpPasteMethod.Invoke(null, new object[] { pawn });

            // Restore the original pawn, so if anyone else was copying then they'll be free to do so without the
            // pawn they selected being overriden
            copiedPawnField.SetValue(comp, originalPawn);
        }
    }
}
