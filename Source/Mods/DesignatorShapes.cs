using System;
using System.Collections;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Designator Shapes by merthsoft</summary>
    /// <see href="https://github.com/merthsoft/designator-shapes"/>
    /// <see href="https://steamcommunity.com/workshop/filedetails/?id=1235181370"/>
    [MpCompatFor("Merthsoft.DesignatorShapes")]
    public class DesignatorShapes
    {
        #region Fields

        private static FastInvokeHandler settingsGetter = (_, _) => null;
        private static AccessTools.FieldRef<ModSettings, int> floodFillCellLimitField = null;

        #endregion

        #region Main patch

        public DesignatorShapes(ModContentPack mod)
        {
            // Init harmony patches
            MpCompatPatchLoader.LoadPatch(this);

            // Defs aren't setup yet, so we need to work on them later
            LongEventHandler.ExecuteWhenFinished(LatePatch);

            // Handle undo/redo
            var type = AccessTools.TypeByName("Merthsoft.DesignatorShapes.HistoryManager");
            MP.RegisterSyncMethod(type, "Redo");
            MP.RegisterSyncMethod(type, "Undo");
            // We could instead sync InternalUndo/InteralRedo, but we'd have to make a sync worker for HistoryManager.
            // This could work better if we also synced UndoStack/RedoStack, as those could potentially be cleared
            // after pressing undo/redo button but before the method is synced.
        }

        private static void LatePatch()
        {
            // Fix pause on selected breaking stuff when one player picks flood fill while not paused.
            // The method tries to pause the game in an unsynced context.
            var shapeDefType = AccessTools.TypeByName("Merthsoft.DesignatorShapes.Defs.DesignatorShapeDef");
            var pauseOnSelectionField = AccessTools.DeclaredField(shapeDefType, "pauseOnSelection");

            var allDatabaseDefs = AccessTools.DeclaredPropertyGetter(typeof(DefDatabase<>).MakeGenericType(shapeDefType), "AllDefs");
            var defsDatabase = allDatabaseDefs.Invoke(null, []);

            if (defsDatabase is IEnumerable defs)
            {
                foreach (var def in defs)
                    pauseOnSelectionField.SetValue(def, false);
            }

            // Init quick access to DesignatorShapes.Settings. field
            settingsGetter = MethodInvoker.GetHandler(AccessTools.DeclaredPropertyGetter(
                "Merthsoft.DesignatorShapes.DesignatorShapes:Settings"));
            floodFillCellLimitField = AccessTools.FieldRefAccess<int>(
                "Merthsoft.DesignatorShapes.DesignatorSettings:FloodFillCellLimit");
        }

        #endregion

        #region Flood fill cell limit

        // If the flood fill limit is over 10000 cells, then it could cause an error
        // when writing/reading the packet. Temporarily limit it to 10000 cells.

        [MpCompatPrefix("Merthsoft.DesignatorShapes.Shapes.FloodFill", "Fill")]
        private static void PreFloodFill(ref int? __state)
        {
            if (!MP.IsInMultiplayer)
                return;

            // Just a check that the field and getter were initialized, and that
            // the mod didn't change too much (getter returns ModSettings object)
            if (floodFillCellLimitField == null || settingsGetter(null) is not ModSettings settings)
                return;

            ref var limit = ref floodFillCellLimitField(settings);
            if (limit <= 10000)
                return;

            __state = limit;
            limit = 10000;
        }

        [MpCompatFinalizer("Merthsoft.DesignatorShapes.Shapes.FloodFill", "Fill")]
        private static void PostFloodFill(int? __state)
        {
            if (__state != null)
                floodFillCellLimitField(settingsGetter(null) as ModSettings) = __state.Value;
        }

        #endregion
    }
}