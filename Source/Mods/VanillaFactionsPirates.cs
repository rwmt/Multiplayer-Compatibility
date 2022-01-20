using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Multiplayer.API;
using UnityEngine;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary></summary>
    /// <see href=""/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2723801948"/>
    [MpCompatFor("OskarPotocki.VFE.Pirates")]
    internal class VanillaFactionsPirates
    {
        private static Type warcasketDialogType;
        private static MethodInfo notifySettingsChangedMethod;
        private static FieldInfo warcasketProjectField;

        private static FieldInfo helmetColorField;
        private static FieldInfo shoulderPadsColorField;
        private static FieldInfo armorColorField;

        public VanillaFactionsPirates(ModContentPack mod)
        {
            // Gizmos
            {
                // Enable/disable siege mode
                MpCompat.RegisterLambdaMethod("VFEPirates.Ability_SiegeMode", "GetGizmo", 0, 2);
                // Trigger flight towards tile (right now, the TransportPodsArrivalAction parameter is always null)
                MP.RegisterSyncMethod(AccessTools.TypeByName("VFEPirates.Ability_BlastOff"), "TryLaunch").ExposeParameter(1);
            }

            // Warcasket dialog
            {
                warcasketDialogType = AccessTools.TypeByName("VFEPirates.Buildings.Dialog_WarcasketCustomization");
                notifySettingsChangedMethod = AccessTools.Method(warcasketDialogType, "Notify_SettingsChanged");
                warcasketProjectField = AccessTools.Field(warcasketDialogType, "project");

                MP.RegisterSyncWorker<Window>(SyncWarcasketCustomizationDialog, warcasketDialogType);

                MP.RegisterSyncMethod(warcasketDialogType, "OnAcceptKeyPressed");
                MP.RegisterSyncMethod(warcasketDialogType, "OnCancelKeyPressed");

                MpCompat.RegisterLambdaMethod(warcasketDialogType, "DoWindowContents", 0, 1, 2);

                MpCompat.harmony.Patch(AccessTools.Method(warcasketDialogType, "DoWindowContents"),
                    prefix: new HarmonyMethod(typeof(VanillaFactionsPirates), nameof(PreDoWindowContents)),
                    postfix: new HarmonyMethod(typeof(VanillaFactionsPirates), nameof(PostDoWindowContents)));
                
                var type = AccessTools.TypeByName("VFEPirates.WarcasketProject");

                helmetColorField = AccessTools.Field(type, "colorHelmet");
                shoulderPadsColorField = AccessTools.Field(type, "colorShoulderPads");
                armorColorField = AccessTools.Field(type, "colorArmor");

                MP.RegisterSyncMethod(typeof(VanillaFactionsPirates), nameof(SyncedSetColors));
            }
        }

        private static void PreDoWindowContents(Window __instance, ref Color[] __state)
        {
            if (!MP.IsInMultiplayer)
                return;

            var project = warcasketProjectField.GetValue(__instance);
            __state = new[]
            {
                (Color)helmetColorField.GetValue(project),
                (Color)shoulderPadsColorField.GetValue(project),
                (Color)armorColorField.GetValue(project),
            };
        }

        private static void PostDoWindowContents(Window __instance, Color[] __state)
        {
            if (!MP.IsInMultiplayer)
                return;

            var project = warcasketProjectField.GetValue(__instance);

            var helmetColor = (Color) helmetColorField.GetValue(project);
            var shoulderPadsColor = (Color) shoulderPadsColorField.GetValue(project);
            var armorColor = (Color)armorColorField.GetValue(project);

            if (__state[0] != helmetColor || __state[1] != shoulderPadsColor || __state[2] != armorColor)
            {
                helmetColorField.SetValue(project, __state[0]);
                shoulderPadsColorField.SetValue(project, __state[1]);
                armorColorField.SetValue(project, __state[2]);

                SyncedSetColors(helmetColor, shoulderPadsColor, armorColor);
            }
        }

        private static void SyncedSetColors(Color helmetColor, Color shoulderPadsColor, Color armorColor)
        {
            var window = Find.WindowStack.Windows.FirstOrDefault(w => w.GetType() == warcasketDialogType);

            if (window == null)
            {
                Log.Error("Couldn't find the warcasket customization dialog while trying to sync it");
                return;
            }

            var project = warcasketProjectField.GetValue(window);

            helmetColorField.SetValue(project, helmetColor);
            shoulderPadsColorField.SetValue(project, shoulderPadsColor);
            armorColorField.SetValue(project, armorColor);

            notifySettingsChangedMethod.Invoke(window, Array.Empty<object>());
        }

        private static void SyncWarcasketCustomizationDialog(SyncWorker sync, ref Window dialog)
        {
            if (!sync.isWriting)
                dialog = Find.WindowStack.Windows.FirstOrDefault(w => w.GetType() == warcasketDialogType);
        }
    }
}
