using System;
using System.Reflection;
using System.Runtime.Serialization;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
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
        private static FieldInfo fridgeField;
        private static Type dialogType;

        public RimFridgeCompat(ModContentPack mod)
        {

            // Several Gizmos
            {
                Type type = AccessTools.TypeByName("RimFridge.CompRefrigerator");

                string[] methods = {
                    "<CompGetGizmosExtra>b__1",
                    "<CompGetGizmosExtra>b__2",
                    "<CompGetGizmosExtra>b__3",
                    "<CompGetGizmosExtra>b__4",
                    "<CompGetGizmosExtra>b__5"
                };

                foreach (string method in methods)
                {
                    MP.RegisterSyncDelegate(type, "<>c__DisplayClass10_0", method);
                }

                dialogType = AccessTools.TypeByName("RimFridge.Dialog_RenameFridge");
                fridgeField = AccessTools.Field(dialogType, "fridge");

                MP.RegisterSyncWorker<Dialog_Rename>(SyncFridgeName, dialogType);
                MP.RegisterSyncMethod(dialogType, "SetName");
            }
        }

        private static void SyncFridgeName(SyncWorker sync, ref Dialog_Rename dialog)
        {
            if (sync.isWriting)
                sync.Write((ThingComp)fridgeField.GetValue(dialog));
            else
            {
                dialog = (Dialog_Rename)FormatterServices.GetUninitializedObject(dialogType);
                fridgeField.SetValue(dialog, sync.Read<ThingComp>());
            }
        }
    }
}
