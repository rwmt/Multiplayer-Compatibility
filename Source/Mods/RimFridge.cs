using System;
using System.Runtime.Serialization;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>RimFridge by KiameV</summary>
    /// <remarks>Fixes for gizmos</remarks>
    /// <see href="https://github.com/KiameV/rimworld-rimfridge"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=1180721235"/>
    [MpCompatFor("rimfridge.kv.rw")]
    public class RimFridgeCompat
    {
        private static AccessTools.FieldRef<object, ThingComp> fridgeField;
        private static Type dialogType;

        public RimFridgeCompat(ModContentPack mod)
        {
            // Several Gizmos
            {
                MpCompat.RegisterLambdaDelegate("RimFridge.CompRefrigerator", "CompGetGizmosExtra", 1, 2, 3, 4, 5);
                MpCompat.RegisterLambdaMethod("RimFridge.CompToggleGlower", "CompGetGizmosExtra", 0);

                dialogType = AccessTools.TypeByName("RimFridge.Dialog_RenameFridge");
                fridgeField = AccessTools.FieldRefAccess<ThingComp>(dialogType, "fridge");

                MP.RegisterSyncWorker<Dialog_Rename>(SyncFridgeName, dialogType);
                MP.RegisterSyncMethod(dialogType, "SetName");
            }

            // Current map usage
            {
                PatchingUtilities.ReplaceCurrentMapUsage("RimFridge.Patch_ReachabilityUtility_CanReach:Prefix");
            }
        }

        private static void SyncFridgeName(SyncWorker sync, ref Dialog_Rename dialog)
        {
            if (sync.isWriting)
                sync.Write(fridgeField(dialog));
            else
            {
                dialog = (Dialog_Rename)FormatterServices.GetUninitializedObject(dialogType);
                fridgeField(dialog) = sync.Read<ThingComp>();
            }
        }
    }
}
