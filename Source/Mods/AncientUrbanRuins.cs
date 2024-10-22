using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Multiplayer.Compat;

/// <summary>Ancient urban ruins by MO</summary>
/// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3316062206"/>
[MpCompatFor("XMB.AncientUrbanrUins.MO")]
public class AncientUrbanRuins
{
    #region Fields

    // GameComponent_AncientMarket
    private static Type ancientMarketGameCompType;
    private static FastInvokeHandler ancientMarketGameCompGetScheduleMethod;
    private static AccessTools.FieldRef<GameComponent, IDictionary> ancientMarketGameCompSchedulesField;
    // LevelSchedule
    private static AccessTools.FieldRef<object, IList> levelScheduleAllowedLevelsField;
    private static AccessTools.FieldRef<object, List<bool>> levelScheduleTimeScheduleField;
    // MapParent_Custom
    private static AccessTools.FieldRef<PocketMapParent, MapPortal> customMapEntranceField;

    // Tab_LevelPower
    private static FastInvokeHandler levelPowerTabCompGetter;
    // CompPowerPlantLevel
    private static Type powerPlantLevelCompType;
    private static FastInvokeHandler powerPlantLevelCompLinkedCompGetter;
    private static AccessTools.FieldRef<CompPowerPlant, float> powerPlantLevelCompTargetOutputLevelField;
    [MpCompatSyncField("AncientMarket_Libraray.CompPowerPlantLevel", "name")]
    protected static ISyncField powerPlantLevelCompNameSyncField;
    [MpCompatSyncField("AncientMarket_Libraray.CompPowerPlantLevel", "outputMode")]
    protected static ISyncField powerPlantLevelCompOutputModeSyncField;
    [MpCompatSyncField("AncientMarket_Libraray.CompPowerPlantLevel", "targetPowerOutput")]
    protected static ISyncField powerPlantLevelTargetOutputLevelSyncField;
    [MpCompatSyncField("AncientMarket_Libraray.CompPowerPlantLevel", "comp")]
    protected static ISyncField powerPlantLevelCompLinkedThingSyncField;
    [MpCompatSyncField("AncientMarket_Libraray.CompPowerPlantLevel", "linked")]
    protected static ISyncField powerPlantLevelCompLinkedCompSyncField;

    // Tab_LevelTransmit
    private static FastInvokeHandler levelTransmitTabReceiverGetter;
    // Building_Transmit
    [MpCompatSyncField("AncientMarket_Libraray.Building_Receive", "name")]
    protected static ISyncField buildingTransmitNameSyncField;

    #endregion

    #region Main patch

