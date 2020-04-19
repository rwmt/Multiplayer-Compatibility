using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using UnityEngine;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Simple Sidearms by PeteTimesSix</summary>
    /// <see href="https://github.com/PeteTimesSix/SimpleSidearms"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=927155256"/>
    /// <remarks>autoLockOnManualSwap is costly for the benefits it brings, pestering
    /// the modder to encapsulate the action in a method is preferable</remarks>
    [MpCompatFor("Simple sidearms")]
    public class SimpleSidearmsCompat
    {
        static PropertyInfo GoldfishModule_PawnProperty;
        static MethodInfo GoldfishModule_GetGoldfishForPawnMethod;

        static PropertyInfo SwapControlsHandler_PawnProperty;
        static MethodInfo SwapControlsHandler_GetHandlerForPawnMethod;
        static ISyncField autoLockOnManualSwapSyncField;

        public SimpleSidearmsCompat(ModContentPack mod)
        {
            Type type;
            {
                type = AccessTools.TypeByName("SimpleSidearms.intercepts.FloatMenuMakerMap_AddHumanLikeOrders_Postfix");

                MP.RegisterSyncDelegate(type, "<>c__DisplayClass0_1", "<AddHumanlikeOrders>b__0");
            }
            {
                type = AccessTools.TypeByName("SimpleSidearms.utilities.WeaponAssingment");

                MP.RegisterSyncMethod(type, "equipSpecificWeaponTypeFromInventory");
                MP.RegisterSyncMethod(type, "equipSpecificWeapon");
                MP.RegisterSyncMethod(type, "dropSidearm");
            }
            /*
            {
                type = AccessTools.TypeByName("SimpleSidearms.rimworld.GoldfishModule");

                MP.RegisterSyncMethod(type, "SetPrimaryEmpty");
                MP.RegisterSyncMethod(type, "AddSidearm");
                MP.RegisterSyncMethod(type, "DropPrimary");
                MP.RegisterSyncMethod(type, "DropSidearm");
                MP.RegisterSyncWorker<object>(SyncWorkerForGoldfishModule, type);

                GoldfishModule_PawnProperty = AccessTools.Property(type, "Owner");
                GoldfishModule_GetGoldfishForPawnMethod = AccessTools.Method(type, "GetGoldfishForPawn");
            }
            */
            // All the following for that tiny lock?!
            // This is an exercise of futility testing the limits of the API
            // TODO: Pester modder to encapsulate autoLockOnManualSwap in a method
            /*
            LongEventHandler.ExecuteWhenFinished(delegate {
                type = AccessTools.TypeByName("SimpleSidearms.rimworld.SwapControlsHandler");

                MP.RegisterSyncWorker<object>(SyncWorkerForSwapControlsHandler, type);

                autoLockOnManualSwapSyncField = MP.RegisterSyncField(type, "autoLockOnManualSwap");

                SwapControlsHandler_PawnProperty = AccessTools.Property(type, "Owner");
                SwapControlsHandler_GetHandlerForPawnMethod = AccessTools.Method(type, "GetHandlerForPawn");

                type = AccessTools.TypeByName("SimpleSidearms.rimworld.Gizmo_SidearmsList");

                MpCompat.harmony.Patch(AccessTools.Method(type, "DrawLocklock"),
                    prefix: new HarmonyMethod(typeof(SimpleSidearmsCompat), nameof(DrawLockPrefix)),
                    postfix: new HarmonyMethod(typeof(SimpleSidearmsCompat), nameof(DrawLockPostfix)));
            });
            */
        }

        // This is required to sync a Pawn, GoldFishModule is included.
        static void SyncWorkerForGoldfishModule(SyncWorker sync, ref object obj)
        {
            Pawn pawn = null;
            if (sync.isWriting) {
                pawn = (Pawn) GoldfishModule_PawnProperty.GetValue(obj, new object[] { });

                sync.Write(pawn);
            } else {
                pawn = sync.Read<Pawn>();

                obj = GoldfishModule_GetGoldfishForPawnMethod.Invoke(null, new object[] { pawn });
            }
        }

        #region DrawLocklock
        // All the following is for the tiny lock of doom

        static void SyncWorkerForSwapControlsHandler(SyncWorker sync, ref object obj)
        {
            Pawn pawn = null;
            if (sync.isWriting) {
                pawn = (Pawn) SwapControlsHandler_PawnProperty.GetValue(obj, new object[] { });

                // If Pawn is null, it was saved and got deleted.
                if (pawn == null) {
                    sync.Write(false);

                    // Must use more reflection to traverse SimpleSidearms.saveData.handlers
                    // This will desync if unhandled :(

                    throw new ArgumentException("About to desync, tiny lock of doom triggered. Refusing to comply");
                } else {
                    sync.Write(true);
                    sync.Write(pawn);
                }

            } else {
                bool exists = sync.Read<bool>();
                if (exists) {
                    pawn = sync.Read<Pawn>();
                    obj = SwapControlsHandler_GetHandlerForPawnMethod.Invoke(null, new object[] { pawn });
                }
            }
        }
        static void DrawLockPrefix(object __instance, object handler)
        {
            if (MP.IsInMultiplayer) {
                MP.WatchBegin();

                autoLockOnManualSwapSyncField.Watch(handler);
            }
        }
        static void DrawLockPostfix()
        {
            if (MP.IsInMultiplayer) {
                MP.WatchEnd();
            }
        }

        #endregion
    }
}
