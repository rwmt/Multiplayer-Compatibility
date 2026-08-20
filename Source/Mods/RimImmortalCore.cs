using HarmonyLib;
using Multiplayer.API;
using System;
using System.Reflection;
using Verse;

namespace Multiplayer.Compat
{
    // Synchs only upgrade ritual and toggable abilities
    // Things like random qi flowers spawn not synching and still can (and mustly will) cause desynch

    /// <summary>RimImmortal-Core by LingLuo, 堂丸, 骸鸾, 玉米淀粉, chitoseender, 爱新觉罗—派大星, 逍逍客</summary>
    /// <see href="https://steamcommunity.com/workshop/filedetails/?id=3296476341"/>
    [MpCompatFor("RI.RimImmortal.Core")]
    public class RimImmortalCore
    {
        static Type messageDialogType;
        static FieldInfo pawnField, targetField, upgradeSpotField;

        public RimImmortalCore(ModContentPack mod)
        {
            var harmony = MpCompat.harmony;

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
            if (!MP.IsExecutingSyncCommandIssuedBySelf
                && window != null
                && window.GetType() == messageDialogType)
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
                sync.Write(pawn);
                sync.Write(target);
                sync.Write(spot);
            }
            else
            {
                var pawn = sync.Read<Pawn>();
                var target = sync.Read<LocalTargetInfo>();
                var spot = sync.Read<LocalTargetInfo>();

                dialog = (Window)Activator.CreateInstance(messageDialogType, pawn, target, spot, true);
            }
        }
    }
}