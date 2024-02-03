using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using JetBrains.Annotations;
using Multiplayer.API;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Multiplayer.Compat;

/// <summary>Vanilla Factions Expanded - Insectoids by Oskar Potocki, Sarg Bjornson, Kikohi</summary>
/// <see href="https://github.com/Vanilla-Expanded/VanillaFactionsExpanded-Insectoids"/>
/// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2149755445"/>
[MpCompatFor("OskarPotocki.VFE.Insectoid")]
public class VanillaFactionsInsectoid
{
    #region Fields

    // CompTeleporter
    private static Type teleporterCompType;

    // Command_LoadTeleporter
    private static Type teleporterCommandType;
    private static AccessTools.FieldRef<Command, ThingComp> commandCompField;
    private static FastInvokeHandler commandChoseWorldTargetMethod;
    private static FastInvokeHandler commandTargetingLabelGetterMethod;
    private static FastInvokeHandler commandActionMakeDropPodInfoMethod;

    // <>c__DisplayClass13_0
    private static AccessTools.FieldRef<object, Command> innerClassThisField; // Ref to Command_LoadTeleporter

    // <>c__DisplayClass13_1
    private static AccessTools.FieldRef<object, object> innerClassLocalsField; // Ref to <>c__DisplayClass13_0
    private static AccessTools.FieldRef<object, MapParent> innerClassMapParentField;

    // Dialog_LoadTeleporter
    private static Type teleporterDialogType;
    private static AccessTools.FieldRef<Window, List<TransferableOneWay>> dialogTransferablesField;
    private static AccessTools.FieldRef<Window, TransferableOneWayWidget> dialogPawnsTransferField;
    private static AccessTools.FieldRef<Window, TransferableOneWayWidget> dialogItemsTransferField;
    private static FastInvokeHandler dialogCalculateAndRecacheTransferablesMethod;
    private static FastInvokeHandler dialogCountToTransferChangedMethod;
    private static FastInvokeHandler dialogLoadInstantlyMethod;
    private static FastInvokeHandler dialogMassCapacityGetter;
    private static FastInvokeHandler dialogMassUsageGetter;

    #endregion

    #region Main patch

