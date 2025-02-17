using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Multiplayer.API;
using UnityEngine;
using Verse;

namespace Multiplayer.Compat;

/// <summary>Vanilla Quests Expanded - The Generator by Oskar Potocki, Sarg Bjornson, Taranchuk, Bread mo</summary>
/// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3411401573"/>
/// <see href="https://github.com/Vanilla-Expanded/VanillaQuestsExpanded-TheGenerator"/>
[MpCompatFor("vanillaquestsexpanded.generator")]
public class VanillaQuestsTheGenerator
{
    #region Fields

    // Building_GenetronWithMaintenance
    [MpCompatSyncField("VanillaQuestsExpandedTheGenerator.Building_GenetronWithMaintenance", "maintenanceMultiplier")]
    protected static ISyncField maintenanceMultiplierField;

    // Building_GenetronOverdrive
    [MpCompatSyncField("VanillaQuestsExpandedTheGenerator.Building_GenetronOverdrive", "compRefuelableWithOverdrive", "tuningMultiplier")]
    protected static ISyncField tuningMultiplierField;

    // Command_SetTargetUraniumLevel
    private static SyncType listOfCompRefuelableWithOverdriveSyncType;
    private static AccessTools.FieldRef<Command, IList> commandSetUraniumLevelRefuelablesField;

    // Window_Downgrade
    private static AccessTools.FieldRef<Window, Building> downgradeWindowBuildingField;
    private static AccessTools.FieldRef<Window, ThingDef> downgradeWindowNewBuildingField;

    #endregion

    #region Main Patch

