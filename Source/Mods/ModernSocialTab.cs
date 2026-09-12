using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Modern Social Tab by Astryl</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3740700588"/>
    [MpCompatFor("astryl.modernsocialtab")]
    [MpCompatFor("astryl.ModernSocialTab")]
    internal class ModernSocialTab
    {
        private static Type compType;
        private static FieldInfo pinnedPawnsField;

        public ModernSocialTab(ModContentPack mod) => LongEventHandler.ExecuteWhenFinished(LatePatch);

        private static void LatePatch()
        {
            var texType = AccessTools.TypeByName("ModernSocialTab.SocialTex");
            if (texType != null)
            {
                var pinField = AccessTools.Field(texType, "Pin");
                if (pinField != null && pinField.GetValue(null) == null)
                {
                    texType.TypeInitializer?.Invoke(null, null);
                }
            }

            compType = AccessTools.TypeByName("ModernSocialTab.ModernSocialTabGameComp");
            pinnedPawnsField = AccessTools.Field(compType, "PinnedPawns");

            MP.RegisterSyncMethod(typeof(ModernSocialTab), nameof(SyncedTogglePin));

            var drawerType = AccessTools.TypeByName("ModernSocialTab.SocialTabDrawer");
            if (drawerType != null)
            {
                var drawRosterCardMethod = AccessTools.Method(drawerType, "DrawRosterCard");
                var handleKeyboardMethod = AccessTools.Method(drawerType, "HandleKeyboard");

                var transpiler = new HarmonyMethod(typeof(ModernSocialTab), nameof(TranspileListAddRemove));

                if (drawRosterCardMethod != null)
                    MpCompat.harmony.Patch(drawRosterCardMethod, transpiler: transpiler);

                if (handleKeyboardMethod != null)
                    MpCompat.harmony.Patch(handleKeyboardMethod, transpiler: transpiler);
            }
        }

        private static void SyncedTogglePin(Pawn pawn)
        {
            if (pawn == null || compType == null || pinnedPawnsField == null)
                return;

            var comp = Current.Game?.GetComponent(compType);
            if (comp == null)
                return;

            if (pinnedPawnsField.GetValue(comp) is List<Pawn> list)
            {
                if (!list.Remove(pawn))
                    list.Add(pawn);
            }
        }

        public static bool SyncedListRemove(List<Pawn> list, Pawn pawn)
        {
            if (MP.IsInMultiplayer)
            {
                SyncedTogglePin(pawn);
                return false;
            }
            return list.Remove(pawn);
        }

        public static void SyncedListAdd(List<Pawn> list, Pawn pawn)
        {
            if (MP.IsInMultiplayer)
            {
                SyncedTogglePin(pawn);
                return;
            }
            list.Add(pawn);
        }

        private static IEnumerable<CodeInstruction> TranspileListAddRemove(IEnumerable<CodeInstruction> instructions)
        {
            var removeMethod = AccessTools.Method(typeof(List<Pawn>), nameof(List<Pawn>.Remove));
            var addMethod = AccessTools.Method(typeof(List<Pawn>), nameof(List<Pawn>.Add));

            var syncedRemove = AccessTools.Method(typeof(ModernSocialTab), nameof(SyncedListRemove));
            var syncedAdd = AccessTools.Method(typeof(ModernSocialTab), nameof(SyncedListAdd));

            foreach (var instr in instructions)
            {
                if (instr.Calls(removeMethod))
                {
                    yield return new CodeInstruction(OpCodes.Call, syncedRemove);
                }
                else if (instr.Calls(addMethod))
                {
                    yield return new CodeInstruction(OpCodes.Call, syncedAdd);
                }
                else
                {
                    yield return instr;
                }
            }
        }
    }
}
