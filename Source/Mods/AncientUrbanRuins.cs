using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using RimWorld.Planet;
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

    #endregion

    #region Main patch

    public AncientUrbanRuins(ModContentPack mod)
    {
        // Mod uses 3 different assemblies, 2 of them use the same namespace.

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
}