    public VanillaFactionsInsectoid(ModContentPack mod)
    {
        LongEventHandler.ExecuteWhenFinished(() =>
        {
            MpCompatPatchLoader.LoadPatch(this);
            MpCompatPatchLoader.LoadPatch<LoadTeleporterSession>();
        });

        #region Gizmos

        {
            var type = AccessTools.TypeByName("InsectoidBioengineering.Building_BioengineeringIncubator");
            // Start insertion (0), remove all genes (1), cancel all jobs (2), engage/start (3)
            MpCompat.RegisterLambdaMethod(type, "GetGizmos", 0, 1, 2, 3);

            type = AccessTools.TypeByName("InsectoidBioengineering.GenomeListClass");
            // Select none (0), or a specific genome (2), handles all slots
            MpCompat.RegisterLambdaDelegate(type, "Process", 0, 2);
        }

        #endregion

        #region RNG

        {
            var constructors = new[]
            {
                "InsectoidBioengineering.Building_BioengineeringIncubator",
                //"VFEI.CompFilthProducer",
            };

            PatchingUtilities.PatchSystemRandCtor(constructors, false);

            var methods = new[]
            {
                "VFEI.CompTargetEffect_Tame:RandomNumber",
            };

            PatchingUtilities.PatchSystemRand(methods);
        }

        #endregion

        #region Session

        {
            // Teleporter comp
            teleporterCompType = AccessTools.TypeByName("VFEI.CompTeleporter");

            // Teleporter command
            var type = teleporterCommandType = AccessTools.TypeByName("VFEI.Command_LoadTeleporter");
            commandCompField = AccessTools.FieldRefAccess<ThingComp>(type, "teleporterComp");
            commandChoseWorldTargetMethod = MethodInvoker.GetHandler(AccessTools.DeclaredMethod(type, "ChoseWorldTarget"));
            commandTargetingLabelGetterMethod = MethodInvoker.GetHandler(AccessTools.DeclaredMethod(type, "TargetingLabelGetter"));
            commandActionMakeDropPodInfoMethod = MethodInvoker.GetHandler(AccessTools.DeclaredMethod(type, "ActionMakeDropPodInfo"));

            // Transform the first argument as null. Those are dummy
            // ActiveDropPod/ActiveDropPodInfo objects that we need
            // to create them in pre invoke. Syncing them is pointless.
            MP.RegisterSyncMethod(type, "ActionNone")
                .SetPreInvoke(PreTeleporterCommandNone)
                .TransformArgument(0, Serializer.SimpleReader<ActiveDropPod>(() => null), true);
            MP.RegisterSyncMethod(type, "ActionAttackSettlement")
                .SetPreInvoke(PreTeleporterCommandAttackSettlement)
                .TransformArgument(0, Serializer.SimpleReader<ActiveDropPodInfo>(() => null), true);
            foreach (var methodName in new[] { "ActionFormCaravan", "ActionGiveGift", "ActionGiveToCaravan", "ActionVisiteSite", })
                MP.RegisterSyncMethod(type, methodName).SetPreInvoke(PreTeleporterCommandOtherAction);

            // Target specific cell on map, sadly it's gonna be messy as it doesn't have a unique method.
            var method = MpMethodUtil.GetLambda(type, "GetTeleporterFloatMenuOptionsAt", lambdaOrdinal: 10);
            MP.RegisterSyncDelegate(type, method.DeclaringType!.Name, method.Name, null)
                .SetPreInvoke(PreTeleporterCommandTargetSpecificCell)
                .SetPostInvoke(PostTeleporterCommandTargetSpecificCell);

            // Get field refs for the child compiler generated type
            var field = AccessTools.DeclaredField(method.DeclaringType, "CS$<>8__locals1");
            innerClassLocalsField = AccessTools.FieldRefAccess<object, object>(field);
            innerClassMapParentField = AccessTools.FieldRefAccess<MapParent>(method.DeclaringType, "mapParent");
            // Get field refs for the parent compiler generated type
            innerClassThisField = AccessTools.FieldRefAccess<Command>(field.FieldType, "<>4__this");

            // Teleporter dialog
            type = teleporterDialogType = AccessTools.TypeByName("VFEI.Dialog_LoadTeleporter");
            dialogTransferablesField = AccessTools.FieldRefAccess<List<TransferableOneWay>>(type, "transferables");
            dialogPawnsTransferField = AccessTools.FieldRefAccess<TransferableOneWayWidget>(type, "pawnsTransfer");
            dialogItemsTransferField = AccessTools.FieldRefAccess<TransferableOneWayWidget>(type, "itemsTransfer");
            dialogCalculateAndRecacheTransferablesMethod = MethodInvoker.GetHandler(AccessTools.DeclaredMethod(type, "CalculateAndRecacheTransferables"));
            dialogCountToTransferChangedMethod = MethodInvoker.GetHandler(AccessTools.DeclaredMethod(type, "CountToTransferChanged"));
            dialogLoadInstantlyMethod = MethodInvoker.GetHandler(AccessTools.DeclaredMethod(type, "LoadInstantly"));
            dialogMassCapacityGetter = MethodInvoker.GetHandler(AccessTools.DeclaredPropertyGetter(type, "MassCapacity"));
            dialogMassUsageGetter = MethodInvoker.GetHandler(AccessTools.DeclaredPropertyGetter(type, "MassUsage"));
        }

        #endregion
    }

    #endregion

    #region Session

    #region Session Class

    public class LoadTeleporterSession : ExposableSession, ISessionWithTransferables, ISessionWithCreationRestrictions, IThingHolder
    {
        public static LoadTeleporterSession drawingSession;

        public bool uiDirty = false;
        public bool thingOwnerDirty = false;
        public ThingWithComps parent;
        public ThingComp teleporterComp;
        public List<TransferableOneWay> transferables;
        // Reusable dummy ThingOwner, just to avoid making a new one every time we need it.
        public ThingOwner<Thing> dummyThingOwner = new();

        public override Map Map => teleporterComp.parent.MapHeld;

        [UsedImplicitly]
        public LoadTeleporterSession(Map map) : base(map)
        {
        }