    public VanillaQuestsTheGenerator(ModContentPack mod)
    {
        MpCompatPatchLoader.LoadPatch(this);

        #region Gizmos

        // Gizmos
        {
            var type = AccessTools.TypeByName("VanillaQuestsExpandedTheGenerator.Building_Genetron");
            // Dev: set running time to 100 days (0), set geothermal studied to true (1),
            MpCompat.RegisterLambdaMethod(type, nameof(Building.GetGizmos), 0, 3).SetDebugOnly();
            // Dev: set nuclear studied to true (2), set fuel burned to 1000 (3)
            MpCompat.RegisterLambdaDelegate(type, nameof(Building.GetGizmos), 1, 2).SetDebugOnly();

            // Restart generator (0) and dev: fake a restart (1), set uranium used to 100 (2)
            MpCompat.RegisterLambdaMethod("VanillaQuestsExpandedTheGenerator.Building_GenetronNuclear", 
                    nameof(Building.GetGizmos), 0, 1, 2).Skip(1).SetDebugOnly();

            // Start overdrive (0) and dev: reset overdrive cooldown (1), stop overdrive (2)
            // set overdrive successful for 5 days (3), nuclear meltdown (4)
            MpCompat.RegisterLambdaMethod("VanillaQuestsExpandedTheGenerator.Building_GenetronOverdrive",
                nameof(Building.GetGizmos), 0, 1, 2, 3, 4).Skip(1).SetDebugOnly();

            // Dev: set time on 200% to 100 days, other gizmos (1/2) open dialog
            MpCompat.RegisterLambdaMethod("VanillaQuestsExpandedTheGenerator.Building_GenetronTuning",
                nameof(Building.GetGizmos), 3).SetDebugOnly();

            // Start calibration, lambda (0) calls the method directly
            MP.RegisterSyncMethod(AccessTools.DeclaredMethod(
                "VanillaQuestsExpandedTheGenerator.Building_GenetronWithCalibration:Signal_CalibrationStarted"));

            // Start calibration, lambda (0) calls the method directly
            MP.RegisterSyncMethod(AccessTools.DeclaredMethod(
                "VanillaQuestsExpandedTheGenerator.Building_GenetronWithComponentCalibration:Signal_CalibrateComponentsStarted"));

            // Emergency shutdown (0)
            MpCompat.RegisterLambdaMethod("VanillaQuestsExpandedTheGenerator.Building_GenetronWithEmergencyShutDown",
                nameof(Building.GetGizmos), 0);

            // Start calibration, lambda (0) calls the method directly
            MP.RegisterSyncMethod(AccessTools.DeclaredMethod(
                "VanillaQuestsExpandedTheGenerator.Building_GenetronWithFuelRodCalibration:Signal_FuelRodCalibrationStarted"));

            type = AccessTools.TypeByName("VanillaQuestsExpandedTheGenerator.Building_GenetronWithHazardModes");
            // Toggle safe/hazard mode, lambdas (0/1) call the method directly
            MP.RegisterSyncMethod(type, "Signal_ToggleHazardMode");
            // Dev: set time to hazard mode to 100 days
            MpCompat.RegisterLambdaMethod(type, nameof(Building.GetGizmos), 2).SetDebugOnly();

            // Dev: set maintenance to 10%
            MpCompat.RegisterLambdaMethod("VanillaQuestsExpandedTheGenerator.Building_GenetronWithMaintenance",
                nameof(Building.GetGizmos), 0).SetDebugOnly();

            // Trigger power surge (0) and dev: reset power surge cooldown (1), set power surge usage to 10 (2)
            MpCompat.RegisterLambdaMethod("VanillaQuestsExpandedTheGenerator.Building_GenetronWithPowerSurge",
                nameof(Building.GetGizmos), 0, 1, 2).Skip(1).SetDebugOnly();

            // Trigger steam boost (0) and dev: reset steam boost cooldown (1), set steam boost usage to 10 (2)
            MpCompat.RegisterLambdaMethod("VanillaQuestsExpandedTheGenerator.Building_GenetronWithSteamBoost",
                nameof(Building.GetGizmos), 0, 1, 2).Skip(1).SetDebugOnly();

            // Upgrade generator gizmo, used by every upgradeable building class
            MP.RegisterSyncMethod(AccessTools.DeclaredMethod("VanillaQuestsExpandedTheGenerator.Utils:PlaceDistinctBlueprint"))
                .CancelIfAnyArgNull();

            // Study genetron (0)
            MpCompat.RegisterLambdaMethod("VanillaQuestsExpandedTheGenerator.Building_Genetron_Studiable",
                nameof(Building.GetGizmos), 0);

            type = AccessTools.TypeByName("VanillaQuestsExpandedTheGenerator.Command_SetTargetUraniumLevel");
            MpCompat.RegisterLambdaDelegate(type, nameof(Command.ProcessInput), 1);
            listOfCompRefuelableWithOverdriveSyncType = typeof(List<>)
                .MakeGenericType(AccessTools.TypeByName("VanillaQuestsExpandedTheGenerator.CompRefuelableWithOverdrive"));
            commandSetUraniumLevelRefuelablesField = AccessTools.FieldRefAccess<IList>(type, "refuelables");

            // Dev: trigger traitor event
            MpCompat.RegisterLambdaMethod("VanillaQuestsExpandedTheGenerator.HediffComp_Traitor",
                nameof(HediffComp.CompGetGizmos), 0).SetDebugOnly();
        }

        #endregion

        #region Dialogs

        {
            var type = AccessTools.TypeByName("VanillaQuestsExpandedTheGenerator.Window_Downgrade");
            downgradeWindowBuildingField = AccessTools.FieldRefAccess<Building>(type, "building");
            downgradeWindowNewBuildingField = AccessTools.FieldRefAccess<ThingDef>(type, "newBuilding");
        }

        #endregion

        #region ShouldSpawnMotesAt

        {
            var methods = new[]
            {
                "VanillaQuestsExpandedTheGenerator.Projectile_SpawnsThingAndExplodes:ThrowBlackSmoke",
                "VanillaQuestsExpandedTheGenerator.Utils:ThrowBlackSmoke",
                "VanillaQuestsExpandedTheGenerator.Utils:ThrowExtendedAirPuffUp",
            };

            PatchingUtilities.PatchPushPopRand(methods);
        }

        #endregion
    }

    #endregion

    #region Downgrade Dialog

