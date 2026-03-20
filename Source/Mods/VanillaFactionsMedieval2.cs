using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.Sound;

namespace Multiplayer.Compat
{
    /// <summary>Vanilla Factions Expanded - Medieval 2 by Oskar Potocki, Taranchuk</summary>
    /// <see href="https://github.com/Vanilla-Expanded/VanillaFactionsExpanded-Medieval2"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3444347874"/>
    [MpCompatFor("OskarPotocki.VFE.Medieval2")]
    public class VanillaFactionsMedieval2
    {
        // Barter dialog
        private static Type barterDealType;
        private static MethodInfo tryExecuteMethod;
        private static AccessTools.FieldRef<object, List<Tradeable>> barterDealTradeablesPlayerField;
        private static AccessTools.FieldRef<object, List<Tradeable>> barterDealTradeablesTraderField;
        private static Type dialogBarterType;

        // Stock generation guard
        private static bool inStockGeneration;
        private static AccessTools.FieldRef<CompForbiddable, bool> forbiddenIntField;

        public VanillaFactionsMedieval2(ModContentPack mod)
        {
            // Archery target uses RNG for fleck visuals - but only when the player is looking at it,
            // which may not be the case in MP for all players.
            PatchingUtilities.PatchPushPopRand("VFEMedieval.JobDriver_PlayArchery:ThrowObjectAt");

            // Rally ability gizmo - adds hediff to wearer
            MP.RegisterSyncMethod(AccessTools.DeclaredMethod("VFEMedieval.CompRallyAbility:ActivateAbility"));

            // Apiary dev gizmos
            MpCompat.RegisterLambdaMethod("VFEMedieval.Building_Apiary", nameof(Thing.GetGizmos), 0, 1).SetDebugOnly();

            LongEventHandler.ExecuteWhenFinished(LatePatchBarter);
        }

        private static void LatePatchBarter()
        {
            // Barter dialog sync
            barterDealType = AccessTools.TypeByName("VFEMedieval.BarterDeal");
            tryExecuteMethod = AccessTools.DeclaredMethod(barterDealType, "TryExecute");
            barterDealTradeablesPlayerField = AccessTools.FieldRefAccess<List<Tradeable>>(barterDealType, "tradeablesPlayer");
            barterDealTradeablesTraderField = AccessTools.FieldRefAccess<List<Tradeable>>(barterDealType, "tradeablesTrader");
            dialogBarterType = AccessTools.TypeByName("VFEMedieval.Dialog_Barter");

            // Stock generation guard - MerchantGuild.RegenerateStock creates unspawned things.
            // MP tries to sync CompForbiddable.Forbidden and StorageSettings.CopyFrom on these
            // things, which fails because they're not on any map. We bypass the synced setters
            // entirely during stock generation since trader stock doesn't need sync.
            forbiddenIntField = AccessTools.FieldRefAccess<CompForbiddable, bool>("forbiddenInt");

            MpCompat.harmony.Patch(
                AccessTools.DeclaredMethod("VFEMedieval.MerchantGuild:RegenerateStock"),
                prefix: new HarmonyMethod(typeof(VanillaFactionsMedieval2), nameof(PreRegenerateStock)),
                finalizer: new HarmonyMethod(typeof(VanillaFactionsMedieval2), nameof(PostRegenerateStock)));

            // Bypass CompForbiddable.Forbidden setter during stock gen (skip MP sync transpiler)
            MpCompat.harmony.Patch(
                AccessTools.DeclaredPropertySetter(typeof(CompForbiddable), nameof(CompForbiddable.Forbidden)),
                prefix: new HarmonyMethod(typeof(VanillaFactionsMedieval2), nameof(PreSetForbidden)));

            // Bypass StorageSettings.CopyFrom during stock gen (skip MP sync transpiler)
            MpCompat.harmony.Patch(
                typeof(StorageSettings).GetMethod(nameof(StorageSettings.CopyFrom),
                    BindingFlags.Instance | BindingFlags.Public,
                    null, new[] { typeof(StorageSettings) }, null),
                prefix: new HarmonyMethod(typeof(VanillaFactionsMedieval2), nameof(PreCopyFrom)));

            // Intercept trade execution to sync it
            MpCompat.harmony.Patch(tryExecuteMethod,
                prefix: new HarmonyMethod(typeof(VanillaFactionsMedieval2), nameof(PreTryExecute)));

            // Synced trade execution
            MP.RegisterSyncMethod(typeof(VanillaFactionsMedieval2), nameof(SyncedBarterExecute));
        }

