using HarmonyLib;
using Multiplayer.API;
using System;
using System.Reflection;
using Verse;

namespace Multiplayer.Compat
{
    [MpCompatFor("RI.RimImmortal.Core")]
    public class RimImmortalCore
    {
        static Type messageDialogType;
        static FieldInfo pawnField, targetField, upgradeSpotField;

        public RimImmortalCore(ModContentPack mod)
        {
            var harmony = new Harmony("rimworld.multiplayer.compat.rimimmortal");

            // Toggable abilities
            var abilitiesType = AccessTools.TypeByName("WhoXiuXian.Abilities.CompAbilityEffect_ToggleHediff");
            MP.RegisterSyncDelegate(abilitiesType, "<>c__DisplayClass8_0", "<GetGizmos>b__2");

            // Rituals (at least upgrade)
            // Synching upgrade ritual call
            var ritualBuildingType = AccessTools.TypeByName("RIRitualFramework.Building_Upgrade");
            MP.RegisterSyncDelegate(ritualBuildingType, "<>c__DisplayClass5_0", "<GetFloatMenuOptions>b__0");

            // Patching dialog
            messageDialogType = AccessTools.TypeByName("RIRitualFramework.MessageDialog");
            pawnField = AccessTools.Field(messageDialogType, "pawn");
            targetField = AccessTools.Field(messageDialogType, "target");
            upgradeSpotField = AccessTools.Field(messageDialogType, "upgradeSpot");

            harmony.Patch(
                AccessTools.Method(typeof(WindowStack), nameof(WindowStack.Add)),
                prefix: new HarmonyMethod(typeof(RimImmortalCore), nameof(WindowStackAddPrefix))
            );

            MP.RegisterSyncWorker<Window>(SyncMessageDialog, messageDialogType, isImplicit: true, shouldConstruct: false);
            MP.RegisterSyncMethod(messageDialogType, "StartRIRitual");
        }

        static bool WindowStackAddPrefix(Window window)
        {
            if (window != null
                && window.GetType() == messageDialogType
                && MP.IsInMultiplayer
                && !MP.IsExecutingSyncCommandIssuedBySelf)
            {
                return false; // Clients
            }
            return true;
        }

        static void SyncMessageDialog(SyncWorker sync, ref Window dialog)
        {
            if (sync.isWriting)
            {
                var pawn = (Pawn)pawnField.GetValue(dialog);
                var target = (LocalTargetInfo)targetField.GetValue(dialog);
                var spot = (LocalTargetInfo)upgradeSpotField.GetValue(dialog);
                sync.Bind(ref pawn);
                sync.Bind(ref target);
                sync.Bind(ref spot);
            }
            else
            {
                Pawn pawn = null;
                LocalTargetInfo target = default;
                LocalTargetInfo spot = default;
                sync.Bind(ref pawn);
                sync.Bind(ref target);
                sync.Bind(ref spot);

                dialog = (Window)Activator.CreateInstance(messageDialogType, pawn, target, spot, true);
            }
        }
    }
}