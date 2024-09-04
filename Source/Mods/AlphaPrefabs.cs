using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>
    /// Alpha Prefabs by Sarg Bjornson
    /// </summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3070780021"/>
    /// <see href="https://github.com/juanosarg/AlphaPrefabs"/>
    [MpCompatFor("Sarg.AlphaPrefabs")]
    public class AlphaPrefabs
    {
        #region Fields

        // Window_Prefab
        private static Type windowPrefabType;
        private static AccessTools.FieldRef<Window, Def> windowPrefabDefField;
        private static AccessTools.FieldRef<Window, Building> windowPrefabCatalogField;
        private static FastInvokeHandler checkModsSilverAndResearchMethod;

        #endregion

        #region Main Patch

        public AlphaPrefabs(ModContentPack mod)
        {
            MpCompatPatchLoader.LoadPatch(this);

            // Gizmos
            // Build (0), undeploy (1)
            MpCompat.RegisterLambdaMethod("AlphaPrefabs.Building_DeployedPrefab", nameof(Building.GetGizmos), 0, 1);

            // Float menu
            MpCompat.RegisterLambdaDelegate("AlphaPrefabs.CompTargetable_Ground", nameof(CompTargetable.SelectedUseOption), 0);

            // Dialogs
            var type = windowPrefabType = AccessTools.TypeByName("AlphaPrefabs.Window_Prefab");
            windowPrefabDefField = AccessTools.FieldRefAccess<Def>(type, "prefab");
            windowPrefabCatalogField = AccessTools.FieldRefAccess<Building>(type, "building");
            checkModsSilverAndResearchMethod = MethodInvoker.GetHandler(AccessTools.DeclaredMethod(type, "CheckModsSilverAndResearch"));
            MP.RegisterSyncMethod(type, "OrderPrefab")
                .SetContext(SyncContext.CurrentMap);
        }

        #endregion

        #region Immediate Feedback

        [MpCompatPrefix("AlphaPrefabs.Window_Prefab", "OrderPrefab")]
        private static bool PreOrderPrefab(Window __instance)
        {
            // Not in MP, let it run as normal
            if (!MP.IsInMultiplayer)
                return true;

            var args = new object[1];
            if ((bool)checkModsSilverAndResearchMethod(__instance, args))
            {
                // By returning true MP will catch the method call and sync it.
                // However, we close the dialog as well because otherwise it
                // won't get closed for the player as we construct a new dialog
                // in the sync worker. We could access this one... But let's just
                // give the player immediate feedback.
                __instance.Close();
                return true;
            }

            var reason = (string)args[0];
            var catalog = windowPrefabCatalogField(__instance);
            // We need to cast null to LookTargets, else it'll treat it as Thing
            // and implicitly convert it LookTargets with null Thing as the argument.
            // It shouldn't cause issues... But let's just copy what the mod does.
            Messages.Message(reason, catalog != null ? catalog : (LookTargets)null, MessageTypeDefOf.RejectInput, false);
            return false;
        }

        #endregion

        #region No camera position

        // Basically, the method OrderPrefab will use the player camera's position when sending them
        // the prefab after purchasing (or the position of catalog building, if using it instead).
        // We need to patch the method to use a deterministic approach (TradeDropSpot).

        private static IntVec3 ReplaceDropPosition(CameraDriver instance)
        {
            if (!MP.IsInMultiplayer)
                return instance.MapPosition;

            // We need SyncContext.CurrentMap for our replacement and the method itself.
            return DropCellFinder.TradeDropSpot(Find.CurrentMap);
        }

        [MpCompatTranspiler("AlphaPrefabs.Window_Prefab", "GetPosition")]
        private static IEnumerable<CodeInstruction> ReplaceCameraMapPosition(IEnumerable<CodeInstruction> instr, MethodBase baseMethod)
        {
            var target = AccessTools.PropertyGetter(typeof(CameraDriver), nameof(CameraDriver.MapPosition));
            var replacement = AccessTools.DeclaredMethod(typeof(AlphaPrefabs), nameof(ReplaceDropPosition));
            var replacedCount = 0;

            foreach (var ci in instr)
            {
                if (ci.Calls(target))
                {
                    ci.opcode = OpCodes.Call;
                    ci.operand = replacement;
                    
                    replacedCount++;
                }

                yield return ci;
            }

            const int expected = 1;
            if (replacedCount != expected)
            {
                var name = (baseMethod.DeclaringType?.Namespace).NullOrEmpty() ? baseMethod.Name : $"{baseMethod.DeclaringType!.Name}:{baseMethod.Name}";
                Log.Warning($"Patched incorrect number of Find.CameraDriver.MapPosition calls (patched {replacedCount}, expected {expected}) for method {name}");
            }
        }

        #endregion

        #region Sync Workers

        [MpCompatSyncWorker("AlphaPrefabs.Window_Prefab")]
        public static void SyncWindowPrefab(SyncWorker sync, ref Window dialog)
        {
            if (sync.isWriting)
            {
                sync.Write(windowPrefabDefField(dialog));
                sync.Write(windowPrefabCatalogField(dialog));
            }
            else
            {
                var prefabDef = sync.Read<Def>();
                var catalog = sync.Read<Building>();
                dialog ??= (Window)Activator.CreateInstance(windowPrefabType, prefabDef, catalog);
            }
        }

        #endregion
    }
}