        #region Stock generation guard

        private static void PreRegenerateStock()
        {
            if (MP.IsInMultiplayer)
                inStockGeneration = true;
        }

        private static void PostRegenerateStock(Exception __exception)
        {
            inStockGeneration = false;
        }

        // Skip the Forbidden setter entirely during stock gen.
        // MP's sync transpiler is in the method body — returning false from prefix skips it.
        // Trader stock forbidden state doesn't matter.
        private static bool PreSetForbidden(CompForbiddable __instance, bool value)
        {
            if (!inStockGeneration)
                return true;

            forbiddenIntField(__instance) = value;
            return false;
        }

        // Skip CopyFrom sync during stock gen. Copy filter manually.
        // Storage settings on trader stock items don't need syncing.
        private static bool PreCopyFrom(StorageSettings __instance, StorageSettings other)
        {
            if (!inStockGeneration)
                return true;

            __instance.filter.CopyAllowancesFrom(other.filter);
            return false;
        }

        #endregion

        #region Barter sync

        // Intercept BarterDeal.TryExecute: in MP, sync the trade instead of executing locally
        private static bool PreTryExecute(object __instance, ref bool actuallyTraded)
        {
            if (!MP.IsInMultiplayer || MP.IsExecutingSyncCommand)
                return true;

            // Collect trade counts from current dialog state
            var playerTradeables = barterDealTradeablesPlayerField(__instance);
            var traderTradeables = barterDealTradeablesTraderField(__instance);

            var playerCounts = new int[playerTradeables.Count];
            for (var i = 0; i < playerTradeables.Count; i++)
                playerCounts[i] = playerTradeables[i].CountToTransfer;

            var traderCounts = new int[traderTradeables.Count];
            for (var i = 0; i < traderTradeables.Count; i++)
                traderCounts[i] = traderTradeables[i].CountToTransfer;

            // Sync the trade to all clients
            SyncedBarterExecute(
                TradeSession.playerNegotiator,
                (WorldObject)(object)TradeSession.trader,
                playerCounts, traderCounts);

            // Close dialog on the initiating player (synced method closes on all clients)
            actuallyTraded = true;
            return false;
        }

        // Synced method: recreates the trade session and executes with the specified counts
        private static void SyncedBarterExecute(Pawn negotiator, WorldObject traderObj,
            int[] playerCounts, int[] traderCounts)
        {
            if (negotiator == null || traderObj == null)
                return;

            // Setup trade session on all clients
            TradeSession.SetupWith((ITrader)traderObj, negotiator, false);

            // Create new BarterDeal (deterministic: same items, same seeded prices)
            var deal = Activator.CreateInstance(barterDealType);

            // Apply the trade counts from the initiating player
            var playerTradeables = barterDealTradeablesPlayerField(deal);
            for (var i = 0; i < playerCounts.Length && i < playerTradeables.Count; i++)
                playerTradeables[i].AdjustTo(playerCounts[i]);

            var traderTradeables = barterDealTradeablesTraderField(deal);
            for (var i = 0; i < traderCounts.Length && i < traderTradeables.Count; i++)
                traderTradeables[i].AdjustTo(traderCounts[i]);

            // Execute the trade (calls through Harmony-patched method, prefix allows it via IsExecutingSyncCommand)
            var args = new object[] { false };
            tryExecuteMethod.Invoke(deal, args);
            var actuallyTraded = (bool)args[0];

            if (actuallyTraded)
            {
                SoundDefOf.ExecuteTrade.PlayOneShotOnCamera();
                negotiator.GetCaravan()?.RecacheInventory();
            }

            // Close barter dialog on all clients
            Find.WindowStack.TryRemove(dialogBarterType);
        }

        #endregion
    }
}
