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
        public DesignatorShapes(ModContentPack mod)
        {
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
            var defsDatabase = allDatabaseDefs.Invoke(null, Array.Empty<object>());

            if (defsDatabase is IEnumerable defs)
            {
                foreach (var def in defs)
                    pauseOnSelectionField.SetValue(def, false);
            }
        }
    }
}