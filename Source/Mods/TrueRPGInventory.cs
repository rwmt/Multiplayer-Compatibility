using System;
using System.Collections.Generic;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>True RPG Inventory by Astryl</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3744201621"/>
    [MpCompatFor("astryl.truerpginventory")]
    internal class TrueRPGInventory
    {
        private static Action markGearDirtyAction;
        private static Type gridStateCompType;
        private static AccessTools.FieldRef<object, Dictionary<int, int>> positionsGetter;
        private static AccessTools.FieldRef<object, HashSet<int>> shownWeaponsGetter;
        private static AccessTools.FieldRef<object, HashSet<int>> hiddenHelmetsGetter;
        private static AccessTools.FieldRef<object, HashSet<int>> hiddenEyewearGetter;

        public TrueRPGInventory(ModContentPack mod) => LongEventHandler.ExecuteWhenFinished(LatePatch);

        private static void LatePatch()
        {
            InitDirtyAction();

            // 1. Sync direct gear commands
            var gearCommandsType = AccessTools.TypeByName("TrueRPGInventory.GearCommands");
            if (gearCommandsType != null)
            {
                MP.RegisterSyncMethod(gearCommandsType, "Wear").CancelIfAnyArgNull();
                MP.RegisterSyncMethod(gearCommandsType, "Equip").CancelIfAnyArgNull();
                MP.RegisterSyncMethod(gearCommandsType, "UnequipToInventory").CancelIfAnyArgNull();
                MP.RegisterSyncMethod(gearCommandsType, "DropAtFeet").CancelIfAnyArgNull();
                MP.RegisterSyncMethod(gearCommandsType, "DropNearby").CancelIfAnyArgNull();
                MP.RegisterSyncMethod(gearCommandsType, "ToggleForced").CancelIfAnyArgNull();
                MP.RegisterSyncMethod(gearCommandsType, "CycleWeapon").CancelIfAnyArgNull();
                MP.RegisterSyncMethod(gearCommandsType, "MakeSidearm").CancelIfAnyArgNull();
                MP.RegisterSyncMethod(gearCommandsType, "ToggleStripDesignation").CancelIfAnyArgNull();
            }
            else
            {
                Log.Warning("[Multiplayer Compat] TrueRPGInventory: Could not find TrueRPGInventory.GearCommands");
            }

            // 2. Bypass trade window redirect in multiplayer to keep Multiplayer's trade session working
            var tradeRedirectPrefix = AccessTools.DeclaredMethod("TrueRPGInventory.Patch_TradeRedirect:Prefix");
            if (tradeRedirectPrefix != null)
            {
                MpCompat.harmony.Patch(
                    tradeRedirectPrefix,
                    prefix: new HarmonyMethod(typeof(TrueRPGInventory), nameof(CancelTradeRedirectInMp))
                );
            }
            else
            {
                Log.Warning("[Multiplayer Compat] TrueRPGInventory: Could not find TrueRPGInventory.Patch_TradeRedirect:Prefix");
            }

            // 3. Sync GridStateComponent persistent layout and visual settings
            gridStateCompType = AccessTools.TypeByName("TrueRPGInventory.GridStateComponent");
            if (gridStateCompType != null)
            {
                MP.RegisterSyncMethod(gridStateCompType, "SetPos").CancelIfAnyArgNull();
                MP.RegisterSyncMethod(gridStateCompType, "SetHeadgearHidden");
                MP.RegisterSyncMethod(gridStateCompType, "SetShownWeapon").CancelIfAnyArgNull();

                var positionsField = AccessTools.Field(gridStateCompType, "positions");
                if (positionsField != null)
                    positionsGetter = AccessTools.FieldRefAccess<object, Dictionary<int, int>>(positionsField);

                var shownWeaponsField = AccessTools.Field(gridStateCompType, "shownWeapons");
                if (shownWeaponsField != null)
                    shownWeaponsGetter = AccessTools.FieldRefAccess<object, HashSet<int>>(shownWeaponsField);

                var hiddenHelmetsField = AccessTools.Field(gridStateCompType, "hiddenHelmets");
                if (hiddenHelmetsField != null)
                    hiddenHelmetsGetter = AccessTools.FieldRefAccess<object, HashSet<int>>(hiddenHelmetsField);

                var hiddenEyewearField = AccessTools.Field(gridStateCompType, "hiddenEyewear");
                if (hiddenEyewearField != null)
                    hiddenEyewearGetter = AccessTools.FieldRefAccess<object, HashSet<int>>(hiddenEyewearField);

                var setPosMethod = AccessTools.DeclaredMethod(gridStateCompType, "SetPos");
                if (setPosMethod != null)
                    MpCompat.harmony.Patch(setPosMethod, postfix: new HarmonyMethod(typeof(TrueRPGInventory), nameof(PostfixSyncCommandDirty)));

                var setHeadgearHiddenMethod = AccessTools.DeclaredMethod(gridStateCompType, "SetHeadgearHidden");
                if (setHeadgearHiddenMethod != null)
                    MpCompat.harmony.Patch(setHeadgearHiddenMethod, postfix: new HarmonyMethod(typeof(TrueRPGInventory), nameof(PostfixSyncCommandDirty)));

                var setShownWeaponMethod = AccessTools.DeclaredMethod(gridStateCompType, "SetShownWeapon");
                if (setShownWeaponMethod != null)
                    MpCompat.harmony.Patch(setShownWeaponMethod, postfix: new HarmonyMethod(typeof(TrueRPGInventory), nameof(PostfixSyncCommandDirty)));
            }
            else
            {
                Log.Warning("[Multiplayer Compat] TrueRPGInventory: Could not find TrueRPGInventory.GridStateComponent");
            }

            // 4. Invalidate TrueGearTab cache when grid positions or visual flags change
            var hashMethod = AccessTools.DeclaredMethod("TrueRPGInventory.TrueGearTab:Hash", new[] { typeof(Pawn) });
            if (hashMethod != null)
            {
                MpCompat.harmony.Patch(
                    hashMethod,
                    postfix: new HarmonyMethod(typeof(TrueRPGInventory), nameof(PostfixHash))
                );
            }
            else
            {
                Log.Warning("[Multiplayer Compat] TrueRPGInventory: Could not find TrueRPGInventory.TrueGearTab:Hash");
            }

            // 5. Sync Dialog_RPGExchange item transfers
            var exchangeType = AccessTools.TypeByName("TrueRPGInventory.Dialog_RPGExchange");
            if (exchangeType != null)
            {
                MP.RegisterSyncMethod(exchangeType, "MoveItemTo").CancelIfAnyArgNull();
            }
            else
            {
                Log.Warning("[Multiplayer Compat] TrueRPGInventory: Could not find TrueRPGInventory.Dialog_RPGExchange");
            }
        }

        private static void PostfixSyncCommandDirty()
        {
            // Only mark UI dirty when executing a synced command received over multiplayer network
            if (MP.IsInMultiplayer && MP.IsExecutingSyncCommand)
            {
                MarkGearDirty();
            }
        }

        private static void PostfixHash(Pawn pawn, ref int __result)
        {
            if (pawn == null || gridStateCompType == null)
                return;

            var comp = Current.Game?.GetComponent(gridStateCompType);
            if (comp == null)
                return;

            int extraHash = 0;

            var list = pawn.inventory?.innerContainer?.InnerListForReading;
            if (positionsGetter != null && list != null)
            {
                var positions = positionsGetter(comp);
                if (positions != null)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        var thing = list[i];
                        if (thing != null)
                        {
                            if (positions.TryGetValue(thing.thingIDNumber, out int pos))
                                extraHash = extraHash * 31 + pos;
                            else
                                extraHash = extraHash * 31 - 1;
                        }
                    }
                }
            }

            if (shownWeaponsGetter != null && list != null)
            {
                var shownWeapons = shownWeaponsGetter(comp);
                if (shownWeapons != null)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        var thing = list[i];
                        if (thing != null && shownWeapons.Contains(thing.thingIDNumber))
                        {
                            extraHash = extraHash * 31 + thing.thingIDNumber;
                        }
                    }
                }
            }

            if (hiddenHelmetsGetter != null)
            {
                var hiddenHelmets = hiddenHelmetsGetter(comp);
                if (hiddenHelmets != null && hiddenHelmets.Contains(pawn.thingIDNumber))
                {
                    extraHash = extraHash * 31 + 1;
                }
            }

            if (hiddenEyewearGetter != null)
            {
                var hiddenEyewear = hiddenEyewearGetter(comp);
                if (hiddenEyewear != null && hiddenEyewear.Contains(pawn.thingIDNumber))
                {
                    extraHash = extraHash * 31 + 2;
                }
            }

            __result = __result * 31 + extraHash;
        }

        private static void InitDirtyAction()
        {
            var notifyGearMutatedMethod = AccessTools.DeclaredMethod("TrueRPGInventory.TrueGearTab:NotifyGearMutated");
            if (notifyGearMutatedMethod != null)
            {
                markGearDirtyAction = (Action)Delegate.CreateDelegate(typeof(Action), notifyGearMutatedMethod);
            }
            else
            {
                var dirtyField = AccessTools.DeclaredField("TrueRPGInventory.TrueGearTab:Dirty");
                if (dirtyField != null)
                {
                    var dirtyRef = AccessTools.StaticFieldRefAccess<bool>(dirtyField);
                    markGearDirtyAction = () => dirtyRef() = true;
                }
            }
        }

        private static void MarkGearDirty()
        {
            markGearDirtyAction?.Invoke();
        }

        private static bool CancelTradeRedirectInMp()
        {
            return !MP.IsInMultiplayer;
        }
    }
}
