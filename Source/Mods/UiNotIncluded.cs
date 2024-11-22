using System;
using System.Collections;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Multiplayer.Compat;

/// <summary>UI Not Included: Customizable UI Overhaul by GonDragon</summary>
/// <see href="https://github.com/GonDragon/UINotIncluded"/>
/// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2588873455"/>
[MpCompatFor("GonDragon.UINotIncluded")]
public class UiNotIncluded
{
    #region MainPatch

    public UiNotIncluded(ModContentPack mod) => LongEventHandler.ExecuteWhenFinished(LatePatch);

    private static void LatePatch()
    {
        string errorType;
        void LogError(string error) => Log.Error($"Patching UI Not Included failed ({errorType}): {error}");

        #region Hotkey

        {
            errorType = "time control hotkeys";

            var type = AccessTools.TypeByName("Multiplayer.Client.AsyncTime.TimeControlPatch");
            var doTimeControlsHotkeys = AccessTools.DeclaredMethod(type, "DoTimeControlsHotkeys");
            var timespeedConfig = AccessTools.TypeByName("UINotIncluded.Widget.Configs.TimespeedConfig");
            type = AccessTools.TypeByName("UINotIncluded.Settings");
            var vanillaControlSpeed = AccessTools.DeclaredField(type, "vanillaControlSpeed");
            var topBar = AccessTools.DeclaredField(type, "topBar");
            var bottomBar = AccessTools.DeclaredField(type, "bottomBar");
            var settingsExposeDataMethod = AccessTools.DeclaredMethod(type, "ExposeData");
            type = AccessTools.TypeByName("UINotIncluded.UINI_Mod");
            var doSettingsWindowMethod = AccessTools.DeclaredMethod(type, "DoSettingsWindowContents");

            // Since failing to patch the mod will most likely result in a game that can't be
            // unpaused and/or constant errors, handle patching with way too much extra safety.
            if (doTimeControlsHotkeys == null)
                LogError("MP's DoTimeControlsHotkeys method doesn't exist.");
            else if (!doTimeControlsHotkeys.IsStatic)
                LogError("MP's DoTimeControlsHotkeys method is not static.");
            else if (doTimeControlsHotkeys.ReturnType != typeof(void))
                LogError("MP's DoTimeControlsHotkeys method does not have a void return type.");
            else if (doTimeControlsHotkeys.GetParameters() is not { Length: 0 })
                LogError($"MP's DoTimeControlsHotkeys method has incorrect number of arguments.");
            else if (timespeedConfig == null)
                LogError("TimespeedConfig type is null.");
            else if(vanillaControlSpeed == null)
                LogError("vanillaControlSpeed field is null");
            else if (!vanillaControlSpeed.IsStatic)
                LogError("vanillaControlSpeed field is not static.");
            else if (vanillaControlSpeed.FieldType != typeof(bool))
                LogError("vanillaControlSpeed field is not of type bool.");
            else if(topBar == null)
                LogError("topBar field is null.");
            else if (!topBar.IsStatic)
                LogError("topBar field is not static.");
            else if (!typeof(IList).IsAssignableFrom(topBar.FieldType))
                LogError("topBar field is not of type IList.");
            else if(bottomBar == null)
                LogError("bottomBar field is null.");
            else if (!bottomBar.IsStatic)
                LogError("bottomBar field is not static.");
            else if (!typeof(IList).IsAssignableFrom(bottomBar.FieldType))
                LogError("bo$ttomBar field is not of type IList.");
            else if (settingsExposeDataMethod == null)
                LogError("ExposeData method is null.");
            else if (doSettingsWindowMethod == null)
                LogError("DoSettingsWindowContents method is null.");
            else
            {
                doTimeControlsHotkeysMethod = MethodInvoker.GetHandler(doTimeControlsHotkeys);
                timespeedConfigType = timespeedConfig;
                vanillaControlSpeedField = AccessTools.StaticFieldRefAccess<bool>(vanillaControlSpeed);
                topBarField = AccessTools.StaticFieldRefAccess<IList>(topBar);
                bottomBarField = AccessTools.StaticFieldRefAccess<IList>(bottomBar);

                MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(GlobalControlsUtility), nameof(GlobalControlsUtility.DoTimespeedControls)),
                    new HarmonyMethod(PreDoTimespeedControls));
                MpCompat.harmony.Patch(doSettingsWindowMethod, new HarmonyMethod(PostPotentialSettingsChange));
                MpCompat.harmony.Patch(settingsExposeDataMethod, new HarmonyMethod(PostPotentialSettingsChange));
            }
        }

        #endregion

        #region Time Controls Widget

        {
            errorType = "time control widget";

            var type = AccessTools.TypeByName("UINotIncluded.Widget.Workers.Timespeed_Worker");
            var timeControlsMethodField = AccessTools.DeclaredField(type, "cached_DoTimeControlsGUI");
            type = AccessTools.TypeByName("Multiplayer.Client.AsyncTime.TimeControlPatch");
            var mpDoGuiMethod = AccessTools.DeclaredMethod(type, "DoTimeControlsGUI");

            // Since failing to patch the mod will most likely result in a game that can't be
            // unpaused and/or constant errors, handle patching with way too much extra safety.
            if (timeControlsMethodField == null)
                LogError("cached time control field doesn't exist.");
            else if (!timeControlsMethodField.IsStatic)
                LogError("cached time control field is not static.");
            else if (timeControlsMethodField.FieldType != typeof(Action<Rect>))
                LogError("cached time control field type is not Action<Rect>.");
            else if (mpDoGuiMethod == null)
                LogError("MP's DoTimeControlsGUI method doesn't exist.");
            else if (!mpDoGuiMethod.IsStatic)
                LogError("MP's DoTimeControlsGUI method is not static.");
            else if (mpDoGuiMethod.ReturnType != typeof(void))
                LogError("MP's DoTimeControlsGUI method does not have a void return type.");
            else if (mpDoGuiMethod.GetParameters() is not { Length: 1 } parms)
                LogError("MP's DoTimeControlsGUI method has incorrect number of arguments.");
            else if (parms[0].ParameterType != typeof(Rect))
                LogError("MP's DoTimeControlsGUI method argument is not of type Rect.");
            // Replace the mod's cached delegate to vanilla DoTimeControlsGUI
            // method with MP's DoTimeControlsGUI method instead.
            else
                timeControlsMethodField.SetValue(null, Delegate.CreateDelegate(typeof(Action<Rect>), mpDoGuiMethod));
        }

        #endregion

        // Things unchanged: setting the "play settings at top" button will cause them to overlap
        // with Multiplayer chat and (if they're enabled) debug buttons. Since it's an option
        // I'm not going to bother changing this at all.
    }

    #endregion

    #region Hotkey patches

    // MP Compat
    private static bool needToListenToHotkeys = false;
    // MP
    private static FastInvokeHandler doTimeControlsHotkeysMethod;
    // UI Not Included
    private static Type timespeedConfigType;
    private static AccessTools.FieldRef<bool> vanillaControlSpeedField;
    private static AccessTools.FieldRef<IList> topBarField;
    private static AccessTools.FieldRef<IList> bottomBarField;

    private static void PreDoTimespeedControls()
    {
        // The mod prevents vanilla code from running here, unless
        // vanillaControlSpeed settings is enabled. Additionally,
        // we need to consider the fact that a timespeed widget
        // may be active somewhere in the mod already, in which
        // case we don't need to do this call either.
        if (needToListenToHotkeys)
            doTimeControlsHotkeysMethod(null);
    }

    private static void PostPotentialSettingsChange()
    {
        if (vanillaControlSpeedField())
        {
            needToListenToHotkeys = false;
            return;
        }

        foreach (var bar in new[] { topBarField(), bottomBarField() })
        {
            if (bar != null)
            {
                foreach (var obj in bar)
                {
                    if (timespeedConfigType.IsInstanceOfType(obj))
                    {
                        needToListenToHotkeys = false;
                        return;
                    }
                }
            }
        }

        needToListenToHotkeys = true;
    }

    #endregion
}