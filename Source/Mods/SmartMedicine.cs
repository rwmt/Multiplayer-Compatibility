using System;
using System.Collections.Generic;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Smart Medicine by Uuugggg, Compact Hediffs by PeteTimesSix</summary>
    /// SmartMedicine:
    /// <see href="https://github.com/alextd/Rimworld-SmartMedicine"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=1309994319"/>
    /// CompactHediffs:
    /// <see href="https://github.com/PeteTimesSix/CompactHediffs"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2031734067"/>
    [MpCompatFor("Uuugggg.SmartMedicine")]
    public class SmartMedicine
    {
        private delegate void StockUpPasteSettingsDelegate(Pawn pawn);

        private delegate Dictionary<ThingDef, int> StockUpSettingsDelegate(Pawn pawn);

        private static Type smartMedicineCompType;
        private static StockUpSettingsDelegate stockUpSettingsMethod;
        private static StockUpPasteSettingsDelegate stockUpPasteMethod;
        private static AccessTools.FieldRef<object, Pawn> copiedPawnField;

        // CompatcHediffs compat
        private static FastInvokeHandler getSmartMedicinePriorityCareDictionary;
        private static AccessTools.FieldRef<object, object> delegateTypeHediffCareField;

        public SmartMedicine(ModContentPack mod)
        {
            // Stock up medicine/drugs
            {
                var type = AccessTools.TypeByName("SmartMedicine.StockUpUtility");

                var pasteMethod = AccessTools.Method(type, "StockUpPasteSettings");

                stockUpPasteMethod = AccessTools.MethodDelegate<StockUpPasteSettingsDelegate>(pasteMethod);
                stockUpSettingsMethod = AccessTools.MethodDelegate<StockUpSettingsDelegate>(AccessTools.Method(type, "StockUpSettings"));

                MpCompat.harmony.Patch(pasteMethod,
                    prefix: new HarmonyMethod(typeof(SmartMedicine), nameof(PrePasteSettings)));
                MpCompat.harmony.Patch(AccessTools.Method(type, "SetStockCount", new[] { typeof(Pawn), typeof(ThingDef), typeof(int) }),
                    prefix: new HarmonyMethod(typeof(SmartMedicine), nameof(PreSetStockCount)));

                // Mod methods to sync
                MP.RegisterSyncMethod(AccessTools.Method(type, "StockUpStop", new[] { typeof(Pawn), typeof(ThingDef) }));
                MP.RegisterSyncMethod(AccessTools.Method(type, "StockUpClearSettings"));
                // Our methods to sync
                MP.RegisterSyncMethod(typeof(SmartMedicine), nameof(SyncedSetStockCount));
                MP.RegisterSyncMethod(typeof(SmartMedicine), nameof(SyncedPasteSettings));

                // We'll need the access to copiedPawn field to modify it when pasting
                smartMedicineCompType = AccessTools.TypeByName("SmartMedicine.SmartMedicineGameComp");
                copiedPawnField = AccessTools.FieldRefAccess<Pawn>(smartMedicineCompType, "copiedPawn");
            }

            // Set wound target tend quality
            {
                var type = AccessTools.TypeByName("SmartMedicine.HediffRowPriorityCare");

                MpCompat.RegisterLambdaDelegate(type, "LabelButton", 0, 1);

                // CompatcHediffs compat
                type = AccessTools.TypeByName("PeteTimesSix.CompactHediffs.Rimworld.UI_compat.UI_SmartMedicine");
                if (type != null)
                {
                    var delegateMethod = MpMethodUtil.GetLambda(type, "AddSmartMedicineFloatMenuButton", lambdaOrdinal: 1);
                    var delegateType = delegateMethod.DeclaringType;

                    MP.RegisterSyncDelegate(type, delegateType!.Name, delegateMethod.Name, new[] { "hediffs" });
                    MpCompat.RegisterLambdaDelegate(type, "AddSmartMedicineFloatMenuButton", new[] { "CS$<>8__locals1/hediffs", "mc" }, 2);

                    delegateTypeHediffCareField = AccessTools.FieldRefAccess<object>(delegateType, "hediffCares");
                    MpCompat.harmony.Patch(AccessTools.Constructor(delegateType),
                        prefix: new HarmonyMethod(typeof(SmartMedicine), nameof(InitHediffCareDictionary)));

                    type = AccessTools.TypeByName("SmartMedicine.PriorityCareComp");
                    getSmartMedicinePriorityCareDictionary = MethodInvoker.GetHandler(AccessTools.Method(type, "Get"));
                }
            }
        }

        private static bool PreSetStockCount(Pawn pawn, ThingDef thingDef, int count)
        {
            if (!MP.IsInMultiplayer)
                return true;

            var dict = stockUpSettingsMethod(pawn);

            // Make sure there's an actual change here, or else it'll end up spamming the sync method
            // That's the main reason why this method exists - if we only sync original one, it'll end up spamming calls
            if (!dict.TryGetValue(thingDef, out var current) || count != current)
                SyncedSetStockCount(pawn, thingDef, count);

            return false;
        }

        private static void SyncedSetStockCount(Pawn pawn, ThingDef thingDef, int count)
        {
            var dict = stockUpSettingsMethod(pawn);
            dict[thingDef] = count;
        }

        private static bool PrePasteSettings(Pawn pawn)
        {
            if (MP.IsInMultiplayer && !MP.IsExecutingSyncCommand)
            {
                // Get the pawn to copy, and "share" it with everyone to sync up
                var comp = Current.Game.GetComponent(smartMedicineCompType);
                var copiedPawn = copiedPawnField(comp);
                SyncedPasteSettings(pawn, copiedPawn);
                return false;
            }

            return true;
        }

        private static void SyncedPasteSettings(Pawn pawn, Pawn copiedPawn)
        {
            var comp = Current.Game.GetComponent(smartMedicineCompType);
            // Get original copied pawn (or null if none) to restore later, and replace it with the synced one for now
            var originalPawn = copiedPawnField(comp);
            copiedPawnField(comp) = copiedPawn;

            // Call the actual method, and make sure it's not cancelled/redirected here
            stockUpPasteMethod(pawn);

            // Restore the original pawn, so if anyone else was copying then they'll be free to do so without the
            // pawn they selected being overriden
            copiedPawnField(comp) = originalPawn;
        }

        // CompactHeddifs compat
        private static void InitHediffCareDictionary(object __instance)
            => delegateTypeHediffCareField(__instance) = getSmartMedicinePriorityCareDictionary(null);
    }
}