    public AncientUrbanRuins(ModContentPack mod)
    {
        // Mod uses several 6 different assemblies, 2 of them use the same namespace.
        // It seems the mod quite often but increments a number in the assemblies
        // name rather than keeping the same name - for example, currently there's
        // an assembly called "AncientMarket_Libraray(66).dll".
        // They seem to ocassionally restore the name to a one without a number.

        MpCompatPatchLoader.LoadPatch(this);
        MpSyncWorkers.Requires<PocketMapParent>();

        #region RNG

        {
            var methods = new[]
            {
                "AncientMarket_Libraray.ACM_RandomUtility:ReplaceWallAndBreakThem",
                "AncientMarket_Libraray.ACM_SketchResolver_ACM_Resturant:ResolveInt",
                "AncientMarket_Libraray.ACM_SketchResolver_BuildingRoom:ResolveInt",
                "AncientMarket_Libraray.ACM_SketchResolver_BuildingRoomWithNoForklift:ResolveInt",
                "AncientMarket_Libraray.ACM_SketchResolver_ContainerRoom:ResolveInt",
                "AncientMarket_Libraray.ACM_SketchResolver_HoboRoom:ResolveInt",
                "AncientMarket_Libraray.ACM_SketchResolver_StoreRoom:ResolveInt",
                "AncientMarket_Libraray.ACM_SketchResolver_UnderRoom:ResolveInt",
                "AncientMarket_Libraray.ComplexThreatWorker_HangingPirates:SpawnThreatPawns",
            };

            PatchingUtilities.PatchUnityRand(methods);
        }

        #endregion

        #region Input

        {
            // Start trade
            MpCompat.RegisterLambdaDelegate("AncientMarket_Libraray.BuildingTrader", nameof(Thing.GetFloatMenuOptions), 0);
            // Start dialogue (seems unused/related feature is unfinished)
            MpCompat.RegisterLambdaDelegate("AncientMarket_Libraray.CompDialogable", nameof(ThingComp.CompFloatMenuOptions), 0);

            // Destroy site
            LongEventHandler.ExecuteWhenFinished(() =>
                MpCompat.RegisterLambdaMethod("AncientMarket_Libraray.CustomSite", nameof(WorldObject.GetGizmos), 1));
            // Toggle plan to fill the portal
            MpCompat.RegisterLambdaMethod("AncientMarket_Libraray.CompFillPortal", nameof(ThingComp.CompGetGizmosExtra), 0);
        }

        #endregion

        #region Permitted floors timetable

        {
            // Prepare stuff
            var type = ancientMarketGameCompType = AccessTools.TypeByName("AncientMarket_Libraray.GameComponent_AncientMarket");
            ancientMarketGameCompGetScheduleMethod = MethodInvoker.GetHandler(AccessTools.DeclaredMethod(type, "GetSchedule"));
            ancientMarketGameCompSchedulesField = AccessTools.FieldRefAccess<IDictionary>(type, "schedules");

            type = AccessTools.TypeByName("AncientMarket_Libraray.LevelSchedule");
            levelScheduleAllowedLevelsField = AccessTools.FieldRefAccess<IList>(type, "allowedLevels");
            levelScheduleTimeScheduleField = AccessTools.FieldRefAccess<List<bool>>(type, "timeSchedule");

            customMapEntranceField = AccessTools.FieldRefAccess<MapPortal>("AncientMarket_Libraray.MapParent_Custom:entrance");

            // Add to allowed (2), remove from allowed (4)
            MpCompat.RegisterLambdaDelegate(
                "AncientMarket_Libraray.Window_AllowLevel",
                nameof(Window.DoWindowContents),
                ["schedule"], // Skip x and y, syncing them is not needed - they're only used for UI
                2, 4);
        }

        #endregion

        #region ITab

        {
            var thingAsIdSerializer = Serializer.New((Thing t) => t.thingIDNumber,
                id => MP.TryGetThingById(id, out var thing) ? thing : null);

            // Cross-map power transmit tab
            var type = AccessTools.TypeByName("AncientMarket_Libraray.Tab_LevelPower");
            levelPowerTabCompGetter = MethodInvoker.GetHandler(
                AccessTools.DeclaredPropertyGetter(type, "Comp"));

            // Link 2 power transmitters.
            // They are most likely on separate maps, so we need to transform
            // the argument (since we can't transform the selected things).
            MpCompat.RegisterLambdaMethod(type, nameof(ITab.FillTab), 1)[0]
                .TransformArgument(0, thingAsIdSerializer)
                .SetContext(SyncContext.MapSelected)
                .CancelIfAnyArgNull();

            type = powerPlantLevelCompType = AccessTools.TypeByName("AncientMarket_Libraray.CompPowerPlantLevel");
            powerPlantLevelCompLinkedCompGetter = MethodInvoker.GetHandler(
                AccessTools.DeclaredPropertyGetter(type, "LinkedComp"));
            powerPlantLevelCompTargetOutputLevelField = AccessTools.FieldRefAccess<float>(type, "targetPowerOutput");

            // Cross-map item transmit tab
            type = AccessTools.TypeByName("AncientMarket_Libraray.Tab_LevelTransmit");
            levelTransmitTabReceiverGetter = MethodInvoker.GetHandler(
                AccessTools.DeclaredPropertyGetter(type, "receive"));

            // Select a target receiver for a transmitter.
            // They are most likely on separate maps, so we need to transform
            // the argument (since we can't transform the selected things).
            MpCompat.RegisterLambdaMethod(type, nameof(ITab.FillTab), 1)[0]
                .TransformArgument(0, thingAsIdSerializer)
                .SetContext(SyncContext.MapSelected)
                .CancelIfAnyArgNull();
        }

        #endregion
    }

    #endregion

    #region Destroy site confirmation dialog

    // If multiple players have the dialog open, close the dialog if one of
    // the players confirmed it as there's no point in having it open.
    // For some reason, this mod uses 2 different confirmation dialogs for
    // different sites, the vanilla confirmation dialog and their own custom one.

    [MpCompatPrefix("AncientMarket_Libraray.Dialog_Confirm", "DoWindowContents")]
    private static void CloseDialogIfSiteDestroyed(Window __instance, Site ___Site)
    {
        if (___Site == null || ___Site.Destroyed)
            __instance.Close();
    }

    // AncientMarket_Libraray.Dialog_Confirm calls WorldObject.Destroy. We could
    // sync that method, but I'd prefer not to sync Vanilla methods

