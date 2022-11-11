using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Multiplayer.API;
using UnityEngine;
using Verse;
namespace Multiplayer.Compat
{
    /// <summary>Path Avoid by Kiame Vivacity</summary>
    /// <remarks>Everything works</remarks>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=1180719857"/>
    /// <see href="https://github.com/KiameV/rimworld-pathavoid"/>
    [MpCompatFor("pathavoid.kv.rw")]
    public class PathAvoidCompat
    {
        private static Type pathAvoidDefType;
        private static Type designator_PathAvoidType;
        private static Type pathAvoidGridType;

        private static AccessTools.FieldRef<object, int> levelField;
        private static AccessTools.FieldRef<object, Def> pathAvoidDefField;
        private static AccessTools.FieldRef<object, TerrainDef> selectedTerrainDefField;
        private static AccessTools.FieldRef<object, Def> selectedPathAvoidDefField;
        private static AccessTools.FieldRef<object, Def> selectedPathAvoidDefForResetField;

        private static MethodInfo pathAvoidGridSetValueMethod;

        public PathAvoidCompat(ModContentPack mod)
        {
            pathAvoidDefType = AccessTools.TypeByName("PathAvoid.PathAvoidDef");
            levelField = AccessTools.FieldRefAccess<int>(pathAvoidDefType, "level");

            designator_PathAvoidType = AccessTools.TypeByName("PathAvoid.Designator_PathAvoid");
            pathAvoidDefField = AccessTools.FieldRefAccess<Def>(designator_PathAvoidType, "def");

            MP.RegisterSyncWorker<Designator>(PathAvoidDesignatorSyncWorker, designator_PathAvoidType);

            var type = AccessTools.TypeByName("PathAvoid.MapSettingsDialog");
            selectedTerrainDefField = AccessTools.FieldRefAccess<TerrainDef>(type, "selectedTerrainDef");
            selectedPathAvoidDefField = AccessTools.FieldRefAccess<Def>(type, "selectedPathAvoidDef");
            selectedPathAvoidDefForResetField = AccessTools.FieldRefAccess<Def>(type, "selectedPathAvoidDefForReset");
            MpCompat.harmony.Patch(AccessTools.Method(type, nameof(Window.DoWindowContents)),
                transpiler: new HarmonyMethod(typeof(PathAvoidCompat), nameof(ReplaceAcceptButtons)));

            pathAvoidGridType = AccessTools.TypeByName("PathAvoid.PathAvoidGrid");
            pathAvoidGridSetValueMethod = AccessTools.Method(pathAvoidGridType, "SetValue", new[] { typeof(int), typeof(byte) });

            MP.RegisterSyncMethod(typeof(PathAvoidCompat), nameof(SyncedSetAllToDef));
        }

        private static void PathAvoidDesignatorSyncWorker(SyncWorker sw, ref Designator designator)
        {
            Def def = null;

            if (sw.isWriting)
            {
                def = pathAvoidDefField(designator);

                sw.Write(def.defName);
            }
            else
            {
                string defName = sw.Read<string>();

                def = GenDefDatabase.GetDef(pathAvoidDefType, defName, false);

                designator = (Designator)Activator.CreateInstance(designator_PathAvoidType, def);
            }
        }

        private static bool SetAllTerrainToDefButton(Rect rect, string label, bool drawBackground, bool doMouseoverSounds, bool active, TextAnchor? overrideTextAnchor, Window instance)
        {
            var result = Widgets.ButtonText(rect, label, drawBackground, doMouseoverSounds, active, overrideTextAnchor);

            if (!MP.IsInMultiplayer)
                return result;

            if (result)
                SyncedSetAllToDef(Current.Game.CurrentMap, (byte)levelField(selectedPathAvoidDefField(instance)), selectedTerrainDefField(instance));

            return false;
        }

        private static bool SetAllToDefButton(Rect rect, string label, bool drawBackground, bool doMouseoverSounds, bool active, TextAnchor? overrideTextAnchor, Window instance)
        {
            var result = Widgets.ButtonText(rect, label, drawBackground, doMouseoverSounds, active, overrideTextAnchor);

            if (!MP.IsInMultiplayer)
                return result;

            if (result)
                SyncedSetAllToDef(Current.Game.CurrentMap, (byte)levelField(selectedPathAvoidDefForResetField(instance)));

            return false;
        }

        private static void SyncedSetAllToDef(Map map, byte pathAvoidLevel, TerrainDef filter = null)
        {
            var comp = map.GetComponent(pathAvoidGridType);

            if (comp != null)
            {
                for (var i = 0; i < map.terrainGrid.topGrid.Length; i++)
                {
                    if (filter == null || map.terrainGrid.TerrainAt(i) == filter)
                    {
                        pathAvoidGridSetValueMethod.Invoke(comp, new object[] { i, pathAvoidLevel });
                    }
                }
            }
            else
                Log.Error("failed to get path avoid grid");
        }

        private static IEnumerable<CodeInstruction> ReplaceAcceptButtons(IEnumerable<CodeInstruction> instr)
        {
            var buttonMethod = AccessTools.Method(typeof(Widgets), nameof(Widgets.ButtonText), new[] { typeof(Rect), typeof(string), typeof(bool), typeof(bool), typeof(bool), typeof(TextAnchor?) });
            var isAccept = false;
            var isSecond = false;
            var patchedCount = 0;

            foreach (var ci in instr)
            {
                if (isAccept && ci.opcode == OpCodes.Call && (MethodInfo)ci.operand == buttonMethod)
                {
                    // Add instance of the object as parameter
                    yield return new CodeInstruction(OpCodes.Ldarg_0);

                    if (isSecond)
                    {
                        ci.operand = AccessTools.Method(typeof(PathAvoidCompat), nameof(SetAllToDefButton));
                        patchedCount++;
                    }
                    else
                    {
                        ci.operand = AccessTools.Method(typeof(PathAvoidCompat), nameof(SetAllTerrainToDefButton));
                        patchedCount++;
                    }

                    isAccept = false;
                    isSecond = true;
                }
                else if (ci.opcode == OpCodes.Ldstr && (string)ci.operand == "Accept")
                    isAccept = true;

                yield return ci;
            }
            
            if (patchedCount == 0) Log.Warning("Failed to patch PathAvoid accept buttons");
            else if (patchedCount == 1) Log.Warning("Failed to patch both PathAvoid buttons");
        }
    }
}
