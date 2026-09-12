using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using UnityEngine;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Nice Bill Tab & Nice Bill Tab Expansion by Andromeda / Hicon</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3520130671"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3721023311"/>
    [MpCompatFor("andromeda.nicebilltab")]
    [MpCompatFor("hicon.nicebilltabexpansion")]
    internal class NiceBillTab
    {
        private static Type tabBillsDrawerType;
        private static Type recipeSelectionType;
        private static Type settingsType;
        private static MethodInfo setMaterialMethod;
        private static MethodInfo autoRenameMethod;
        private static MethodInfo getDropIndexMethod;
        private static MethodInfo selectBillMethod;
        private static MethodInfo doAutomaticScrollDecisionMethod;
        private static FieldInfo shouldRefreshFilterField;
        private static FieldInfo draggedBillIndexField;
        private static FieldInfo lastSelTableField;
        private static FieldInfo lockedSelectionField;
        private static FieldInfo enableAutoNamingField;
        private static FieldInfo workTableField;
        private static FieldInfo selectedRecipeField;
        private static FieldInfo styleField;

        public NiceBillTab(ModContentPack mod) => LongEventHandler.ExecuteWhenFinished(LatePatch);

        private static void LatePatch()
        {
            tabBillsDrawerType = AccessTools.TypeByName("NiceBillTab.TabBillsDrawer");
            recipeSelectionType = AccessTools.TypeByName("NiceBillTab.RecipeSelection");
            settingsType = AccessTools.TypeByName("NiceBillTab.Settings");

            if (tabBillsDrawerType != null && recipeSelectionType != null)
            {
                // 1. Sync quantity adjustments (with modifier support) and suspend adjustments
                var minusActionMethod = AccessTools.DeclaredMethod(tabBillsDrawerType, "MinusAction", new[] { typeof(Bill_Production), typeof(RecipeDef) });
                var plusActionMethod = AccessTools.DeclaredMethod(tabBillsDrawerType, "PlusAction", new[] { typeof(Bill_Production), typeof(RecipeDef) });
                if (minusActionMethod != null)
                {
                    MpCompat.harmony.Patch(minusActionMethod, prefix: new HarmonyMethod(typeof(NiceBillTab), nameof(MinusAction_Prefix)));
                }
                if (plusActionMethod != null)
                {
                    MpCompat.harmony.Patch(plusActionMethod, prefix: new HarmonyMethod(typeof(NiceBillTab), nameof(PlusAction_Prefix)));
                }

                MP.RegisterSyncMethod(typeof(NiceBillTab), nameof(SyncedMinusAction)).CancelIfAnyArgNull();
                MP.RegisterSyncMethod(typeof(NiceBillTab), nameof(SyncedPlusAction)).CancelIfAnyArgNull();
                MP.RegisterSyncMethod(tabBillsDrawerType, "SuspendBill").CancelIfAnyArgNull();

                var insertBillMethod = AccessTools.DeclaredMethod(tabBillsDrawerType, "InsertBill", new[] { typeof(Building_WorkTable), typeof(Bill), typeof(int) });
                if (insertBillMethod != null)
                {
                    MpCompat.harmony.Patch(insertBillMethod, prefix: new HarmonyMethod(typeof(NiceBillTab), nameof(InsertBill_Prefix)));
                    MP.RegisterSyncMethod(insertBillMethod).ExposeParameter(1);
                }

                // Reflection members for bill drawer
                setMaterialMethod = AccessTools.DeclaredMethod(tabBillsDrawerType, "SetMaterialToBill", new[] { recipeSelectionType, typeof(RecipeDef), typeof(Bill) });
                autoRenameMethod = AccessTools.DeclaredMethod(tabBillsDrawerType, "AutoRenameBill", new[] { typeof(Bill) });
                getDropIndexMethod = AccessTools.DeclaredMethod(tabBillsDrawerType, "GetDropIndex", new[] { typeof(List<Bill>), typeof(Vector2), typeof(float) });
                selectBillMethod = AccessTools.DeclaredMethod(tabBillsDrawerType, "SelectBill", new[] { typeof(Bill), typeof(bool) });
                doAutomaticScrollDecisionMethod = AccessTools.DeclaredMethod(tabBillsDrawerType, "DoAutomaticScrollDecision", new[] { typeof(bool) });

                shouldRefreshFilterField = AccessTools.DeclaredField(tabBillsDrawerType, "shouldRefreshFilter");
                draggedBillIndexField = AccessTools.DeclaredField(tabBillsDrawerType, "draggedBillIndex");
                lastSelTableField = AccessTools.DeclaredField(tabBillsDrawerType, "LastSelTable");
                lockedSelectionField = AccessTools.DeclaredField(tabBillsDrawerType, "LockedSelection");

                // Reflection fields for recipe selection
                workTableField = AccessTools.DeclaredField(recipeSelectionType, "workTable");
                selectedRecipeField = AccessTools.DeclaredField(recipeSelectionType, "SelectedRecipe");
                styleField = AccessTools.DeclaredField(recipeSelectionType, "style");

                if (settingsType != null)
                {
                    enableAutoNamingField = AccessTools.DeclaredField(settingsType, "EnableAutoNaming");
                }

                // 2. Pre-assign material & name before AddBill so MP serialization captures full bill state
                var tryAddBill = AccessTools.DeclaredMethod(tabBillsDrawerType, "TryAddBillToQueue", new[] { recipeSelectionType });
                if (tryAddBill != null)
                {
                    MpCompat.harmony.Patch(
                        tryAddBill,
                        prefix: new HarmonyMethod(typeof(NiceBillTab), nameof(TryAddBillToQueue_Prefix))
                    );
                }

                // 3. Sync bill drag-and-drop reordering
                var handleBillDrop = AccessTools.DeclaredMethod(tabBillsDrawerType, "HandleBillDrop", new[] { typeof(List<Bill>), typeof(Vector2), typeof(float) });
                if (handleBillDrop != null)
                {
                    MpCompat.harmony.Patch(
                        handleBillDrop,
                        prefix: new HarmonyMethod(typeof(NiceBillTab), nameof(HandleBillDrop_Prefix))
                    );
                }

                // Register our synced bill reorder method
                MP.RegisterSyncMethod(typeof(NiceBillTab), nameof(SyncedReorderBill)).CancelIfAnyArgNull();
            }
            else
            {
                Log.Warning("[Multiplayer Compat] NiceBillTab: Could not find NiceBillTab.TabBillsDrawer");
            }

            // 4. Also patch Utils.TryAddBillToQueue
            var utilsType = AccessTools.TypeByName("NiceBillTab.Utils");
            if (utilsType != null)
            {
                var utilsTryAddBill = AccessTools.DeclaredMethod(utilsType, "TryAddBillToQueue", new[] { typeof(ThingWithComps), typeof(RecipeDef), typeof(int), typeof(Map) });
                if (utilsTryAddBill != null)
                {
                    MpCompat.harmony.Patch(
                        utilsTryAddBill,
                        prefix: new HarmonyMethod(typeof(NiceBillTab), nameof(Utils_TryAddBillToQueue_Prefix))
                    );
                }
            }

            // 5. Sync NiceBillTabExpansion hidden recipes
            var hiddenRecipeStoreType = AccessTools.TypeByName("NiceBillTabExpansion.Storage.HiddenRecipeStore");
            if (hiddenRecipeStoreType != null)
            {
                MP.RegisterSyncMethod(hiddenRecipeStoreType, "SetHidden").CancelIfAnyArgNull();
            }
        }

        private static bool TryAddBillToQueue_Prefix(object selection, ref bool __result)
        {
            if (!MP.IsInMultiplayer || selection == null) return true;

            var workTable = workTableField?.GetValue(selection) as Building_WorkTable;
            var recipe = selectedRecipeField?.GetValue(selection) as RecipeDef;
            var style = styleField?.GetValue(selection) as Precept_ThingStyle;

            if (workTable == null || !workTable.Spawned || recipe == null)
            {
                __result = false;
                return false;
            }

            var lastSelTable = lastSelTableField?.GetValue(null) as Building_WorkTable;
            var map = workTable.Map ?? lastSelTable?.Map;

            if (lockedSelectionField?.GetValue(null) == selection)
            {
                lockedSelectionField.SetValue(null, null);
            }

            if (ModsConfig.BiotechActive && recipe.mechanitorOnlyRecipe && (map == null || !GenCollection.Any(map.mapPawns.FreeColonists, MechanitorUtility.IsMechanitor)))
            {
                Find.WindowStack.Add(new Dialog_MessageBox("RecipeRequiresMechanitor".Translate(recipe.LabelCap)));
                __result = false;
                return false;
            }

            if (map != null && !GenCollection.Any(map.mapPawns.FreeColonists, col => recipe.PawnSatisfiesSkillRequirements(col)))
            {
                Bill.CreateNoPawnsWithSkillDialog(recipe);
            }

            shouldRefreshFilterField?.SetValue(null, true);

            Bill bill = BillUtility.MakeNewBill(recipe, style);

            int multiplier = GetCurrentMultiplier();
            if (multiplier > 1 && bill is Bill_Production billProd)
            {
                billProd.repeatMode = BillRepeatModeDefOf.RepeatCount;
                billProd.repeatCount = multiplier;
            }

            // Pre-apply material and custom auto-naming BEFORE AddBill so MP's ExposeParameter serializes it properly
            try
            {
                setMaterialMethod?.Invoke(null, new[] { selection, recipe, bill });
                if (enableAutoNamingField == null || (bool)enableAutoNamingField.GetValue(null))
                {
                    autoRenameMethod?.Invoke(null, new object[] { bill });
                }
            }
            catch (Exception e)
            {
                Log.Warning($"[Multiplayer Compat] NiceBillTab: Error initializing bill before AddBill: {e}");
            }

            // Call AddBill (which is synchronized by Multiplayer core)
            workTable.billStack.AddBill(bill);

            if (recipe.conceptLearned != null)
            {
                PlayerKnowledgeDatabase.KnowledgeDemonstrated(recipe.conceptLearned, KnowledgeAmount.Total);
            }

            if (TutorSystem.TutorialMode)
            {
                TutorSystem.Notify_Event("AddBill-" + recipe.LabelCap.Resolve());
            }

            if (workTable != lastSelTable)
            {
                CameraJumper.TryJumpAndSelect(workTable);
                doAutomaticScrollDecisionMethod?.Invoke(null, new object[] { true });
            }
            else
            {
                selectBillMethod?.Invoke(null, new object[] { bill, false });
                doAutomaticScrollDecisionMethod?.Invoke(null, new object[] { false });
            }

            __result = true;
            return false;
        }

        private static bool Utils_TryAddBillToQueue_Prefix(ThingWithComps workbench, RecipeDef recipe, int count, Map map)
        {
            if (!MP.IsInMultiplayer || !(workbench is Building_WorkTable workTable) || recipe == null) return true;

            if (ModsConfig.BiotechActive && recipe.mechanitorOnlyRecipe && (map == null || !GenCollection.Any(map.mapPawns.FreeColonists, MechanitorUtility.IsMechanitor)))
            {
                Find.WindowStack.Add(new Dialog_MessageBox("RecipeRequiresMechanitor".Translate(recipe.LabelCap)));
                return false;
            }

            if (map != null && !GenCollection.Any(map.mapPawns.FreeColonists, col => recipe.PawnSatisfiesSkillRequirements(col)))
            {
                Bill.CreateNoPawnsWithSkillDialog(recipe);
                return false;
            }

            Bill bill = BillUtility.MakeNewBill(recipe, null);
            int multiplier = GetCurrentMultiplier();
            int totalCount = count * (multiplier > 1 ? multiplier : 1);
            if (bill is Bill_Production billProd && recipe.products != null && recipe.products.Count > 0)
            {
                int prodCount = recipe.products[0].count;
                int finalCount = Mathf.CeilToInt((float)totalCount / (float)prodCount);
                billProd.repeatMode = BillRepeatModeDefOf.RepeatCount;
                billProd.repeatCount = finalCount;
            }
            else if (multiplier > 1 && bill is Bill_Production billProd2)
            {
                billProd2.repeatMode = BillRepeatModeDefOf.RepeatCount;
                billProd2.repeatCount = multiplier;
            }

            var lastSelTable = lastSelTableField?.GetValue(null) as Building_WorkTable;
            if (workbench == lastSelTable)
            {
                shouldRefreshFilterField?.SetValue(null, true);
                doAutomaticScrollDecisionMethod?.Invoke(null, new object[] { true });
            }

            workTable.billStack.AddBill(bill);

            if (recipe.conceptLearned != null)
            {
                PlayerKnowledgeDatabase.KnowledgeDemonstrated(recipe.conceptLearned, KnowledgeAmount.Total);
            }

            return false;
        }

        private static bool HandleBillDrop_Prefix(List<Bill> filteredBills, Vector2 mousePosition, float width)
        {
            if (!MP.IsInMultiplayer) return true;

            int draggedIndex = (int)(draggedBillIndexField?.GetValue(null) ?? -1);
            if (draggedIndex >= 0 && filteredBills != null && draggedIndex < filteredBills.Count)
            {
                int dropIndex = -1;
                try
                {
                    dropIndex = (int)(getDropIndexMethod?.Invoke(null, new object[] { filteredBills, mousePosition, width }) ?? -1);
                }
                catch
                {
                    dropIndex = -1;
                }

                if (dropIndex >= 0 && dropIndex != draggedIndex)
                {
                    Bill item = filteredBills[draggedIndex];
                    var table = lastSelTableField?.GetValue(null) as Building_WorkTable;
                    if (table?.billStack != null && item != null)
                    {
                        SyncedReorderBill(table, item, dropIndex);
                    }
                }
            }

            return false;
        }

        private static void SyncedReorderBill(Building_WorkTable table, Bill bill, int targetIndex)
        {
            if (table?.billStack?.Bills == null || bill == null) return;

            int currentIndex = table.billStack.Bills.IndexOf(bill);
            if (currentIndex >= 0 && targetIndex >= 0)
            {
                table.billStack.Bills.RemoveAt(currentIndex);
                if (targetIndex > table.billStack.Bills.Count)
                {
                    targetIndex = table.billStack.Bills.Count;
                }
                table.billStack.Bills.Insert(targetIndex, bill);
                shouldRefreshFilterField?.SetValue(null, true);
            }
        }

        private static int GetCurrentMultiplier()
        {
            try
            {
                if (KeyBindingDefOf.ModifierIncrement_100x != null && KeyBindingDefOf.ModifierIncrement_100x.IsDown)
                    return 100;
                if (KeyBindingDefOf.ModifierIncrement_10x != null && KeyBindingDefOf.ModifierIncrement_10x.IsDown)
                    return 10;
            }
            catch
            {
                // ignored
            }

            if (Event.current != null)
            {
                if (Event.current.control)
                    return 100;
                if (Event.current.shift)
                    return 10;
            }

            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                return 100;
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                return 10;

            return 1;
        }

        private static bool MinusAction_Prefix(Bill_Production billProd, RecipeDef recipe)
        {
            if (!MP.IsInMultiplayer) return true;
            SyncedMinusAction(billProd, recipe, GetCurrentMultiplier());
            return false;
        }

        private static bool PlusAction_Prefix(Bill_Production billProd, RecipeDef recipe)
        {
            if (!MP.IsInMultiplayer) return true;
            SyncedPlusAction(billProd, recipe, GetCurrentMultiplier());
            return false;
        }

        [SyncMethod]
        private static void SyncedMinusAction(Bill_Production billProd, RecipeDef recipe, int multiplier)
        {
            if (billProd == null || recipe == null) return;

            if (billProd.repeatMode == BillRepeatModeDefOf.Forever)
            {
                billProd.repeatMode = BillRepeatModeDefOf.RepeatCount;
                billProd.repeatCount = 1;
            }
            else if (billProd.repeatMode == BillRepeatModeDefOf.TargetCount)
            {
                int num = recipe.targetCountAdjustment * multiplier;
                billProd.targetCount = Mathf.Max(0, billProd.targetCount - num);
                billProd.unpauseWhenYouHave = Mathf.Max(0, billProd.unpauseWhenYouHave - num);
            }
            else if (billProd.repeatMode == BillRepeatModeDefOf.RepeatCount)
            {
                billProd.repeatCount = Mathf.Max(0, billProd.repeatCount - multiplier);
            }
            else if (billProd.repeatMode?.defName == "TD_PersonCount" || billProd.repeatMode?.defName == "TD_XPerPerson")
            {
                int num2 = recipe.targetCountAdjustment * multiplier;
                if (billProd.repeatMode.defName == "TD_XPerPerson")
                {
                    billProd.targetCount = Mathf.Max(0, billProd.targetCount - num2);
                    billProd.unpauseWhenYouHave = Mathf.Max(0, billProd.unpauseWhenYouHave - num2);
                }
                else
                {
                    billProd.targetCount -= num2;
                    billProd.unpauseWhenYouHave -= num2;
                }
            }
        }

        [SyncMethod]
        private static void SyncedPlusAction(Bill_Production billProd, RecipeDef recipe, int multiplier)
        {
            if (billProd == null || recipe == null) return;

            if (billProd.repeatMode == BillRepeatModeDefOf.Forever)
            {
                billProd.repeatMode = BillRepeatModeDefOf.RepeatCount;
                billProd.repeatCount = 1;
            }
            else if (billProd.repeatMode == BillRepeatModeDefOf.TargetCount)
            {
                int num = recipe.targetCountAdjustment * multiplier;
                billProd.targetCount += num;
                billProd.unpauseWhenYouHave += num;
            }
            else if (billProd.repeatMode == BillRepeatModeDefOf.RepeatCount)
            {
                billProd.repeatCount += multiplier;
            }
            else if (billProd.repeatMode?.defName == "TD_PersonCount" || billProd.repeatMode?.defName == "TD_XPerPerson")
            {
                int num2 = recipe.targetCountAdjustment * multiplier;
                billProd.targetCount += num2;
                billProd.unpauseWhenYouHave += num2;
            }
        }

        private static void InsertBill_Prefix(Building_WorkTable SelTable, Bill bill, int index)
        {
            if (MP.IsExecutingSyncCommand && bill != null && bill.loadID < 0)
            {
                bill.loadID = Find.UniqueIDsManager.GetNextBillID();
            }
        }
    }
}