    [MpCompatTranspiler("AncientMarket_Libraray.Dialog_Confirm", "DoWindowContents")]
    private static IEnumerable<CodeInstruction> ReplaceDestroyWithSyncedCall(IEnumerable<CodeInstruction> instr)
    {
        var target = AccessTools.DeclaredMethod(typeof(WorldObject), nameof(WorldObject.Destroy));
        var replacement = MpMethodUtil.MethodOf(SyncedDestroySite);

        foreach (var ci in instr)
        {
            if (ci.Calls(target))
            {
                ci.opcode = OpCodes.Call;
                ci.operand = replacement;
            }

            yield return ci;
        }
    }

    [MpCompatSyncMethod]
    private static void SyncedDestroySite(WorldObject site)
    {
        // Make sure it's not already destroyed to prevent errors
        if (!site.Destroyed)
            site.Destroy();
    }

    #endregion

    #region Permitted floors timetable patches and syncing

    [MpCompatSyncWorker("AncientMarket_Libraray.LevelSchedule")]
    private static void SyncLevelSchedule(SyncWorker sync, ref object schedule)
    {
        var comp = Current.Game.GetComponent(ancientMarketGameCompType);

        if (sync.isWriting)
        {
            if (schedule == null)
            {
                sync.Write<Pawn>(null);
                return;
            }

            // Get the dictionary of all schedules and pawns and iterate over them
            var list = ancientMarketGameCompSchedulesField(comp);
            Pawn pawn = null;
            foreach (DictionaryEntry value in list)
            {
                // If the value is the schedule we're syncing, sync the pawn key.
                if (value.Value == schedule)
                {
                    pawn = value.Key as Pawn;
                    break;
                }
            }

            sync.Write(pawn);
        }
        else
        {
            var pawn = sync.Read<Pawn>();
            // Will create the schedule if null here, as it may be created in interface.
            if (pawn != null)
                schedule = ancientMarketGameCompGetScheduleMethod(comp, pawn);
        }
    }

    [MpCompatPrefix("AncientMarket_Libraray.Window_AllowLevel", nameof(Window.DoWindowContents), 2)]
    private static bool PreMapAddedToSchedule(PocketMapParent m, object ___schedule)
    {
        if (!MP.IsInMultiplayer || !MP.IsExecutingSyncCommand)
            return true;
        // Hopefully shouldn't happen
        if (m == null || ___schedule == null)
            return false;

        var allowedLevels = levelScheduleAllowedLevelsField(___schedule);
        var entrance = customMapEntranceField(m);

        // If the allowed levels already contains the entrance, cancel execution.
        return !allowedLevels.Contains(entrance);
    }

    [MpCompatSyncMethod(cancelIfAnyArgNull = true)]
    private static void SyncedSetTimeAssignment(Pawn pawn, int hour, bool allow)
    {
        // No need to check if hour is correct, as it should be.
        var comp = Current.Game.GetComponent(ancientMarketGameCompType);
        var schedule = ancientMarketGameCompGetScheduleMethod(comp, pawn);
        levelScheduleTimeScheduleField(schedule)[hour] = allow;
    }

    private static void ReplacedSetTimeSchedule(List<bool> schedule, int hour, bool allow, Pawn pawn)
    {
        // Ignore execution if there would be no change, prevents unnecessary syncing.
        if (schedule[hour] != allow)
            SyncedSetTimeAssignment(pawn, hour, allow);
    }