        public LoadTeleporterSession(ThingComp comp) : base(null)
        {
            teleporterComp = comp;
            parent = comp?.parent;

            AddItems();
        }

        public void AddItems()
        {
            var dialog = Activator.CreateInstance(teleporterDialogType, parent.MapHeld, teleporterComp) as Window;

            uiDirty = thingOwnerDirty = true;
            dialogCalculateAndRecacheTransferablesMethod(dialog);
            transferables = dialogTransferablesField(dialog);
        }

        public bool TryRestoreCompFromParent()
        {
            if (parent == null)
            {
                Log.Error("Teleporter comp's parent is null");
                Remove();
                return false;
            }

            teleporterComp = parent.AllComps.FirstOrDefault(c => teleporterCompType.IsInstanceOfType(c));
            if (teleporterComp == null)
            {
                Log.Error($"Teleporter comp is null for {parent}");
                Remove();
                return false;
            }

            return true;
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Collections.Look(ref transferables, "transferables", LookMode.Deep);

            // Make sure the parent isn't null, just in case.
            if (Scribe.mode == LoadSaveMode.Saving && parent == null)
                parent = teleporterComp?.parent;
            Scribe_References.Look(ref parent, "teleporter");
            // Get the comp from the parent if possible.
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                TryRestoreCompFromParent();
        }

        public override bool IsCurrentlyPausing(Map map) => map == Map;

        public override FloatMenuOption GetBlockingWindowOptions(ColonistBar.Entry entry)
        {
            return new FloatMenuOption("MpInsectoidLoadTeleporterSession".Translate(), () =>
            {
                SwitchToMapOrWorld(Map);
                OpenWindow();
            });
        }

        public Transferable GetTransferableByThingId(int thingId)
            => transferables.Find(tr => tr.things.Any(t => t.thingIDNumber == thingId));

        public void Notify_CountChanged(Transferable tr) => uiDirty = true;

        public bool CanExistWith(Session other) => other is not LoadTeleporterSession;

        private void OpenWindow(bool sound = true)
        {
            Log.Message($"teleporter session {sessionId}");

            var dialog = PrepareDummyDialog();
            if (!sound)
                dialog.soundAppear = null;

            // Since we cancel the CalculateAndRecacheTransferables call on PostOpen, we need to initialize the widgets ourselves.
            // Init pawns
            dialogPawnsTransferField(dialog) = new TransferableOneWayWidget(
                null,
                null,
                null,
                "FormCaravanColonyThingCountTip".Translate(),
                true,
                IgnorePawnsInventoryMode.IgnoreIfAssignedToUnload,
                true,
                () => (float)dialogMassCapacityGetter(dialog) - (float)dialogMassUsageGetter(dialog),
                0f,
                false,
                Map.Tile,
                true,
                true,
                true,
                false,
                false,
                true);
            CaravanUIUtility.AddPawnsSections(dialogPawnsTransferField(dialog), transferables);
            // Init items
            dialogItemsTransferField(dialog) = new TransferableOneWayWidget(
                transferables.Where(t => t.ThingDef.category != ThingCategory.Pawn),
                null,
                null,
                "FormCaravanColonyThingCountTip".Translate(),
                true,
                IgnorePawnsInventoryMode.IgnoreIfAssignedToUnload,
                true,
                () => (float)dialogMassCapacityGetter(dialog) - (float)dialogMassUsageGetter(dialog),
                0f,
                false,
                Map.Tile,
                true,
                false,
                false,
                false,
                true,
                false,
                true);

            Find.WindowStack.Add(dialog);
            uiDirty = thingOwnerDirty = true;
        }

        public Window PrepareDummyDialog()
        {
            var dialog = Activator.CreateInstance(teleporterDialogType, parent.MapHeld, teleporterComp) as Window;
            dialogTransferablesField(dialog) = transferables;

            return dialog;
        }

        [MpCompatSyncMethod]
        public static void CreateLoadTeleporterDialog(ThingComp comp)
        {
            var map = comp?.parent?.MapHeld;
            if (map == null)
                return;

            var manager = MP.GetLocalSessionManager(map);
            var session = manager.GetOrAddSession(new LoadTeleporterSession(comp));

            if (session == null)
                Log.Error($"Couldn't get or create {nameof(LoadTeleporterSession)}");
            else if (MP.IsExecutingSyncCommandIssuedBySelf)
                session.OpenWindow();
        }