    [MpCompatTranspiler("VanillaQuestsExpandedTheGenerator.Window_Downgrade", nameof(Window.DoWindowContents))]
    private static IEnumerable<CodeInstruction> ReplaceDowngradeAcceptButton(IEnumerable<CodeInstruction> instr, MethodBase baseMethod)
    {
        var target = AccessTools.DeclaredMethod(typeof(Widgets), nameof(Widgets.ButtonText),
            [typeof(Rect), typeof(string), typeof(bool), typeof(bool), typeof(bool), typeof(TextAnchor?)]);
        var replacement = MpMethodUtil.MethodOf(ReplacedAcceptButton);

        IEnumerable<CodeInstruction> ExtraInstructions(CodeInstruction _) =>
        [
            // Load in "this"
            new CodeInstruction(OpCodes.Ldarg_0),
        ];

        return instr.ReplaceMethod(target, replacement, baseMethod, ExtraInstructions, expectedReplacements: 1, targetText: "OK");
    }

    private static bool ReplacedAcceptButton(Rect rect, string label, bool drawBackground, bool doMouseoverSound, bool active, TextAnchor? overrideTextAnchor, Window instance)
    {
        var result = Widgets.ButtonText(rect, label, drawBackground, doMouseoverSound, active, overrideTextAnchor);
        if (!MP.IsInMultiplayer || !result)
            return result;

        instance.Close();
        SyncedAcceptButton(downgradeWindowBuildingField(instance), downgradeWindowNewBuildingField(instance));

        return false;
    }

    [MpCompatSyncMethod(cancelIfAnyArgNull = true)]
    private static void SyncedAcceptButton(Building building, ThingDef newBuilding)
    {
        // Shouldn't happen, just an extra safety check I guess
        if (building.def != newBuilding)
            GenSpawn.Spawn(ThingMaker.MakeThing(newBuilding), building.PositionHeld, building.Map).SetFaction(building.Faction);
    }

    #endregion

    #region Dialog Field Syncing

    [MpCompatPrefix("VanillaQuestsExpandedTheGenerator.Window_FineTuning", nameof(Window.DoWindowContents))]
    private static void PreFineTuningDialog(Building ___building)
    {
        if (!MP.IsInMultiplayer)
            return;

        MP.WatchBegin();
        maintenanceMultiplierField.Watch(___building);
        tuningMultiplierField.Watch(___building);
    }

    [MpCompatPrefix("VanillaQuestsExpandedTheGenerator.Window_SteamTuning", nameof(Window.DoWindowContents))]
    private static void PreSteamTuningDialog(Building ___building)
    {
        if (!MP.IsInMultiplayer)
            return;

        MP.WatchBegin();
        maintenanceMultiplierField.Watch(___building);
    }

    [MpCompatPrefix("VanillaQuestsExpandedTheGenerator.Window_Tuning", nameof(Window.DoWindowContents))]
    private static void PreTuningDialog(Building ___building)
    {
        if (!MP.IsInMultiplayer)
            return;

        MP.WatchBegin();
        tuningMultiplierField.Watch(___building);
    }

    [MpCompatPostfix("VanillaQuestsExpandedTheGenerator.Window_FineTuning", nameof(Window.DoWindowContents))]
    [MpCompatPostfix("VanillaQuestsExpandedTheGenerator.Window_SteamTuning", nameof(Window.DoWindowContents))]
    [MpCompatPostfix("VanillaQuestsExpandedTheGenerator.Window_Tuning", nameof(Window.DoWindowContents))]
    private static void WatchEnd()
    {
        if (MP.IsInMultiplayer)
            MP.WatchEnd();
    }

    #endregion

    #region Sync Worker

    [MpCompatSyncWorker("VanillaQuestsExpandedTheGenerator.Command_SetTargetUraniumLevel", shouldConstruct = true)]
    private static void SyncCommandSetUraniumLevel(SyncWorker sync, ref Command command)
    {
        if (sync.isWriting)
            sync.Write(commandSetUraniumLevelRefuelablesField(command), listOfCompRefuelableWithOverdriveSyncType);
        else
            commandSetUraniumLevelRefuelablesField(command) = sync.Read<IList>(listOfCompRefuelableWithOverdriveSyncType);
    }

    #endregion
}