    [MpCompatTranspiler("AncientMarket_Libraray.PawnColumnWorker_LevelTimetable", "DoTimeAssignment")]
    private static IEnumerable<CodeInstruction> ReplaceIndexerSetterWithSyncedTimetableChange(IEnumerable<CodeInstruction> instr, MethodBase baseMethod)
    {
        // The method calls (List<bool>)[int] = bool. We need to sync this call, which happens
        // after a check if the cell was clicked. We replace the call to this setter, replacing
        // it with our own method. We also need to get a pawn for syncing, as we can't just
        // sync List<bool> here - we need to sync the Pawn or LevelSchedule.

        var target = AccessTools.DeclaredIndexerSetter(typeof(List<>).MakeGenericType(typeof(bool)), [typeof(int)]);
        var replacement = MpMethodUtil.MethodOf(ReplacedSetTimeSchedule);
        var replacedCount = 0;

        foreach (var ci in instr)
        {
            if (ci.Calls(target))
            {
                // Push the Pawn argument onto the stack
                yield return new CodeInstruction(OpCodes.Ldarg_2);

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

    #region Power transfer ITab

    [MpCompatPrefix("AncientMarket_Libraray.Tab_LevelPower", nameof(ITab.FillTab))]
    private static void PreLevelPowerITabFillTab(ITab __instance, ref bool __state)
    {
        if (!MP.IsInMultiplayer)
            return;

        var comp = levelPowerTabCompGetter(__instance);
        if (comp == null)
            return;

        __state = true;
        MP.WatchBegin();
        powerPlantLevelCompNameSyncField.Watch(comp);
        powerPlantLevelCompOutputModeSyncField.Watch(comp);
        powerPlantLevelTargetOutputLevelSyncField.Watch(comp);
        // Watch the linked thing/comp (and do the same for the linked thing,
        // if there's one). They'll ever only be set to null in here.
        powerPlantLevelCompLinkedThingSyncField.Watch(comp);
        powerPlantLevelCompLinkedCompSyncField.Watch(comp);

        var linkedComp = powerPlantLevelCompLinkedCompGetter(comp);
        if (linkedComp != null)
        {
            powerPlantLevelCompLinkedThingSyncField.Watch(linkedComp);
            powerPlantLevelCompLinkedCompSyncField.Watch(linkedComp);
        }
    }

    [MpCompatFinalizer("AncientMarket_Libraray.Tab_LevelPower", nameof(ITab.FillTab))]
    private static void PostLevelPowerITabFillTab(bool __state)
    {
        if (__state)
            MP.WatchEnd();
    }

    [MpCompatSyncMethod(cancelIfAnyArgNull = true)]
    private static void SyncedAcceptPowerChange(CompPowerPlant comp)
        => comp.PowerOutput = -powerPlantLevelCompTargetOutputLevelField(comp);

    private static bool ReplacedAcceptPowerChangeButton(Rect rect, string label, bool drawBackground, bool doMouseoverSound, bool active, TextAnchor? overrideTextAnchor)
    {
        var result = Widgets.ButtonText(rect, label, drawBackground, doMouseoverSound, active, overrideTextAnchor);
        if (!MP.IsInMultiplayer || !result)
            return result;

        // Shouldn't happen unless mod makes some changes
        if (Find.Selector.SingleSelectedThing is not ThingWithComps target)
            return false;

        // We could probably just call:
        // Find.Selector.SingleSelectedThing.TryGetComp<CompPowerPlant>().
        // This should handle situations where there's multiple power plant
        // comps, event though it's not really needed here at the moment.
        foreach (var comp in target.AllComps)
        {
            if (comp is CompPowerPlant powerPlantComp && powerPlantLevelCompType.IsInstanceOfType(powerPlantComp))
            {
                SyncedAcceptPowerChange(powerPlantComp);
                break;
            }
        }

        return false;
    }

    [MpCompatTranspiler("AncientMarket_Libraray.Tab_LevelPower", nameof(ITab.FillTab))]
    private static IEnumerable<CodeInstruction> ReplaceApplyButton(IEnumerable<CodeInstruction> instr, MethodBase baseMethod)
    {
        var target = AccessTools.DeclaredMethod(typeof(Widgets), nameof(Widgets.ButtonText),
            [typeof(Rect), typeof(string), typeof(bool), typeof(bool), typeof(bool), typeof(TextAnchor?)]);
        var replacement = MpMethodUtil.MethodOf(ReplacedAcceptPowerChangeButton);
    
        return instr.ReplaceMethod(target, replacement, baseMethod, targetText: "Apply", expectedReplacements: 1);
    }

    #endregion

    #region Resource elevator ITab

    [MpCompatPrefix("AncientMarket_Libraray.Tab_LevelTransmit", nameof(ITab.FillTab))]
    private static void PreLevelTransmitITabFillTab(ITab __instance, ref bool __state)
    {
        if (!MP.IsInMultiplayer)
            return;

        var selThing = levelTransmitTabReceiverGetter(__instance);
        if (selThing == null)
            return;

        // If a receiver is selected then watch changes to its name
        __state = true;
        MP.WatchBegin();
        buildingTransmitNameSyncField.Watch(selThing);
    }

    [MpCompatFinalizer("AncientMarket_Libraray.Tab_LevelTransmit", nameof(ITab.FillTab))]
    private static void PostLevelTransmitITabFillTab(bool __state)
    {
        if (__state)
            MP.WatchEnd();
    }

    #endregion

    #region Shared

    [MpCompatSyncWorker("AncientMarket_Libraray.Tab_LevelPower", shouldConstruct = true)]
    [MpCompatSyncWorker("AncientMarket_Libraray.Tab_LevelTransmit", shouldConstruct = true)]
    private static void NoSync(SyncWorker sync, ref ITab tab)
    {
    }

    #endregion
}