        public static bool TryOpenLoadTeleporterDialog(ThingComp comp)
        {
            var map = comp?.parent?.MapHeld;
            if (map == null)
                return false;

            var session = MP.GetLocalSessionManager(map).GetFirstOfType<LoadTeleporterSession>();
            if (session == null)
                return false;

            session.OpenWindow();
            return true;
        }

        [MpCompatSyncMethod]
        public void Reset()
        {
            SoundDefOf.Tick_Low.PlayOneShotOnCamera();
            transferables.ForEach(t => t.CountToTransfer = 0);
            uiDirty = thingOwnerDirty = true;
        }

        [MpCompatSyncMethod]
        public void Remove() => MP.GetLocalSessionManager(Map).RemoveSession(this);

        // IThingHolder implementation, used as a replacement
        //  for using the comp itself for generating float menu.
        public void GetChildHolders(List<IThingHolder> outChildren)
            => ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());

        public ThingOwner GetDirectlyHeldThings() => dummyThingOwner;

        public IThingHolder ParentHolder => null;
    }

    #endregion

    #region Dialog Patches

    public static void SetCurrentSessionState(LoadTeleporterSession session)
    {
        LoadTeleporterSession.drawingSession = session;
        MP.SetCurrentSessionWithTransferables(session);
    }

    [MpCompatPrefix("VFEI.Dialog_LoadTeleporter", nameof(Window.DoWindowContents))]
    private static void PreDrawTeleporter(Window __instance, Map ___map, out bool __state)
    {
        __state = false;
        if (!MP.IsInMultiplayer)
            return;

        var session = MP.GetLocalSessionManager(___map).GetFirstOfType<LoadTeleporterSession>();
        if (session == null)
        {
            __instance.Close();
            return;
        }

        if (session.teleporterComp == null)
        {
            if (session.parent == null)
            {
                Log.Error("Teleporter comp and its parent are null");
                __instance.Close();
                return;
            }

            if (!session.TryRestoreCompFromParent())
                return;
        }

        if (session.uiDirty)
        {
            dialogCountToTransferChangedMethod(__instance);
            session.uiDirty = false;
        }

        __state = true;
        SetCurrentSessionState(session);
    }

    [MpCompatFinalizer("VFEI.Dialog_LoadTeleporter", nameof(Window.DoWindowContents))]
    private static void PostDrawTeleporter(Window __instance, Rect __0, bool __state)
    {
        if (!__state)
            return;

        using (new TextBlock(GameFont.Tiny))
        {
            // TODO: Switch to the MP translation once it's included in the mod
            var switchToMapText = "MpCompatSwitchToMap".Translate();
            var width = switchToMapText.GetWidthCached() + 25;

            if (Widgets.ButtonText(new Rect(__0.xMax - width, 5, width, 24), switchToMapText))
                __instance.Close();
        }
    }

    [MpCompatFinalizer("VFEI.Dialog_LoadTeleporter", "DoWindowContents")]
    private static void FinalizeDrawTeleporter(bool __state)
    {
        if (__state)
            SetCurrentSessionState(null);
    }

    private static void OnlyCalculateAndRecacheTransferablesInSp(Window instance)
    {
        // We don't want to do this call in MP, handled by session
        if (!MP.IsInMultiplayer)
            dialogCalculateAndRecacheTransferablesMethod(instance);
    }

    [MpCompatTranspiler("VFEI.Dialog_LoadTeleporter", "PostOpen")]
    private static IEnumerable<CodeInstruction> ReplaceCalculateAndRecacheTransferables(IEnumerable<CodeInstruction> instr, MethodBase baseMethod)
    {
        var target = AccessTools.DeclaredMethod("VFEI.Dialog_LoadTeleporter:CalculateAndRecacheTransferables");
        var replacement = MpMethodUtil.MethodOf(OnlyCalculateAndRecacheTransferablesInSp);
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
            Log.Warning($"Patched incorrect number of Dialog_LoadTeleporter.CalculateAndRecacheTransferables calls (patched {replacedCount}, expected {expected}) for method {name}");
        }
    }

    [MpCompatPrefix("VFEI.Dialog_LoadTeleporter", "LoadInstantly")]
    private static bool PreLoadInstantly(Window __instance)
    {
        if (LoadTeleporterSession.drawingSession == null || !MP.InInterface)
            return true;

        // The mod normally opens the dialog and starts the targeting at the same time.
        // We need to handle it in an MP-safe manner by picking the target independent of the dialog opening.
        CameraJumper.TryJump(CameraJumper.GetWorldTarget(LoadTeleporterSession.drawingSession.parent));
        Find.WorldSelector.ClearSelection();
        var command = Activator.CreateInstance(teleporterCommandType) as Command;
        var comp = LoadTeleporterSession.drawingSession.teleporterComp;
        commandCompField(command) = comp;
        Find.WorldTargeter.BeginTargeting(
            t => (bool)commandChoseWorldTargetMethod(command, t),
            true,
            extraLabelGetter: t => (string)commandTargetingLabelGetterMethod(command, t, comp));

        return false;
    }

    [MpCompatPrefix("VFEI.Dialog_LoadTeleporter", "CalculateAndRecacheTransferables")]
    private static bool PreCalculateAndRecacheTransferables(Window __instance)
    {
        if (LoadTeleporterSession.drawingSession == null)
            return true;

        LoadTeleporterSession.drawingSession.Reset();
        return false;
    }

    private static bool ReplacedCloseButton(Rect rect, string label, bool drawBackground = true, bool doMouseoverSound = true, bool active = true, TextAnchor? overrideTextAnchor = null)
    {
        bool DoButton() => Widgets.ButtonText(rect, label, drawBackground, doMouseoverSound, active, overrideTextAnchor);

        if (LoadTeleporterSession.drawingSession == null)
            return DoButton();

        var color = GUI.color;
        try
        {
            // Red button like in MP
            GUI.color = new Color(1f, 0.3f, 0.35f);

            // If the button was pressed sync removing the dialog
            if (DoButton())
                LoadTeleporterSession.drawingSession.Remove();
        }
        finally
        {
            GUI.color = color;
        }

        return false;
    }

    [MpCompatTranspiler("VFEI.Dialog_LoadTeleporter", "DoBottomButtons")]
    private static IEnumerable<CodeInstruction> ReplaceCloseButton(IEnumerable<CodeInstruction> instr, MethodBase baseMethod)
    {
        var target = MpMethodUtil.MethodOf(new Func<Rect, string, bool, bool, bool, TextAnchor?, bool>(Widgets.ButtonText));
        var replacement = MpMethodUtil.MethodOf(ReplacedCloseButton);
        var replacedCount = 0;
        var replaceCall = false;

        foreach (var ci in instr)
        {
            if (!replaceCall)
            {
                if (ci.opcode == OpCodes.Ldstr && ci.operand is "CancelButton")
                    replaceCall = true;
            }
            else if (ci.Calls(target))
            {
                ci.opcode = OpCodes.Call;
                ci.operand = replacement;

                replacedCount++;
                replaceCall = false;
            }

            yield return ci;
        }

        const int expected = 1;
        if (replacedCount != expected)
        {
            var name = (baseMethod.DeclaringType?.Namespace).NullOrEmpty() ? baseMethod.Name : $"{baseMethod.DeclaringType!.Name}:{baseMethod.Name}";
            Log.Warning($"Patched incorrect number of Widgets.ButtonText calls (patched {replacedCount}, expected {expected}) for method {name}");
        }
    }

    #endregion

    #region Command Patches

    [MpCompatPrefix("VFEI.Command_LoadTeleporter", nameof(Command.ProcessInput))]
    private static bool PreOpenDialog(Command __instance, ThingComp ___teleporterComp)
    {
        if (!MP.IsInMultiplayer)
            return true;

        // Replicate the call to base.ProcessInput(ev);
        __instance.CurActivateSound?.PlayOneShotOnCamera();

        if (!LoadTeleporterSession.TryOpenLoadTeleporterDialog(___teleporterComp))
            LoadTeleporterSession.CreateLoadTeleporterDialog(___teleporterComp);

        return false;
    }

    [MpCompatPrefix("VFEI.Command_LoadTeleporter", "ActionMakeDropPodInfo")]
    private static bool DropPodInfoOutOfInterface(ref ActiveDropPod __result)
    {
        if (!MP.InInterface)
            return true;

        // Return an empty/dummy ActiveDropPod.
        // The method would transfer all things held by the teleporter
        // to the drop pod in interface, which could mess things up.
        // We have to init the drop pod ourselves pre invoking synced
        // method, if the method expects it to be created at this point.
        __result = new ActiveDropPod();
        return false;
    }

    [MpCompatPrefix("VFEI.Command_LoadTeleporter", "GetTeleporterFloatMenuOptionsAt", 11)]
    private static bool StopPostActionForTargetSpecificCell()
    {
        // Cancel the call in MP, as it would mess with MP if called.
        return !MP.IsInMultiplayer;
    }

    [MpCompatPrefix("VFEI.Command_LoadTeleporter", "TargetingLabelGetter")]
    [MpCompatPrefix("VFEI.Command_LoadTeleporter", "ChoseWorldTarget")]
    private static void PreTeleporterOptionsAtTile(ThingComp ___teleporterComp)
    {
        if (!MP.IsInMultiplayer)
            return;

        var map = ___teleporterComp?.parent?.MapHeld;
        if (___teleporterComp?.parent?.MapHeld == null)
            return;

        var session = MP.GetLocalSessionManager(map).GetFirstOfType<LoadTeleporterSession>();
        if (session == null)
        {
            Log.ErrorOnce($"Trying to display targeting for a teleporter, but it doesn't have a session. Teleporter: {___teleporterComp}",
                Gen.HashCombineInt(___teleporterComp.parent.GetHashCode(), -94108726));
            return;
        }

        SetCurrentSessionState(session);

        if (session.thingOwnerDirty)
        {
            // Clear just in case. Need to do it on the inner list,
            // as clearing directly would have side effects.
            session.dummyThingOwner.InnerListForReading.Clear();
            // Fill the thing owner without actually removing/splitting stuff from the map.
            // Again, need to operate on the inner list as adding normally would
            // have side effects undesirable for us, like despawning things/splitting stack.
            foreach (var transferable in session.transferables)
            {
                if (transferable.CountToTransfer > 0)
                {
                    var thing = transferable.AnyThing;
                    if (thing is Pawn || transferable.CountToTransfer == thing.stackCount)
                    {
                        session.dummyThingOwner.InnerListForReading.Add(thing);
                    }
                    else
                    {
                        // Create a new thing without initializing it.
                        // We need a new thing to be able to "put" specific count
                        // of it into the ThingOwner, but since it's only a dummy
                        // ThingOwner we need to make sure we aren't putting creating
                        // real Things (so no assigning IDs or PostMake calls,
                        // as it could mess with MP).
                        var newThing = (Thing)Activator.CreateInstance(thing.def.thingClass);
                        newThing.def = thing.def;
                        newThing.stackCount = transferable.CountToTransfer;
                        newThing.SetStuffDirect(thing.Stuff);

                        session.dummyThingOwner.InnerListForReading.Add(newThing);
                    }
                }
            }

            session.thingOwnerDirty = false;
        }
    }

    [MpCompatFinalizer("VFEI.Command_LoadTeleporter", "TargetingLabelGetter")]
    [MpCompatFinalizer("VFEI.Command_LoadTeleporter", "ChoseWorldTarget")]
    private static void PostTeleporterOptionsAtTile()
    {
        if (LoadTeleporterSession.drawingSession != null)
            SetCurrentSessionState(null);
    }

    private static IThingHolder ReplaceCompWithSession(IThingHolder comp)
        => LoadTeleporterSession.drawingSession ?? comp;

    [MpCompatTranspiler("VFEI.Command_LoadTeleporter", "GetTeleporterFloatMenuOptionsAt", MethodType.Enumerator)]
    private static IEnumerable<CodeInstruction> TeleporterOptionsAtTileTranspiler(IEnumerable<CodeInstruction> instr, MethodBase baseMethod)
    {
        var targetField = AccessTools.DeclaredField("VFEI.Command_LoadTeleporter:teleporterComp");
        var addition = MpMethodUtil.MethodOf(ReplaceCompWithSession);

        // Took me way too long to realize the issue with this method.
        // Replace direct call to CompTeleporter methods (which only works on CompTeleporter),
        // and replace it with the call to the interface method (which will work on our type as well).
        // If not patched it can cause the resulting object to either
        // crash the game, or cause null reference exceptions.
        var targetMethod = AccessTools.DeclaredMethod("VFEI.CompTeleporter:GetDirectlyHeldThings");
        var replacementMethod = AccessTools.DeclaredMethod(typeof(IThingHolder), nameof(IThingHolder.GetDirectlyHeldThings));

        var interceptedCount = 0;
        var replacedCount = 0;

        foreach (var ci in instr)
        {
            yield return ci;

            if (ci.opcode == OpCodes.Ldfld && ci.operand is FieldInfo info && info == targetField)
            {
                yield return new CodeInstruction(OpCodes.Call, addition);

                interceptedCount++;
            }
            else if (ci.Calls(targetMethod))
            {
                ci.opcode = OpCodes.Call;
                ci.operand = replacementMethod;

                replacedCount++;
            }
        }

        const int expectedIntercepts = 3;
        const int expectedReplacements = 1;
        if (interceptedCount != expectedIntercepts)
        {
            var name = (baseMethod.DeclaringType?.Namespace).NullOrEmpty() ? baseMethod.Name : $"{baseMethod.DeclaringType!.Name}:{baseMethod.Name}";
            Log.Warning($"Patched incorrect number of this.teleporterComp calls (patched {interceptedCount}, expected {expectedIntercepts}) for method {name}");
        }
        if (replacedCount != expectedReplacements)
        {
            var name = (baseMethod.DeclaringType?.Namespace).NullOrEmpty() ? baseMethod.Name : $"{baseMethod.DeclaringType!.Name}:{baseMethod.Name}";
            Log.Warning($"Patched incorrect number of CompTeleporter.GetDirectlyHeldThings calls (patched {replacedCount}, expected {expectedReplacements}) for method {name}");
        }
    }

    #endregion

    #region Comp Patches

    private static bool ReturnFalseInMp(bool value) => !MP.IsInMultiplayer && value;

    [MpCompatTranspiler("VFEI.CompTeleporter", "CompTickRare")]
    private static IEnumerable<CodeInstruction> ReplaceIsTargeting(IEnumerable<CodeInstruction> instr, MethodBase baseMethod)
    {
        // The method checks if the teleported is full. If it's full and the player is not
        // currently targeting, it'll spill out its contents around. In MP, the check for
        // not targeting depends on the current player and not a global state, so we always
        // replace false in MP for safety.
        // 
        // This is mostly of a precaution, as (whenever the method runs) the teleporter should
        // always be empty in MP (unless hosting from a point where it was already full). This
        // is due to changes of how the teleporter works in MP, where it's loaded right before
        // being used (in SP it's loaded, and then the player is picking the target).

        // Target methods
        var worldTarget = AccessTools.DeclaredPropertyGetter(typeof(WorldTargeter), nameof(WorldTargeter.IsTargeting));
        var mapTarget = AccessTools.DeclaredPropertyGetter(typeof(Targeter), nameof(Targeter.IsTargeting));
        // Method to call after the target
        var insertion = MpMethodUtil.MethodOf(ReturnFalseInMp);
        // Counter
        var insertedCount = 0;

        foreach (var ci in instr)
        {
            yield return ci;

            if (ci.Calls(worldTarget) || ci.Calls(mapTarget))
            {
                // Instead of replacing the call, insert another one that will
                // take the result from the previous one and either return it
                // (in SP) or return false (in MP).
                yield return new CodeInstruction(OpCodes.Call, insertion);
                insertedCount++;
            }
        }

        const int expectedInsertions = 2;
        if (insertedCount != expectedInsertions)
        {
            var name = (baseMethod.DeclaringType?.Namespace).NullOrEmpty() ? baseMethod.Name : $"{baseMethod.DeclaringType!.Name}:{baseMethod.Name}";
            Log.Warning($"Patched incorrect number of Find.WorldTargeter.IsTargeting and Find.Targeter.IsTargeting calls (patched {insertedCount}, expected {expectedInsertions}) for method {name}");
        }
    }

    #endregion

    #region Syncing

    private static void PreTeleporterCommandNone(object instance, object[] args)
        => PreSyncTeleporterCommand(instance, args, 0, false);

    private static void PreTeleporterCommandAttackSettlement(object instance, object[] args)
        => PreSyncTeleporterCommand(instance, args, 0, true);

    private static void PreTeleporterCommandTargetSpecificCell(object instance, object[] args)
    {
        // Instance is <>c__DisplayClass13_1, which we don't want.
        // We need to access the command itself.
        var command = innerClassThisField(innerClassLocalsField(instance));
        PreSyncTeleporterCommand(command);
    }

    private static void PostTeleporterCommandTargetSpecificCell(object instance, object[] args)
    {
        // Need to destroy the teleporter after the sync command is executed
        var command = innerClassThisField(innerClassLocalsField(instance));
        commandCompField(command).parent.Destroy();

        if (!MP.IsExecutingSyncCommandIssuedBySelf)
            return;

        Find.WorldTargeter.StopTargeting();
        Find.Targeter.StopTargeting();

        // If the player that execute the command, switch the map (if possible)
        var map = innerClassMapParentField(instance)?.Map;
        if (map != null && Find.Maps.Contains(map))
            Current.Game.CurrentMap = map;
    }

    private static void PreTeleporterCommandOtherAction(object instance, object[] args)
        => PreSyncTeleporterCommand(instance);

    private static void PreSyncTeleporterCommand(object instance, object[] args = null, int initIndex = -1, bool useContents = false)
    {
        var comp = commandCompField((Command)instance);

        if (comp == null)
        {
            Log.Error("Trying so sync teleporter but it is null");
            return;
        }

        // If the teleporter is already holding stuff it means it was used/is in use, skip.
        if (((IThingHolder)comp).GetDirectlyHeldThings().Any)
            return;

        var map = comp.parent?.MapHeld;
        if (map == null)
        {
            Log.Error($"Trying so sync teleporter but its map is null, teleporter: {comp.ToStringSafe()}, parent: {comp?.parent.ToStringSafe()}");
            return;
        }

        var session = MP.GetLocalSessionManager(map).GetFirstOfType<LoadTeleporterSession>();
        if (session == null)
        {
            Log.Error($"Trying so sync teleporter but the session is null, teleporter: {comp}, parent: {comp.parent}");
            return;
        }

        // Transfer all the transferables to the teleporter before launching it.
        dialogLoadInstantlyMethod(session.PrepareDummyDialog());

        // Init the drop pod if needed and pass it (or its contents) as the argument
        if (initIndex >= 0)
        {
            if (args == null || initIndex >= args.Length)
                Log.Error("Trying to init drop pod for teleporter, but args are empty");
            else if (initIndex >= args.Length)
                Log.Error($"Trying to init drop pod for teleporter, but target arg is out of range (target: {initIndex}, length: {args.Length})");
            else
            {
                var dropPodInfo = (ActiveDropPod)commandActionMakeDropPodInfoMethod(instance);
                if (useContents)
                    args[initIndex] = dropPodInfo.Contents;
                else
                    args[initIndex] = dropPodInfo;
            }
        }

        session.Remove();
    }

    [MpCompatSyncWorker("VFEI.Command_LoadTeleporter", shouldConstruct = true)]
    private static void SyncCommandTeleporter(SyncWorker sync, ref Command command)
    {
        if (sync.isWriting)
        {
            // Don't sync thing comp, as we may end up syncing
            // 2 maps at the same time and MP doesn't allow that.
            // If null, sync -1 (value for uninitialized things).
            sync.Write(commandCompField(command)?.parent?.thingIDNumber ?? -1);
        }
        else
        {
            // Again, can't sync as ThingComp cause MP doesn't support multi map syncing.
            var id = sync.Read<int>();
            if (id != -1 && MP.TryGetThingById(id, out var thing))
            {
                if (thing is ThingWithComps t)
                    commandCompField(command) = t.AllComps.FirstOrDefault(x => teleporterCompType.IsInstanceOfType(x));
            }
        }
    }

    #endregion

    #endregion
}