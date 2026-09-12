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
    /// <summary>Character Creator by Astryl</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3762126187"/>
    [MpCompatFor("astryl.moderncc")]
    [MpCompatFor("astryl.ModernCC")]
    internal class ModernCC
    {
        private static MethodInfo teleportMethod;

        public ModernCC(ModContentPack mod) => LongEventHandler.ExecuteWhenFinished(LatePatch);

        private static void LatePatch()
        {
            // 1. Block opening Character Editor mid-game in multiplayer to prevent raw in-memory mutations
            var editorType = AccessTools.TypeByName("MCE.Window_CharacterEditor");
            if (editorType != null)
            {
                var toggleEditor = AccessTools.Method(editorType, "ToggleEditor");
                if (toggleEditor != null)
                {
                    MpCompat.harmony.Patch(
                        toggleEditor,
                        prefix: new HarmonyMethod(typeof(ModernCC), nameof(PrefixBlockInMultiplayer)));
                }

                var doWindowContents = AccessTools.Method(editorType, "DoWindowContents", new[] { typeof(Rect) });
                if (doWindowContents != null)
                {
                    MpCompat.harmony.Patch(
                        doWindowContents,
                        prefix: new HarmonyMethod(typeof(ModernCC), nameof(PrefixBlockWindowContents)));
                }
            }

            // 2. Synchronize pawn skipping/teleportation
            var teleportType = AccessTools.TypeByName("MCE.MCETeleport");
            if (teleportType != null)
            {
                teleportMethod = AccessTools.Method(teleportType, "Teleport");

                var beginMethod = AccessTools.Method(teleportType, "Begin", new[] { typeof(List<Pawn>), typeof(Action) });
                if (beginMethod != null)
                {
                    MpCompat.harmony.Patch(
                        beginMethod,
                        prefix: new HarmonyMethod(typeof(ModernCC), nameof(PrefixTeleportBegin)));
                }

                MP.RegisterSyncMethod(typeof(ModernCC), nameof(SyncedTeleport));
            }
        }

        private static bool PrefixBlockInMultiplayer()
        {
            if (MP.IsInMultiplayer && Current.ProgramState == ProgramState.Playing)
            {
                Messages.Message("Character Editor cannot be opened during a multiplayer game.", MessageTypeDefOf.RejectInput, false);
                return false;
            }
            return true;
        }

        private static bool PrefixBlockWindowContents(Window __instance)
        {
            if (MP.IsInMultiplayer && Current.ProgramState == ProgramState.Playing)
            {
                __instance?.Close(false);
                Messages.Message("Character Editor cannot be opened during a multiplayer game.", MessageTypeDefOf.RejectInput, false);
                return false;
            }
            return true;
        }

        private static bool PrefixTeleportBegin(List<Pawn> pawns, Action onFinish)
        {
            if (!MP.IsInMultiplayer)
                return true;

            if (pawns == null || pawns.Count == 0)
            {
                onFinish?.Invoke();
                return false;
            }

            var map = pawns[0]?.MapHeld;
            if (map == null)
            {
                onFinish?.Invoke();
                return false;
            }

            var targetParams = new TargetingParameters
            {
                canTargetLocations = true,
                canTargetPawns = false,
                canTargetBuildings = false
            };

            Find.Targeter.BeginTargeting(targetParams, target =>
            {
                IntVec3 cell = target.Cell;
                if (!cell.IsValid || !cell.InBounds(map) || !cell.Standable(map))
                {
                    Messages.Message("MCE_CannotSkipThere".Translate(), MessageTypeDefOf.RejectInput, false);
                    return;
                }

                SyncedTeleport(pawns, cell, map);
            }, null, onFinish, null, true);

            return false;
        }

        private static void SyncedTeleport(List<Pawn> pawns, IntVec3 cell, Map map)
        {
            if (pawns == null || pawns.Count == 0 || map == null || teleportMethod == null)
                return;

            int num = 0;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn p = pawns[i];
                if (p != null && p.Spawned && p.MapHeld == map)
                {
                    IntVec3 targetCell = (i == 0) ? cell : CellFinder.RandomClosewalkCellNear(cell, map, 3, null);
                    if (!targetCell.IsValid || !targetCell.InBounds(map))
                        targetCell = cell;

                    if ((bool)teleportMethod.Invoke(null, new object[] { p, targetCell, map }))
                    {
                        num++;
                    }
                }
            }

            if (num > 0)
            {
                FleckDef namedSilentFail = DefDatabase<FleckDef>.GetNamedSilentFail("PsycastSkipInnerExit");
                if (namedSilentFail != null)
                    FleckMaker.Static(cell, map, namedSilentFail, 1f);

                Messages.Message(
                    (num == 1) ? "MCE_TeleportedOne".Translate(pawns[0].LabelShortCap) : "MCE_TeleportedN".Translate(num),
                    MessageTypeDefOf.TaskCompletion, false);
            }
        }
    }
}
