using AlteredCarbon;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using SmashTools;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Almost There! by Roolo</summary>
    /// <see href="https://steamcommunity.com/workshop/filedetails/?id=2196278117"/>
    [MpCompatFor("hlx.UltratechAlteredCarbon")]
    public class AlteredCarbon
    {
        public AlteredCarbon(ModContentPack mod)
        {
            LongEventHandler.ExecuteWhenFinished(LatePatch);
        }

        static FieldInfo DontSync;
        static FieldInfo UseLocalIdsOverride;
        static FieldInfo IgnoreTraces;

        //A Buncccccccccccccch of infos ok I admit AlteredCarbon is a BIG mod
        //Should turn to referenced.

        static ISyncField FieldCompNeuralCacheAllowColonistNeuralStacks;
        static ISyncField FieldCompNeuralCacheAllowHostileNeuralStacks;
        static ISyncField FieldCompNeuralCacheAllowStrangerNeuralStacks;

        static ISyncField FieldNeuralDataNeuralDataRewritten;
        static ISyncField FieldNeuralDataEditTime;
        static ISyncField FieldNeuralDataStackDegradationToAdd;

        static ISyncField FieldNeuralDataTrackedToMatrix;
        static ISyncField FieldNeuralStackAutoLoad;

        static SyncType SyncTypeNeuralData;
        static void LatePatch()
        {
            //CompCastingRelay
            MP.RegisterSyncMethod(typeof(CompCastingRelay), nameof(CompCastingRelay.TuneTo));

            //CompNeuralCache
            MpCompat.RegisterLambdaMethod(typeof(CompNeuralCache), nameof(CompNeuralCache.CompGetGizmosExtra), 0);
            MpCompat.RegisterLambdaMethod(typeof(CompNeuralCache), nameof(CompNeuralCache.CompGetGizmosExtra), 1).SetDebugOnly();


            //Building_NeuralConnector
            MpCompat.RegisterLambdaMethod(typeof(Building_NeuralConnector), nameof(Building_NeuralConnector.GetGizmos), 3, 4, 11);
            MpCompat.RegisterLambdaDelegate(typeof(Building_NeuralConnector), nameof(Building_NeuralConnector.GetGizmos), 6, 10);
            MpCompat.RegisterLambdaMethod(typeof(Building_NeuralConnector), nameof(Building_NeuralConnector.GetGizmos), 0, 12).SetDebugOnly();
            MpCompat.RegisterLambdaDelegate(typeof(Building_NeuralConnector), nameof(Building_NeuralConnector.GetFloatMenuOptions), 0);

            //Building_SleeveCasket
            MpCompat.harmony.Patch(AccessTools.Method(typeof(Building_SleeveCasket), nameof(Building_SleeveCasket.GetInspectString)), transpiler: new HarmonyMethod(typeof(AlteredCarbon), nameof(TranspileBuilding_SleeveCasketGetInspectString)));

            //Building_SleeveGestator
            MP.RegisterSyncMethod(typeof(Building_SleeveGestator), nameof(Building_SleeveGestator.StartGrowth)).ExposeParameter(0);
            MP.RegisterSyncMethod(typeof(Building_SleeveGestator), nameof(Building_SleeveGestator.FinishGrowth)).SetDebugOnly();
            MP.RegisterSyncMethod(typeof(Building_SleeveGestator), nameof(Building_SleeveGestator.AddGrowth)).SetDebugOnly();
            MP.RegisterSyncMethod(typeof(Building_SleeveGestator), nameof(Building_SleeveGestator.OrderToCancel));
            MpCompat.RegisterLambdaMethod(typeof(Building_SleeveGestator), nameof(Building_SleeveGestator.RepurposeCorpse), 0);

            //Building_NeuralEditor
            //All good?
            MP.RegisterSyncMethod(typeof(Building_NeuralEditor), nameof(Building_NeuralEditor.PostInstall));

            //Window_SleeveCustomization
            MpCompat.harmony.Patch(AccessTools.Method(typeof(Window_SleeveCustomization), nameof(Window_SleeveCustomization.CreateSleeve)),
                prefix: new HarmonyMethod(typeof(Window_SleeveCustomizationCreateSleeve_Patch), nameof(Window_SleeveCustomizationCreateSleeve_Patch.Prefix)),
                finalizer: new HarmonyMethod(typeof(Window_SleeveCustomizationCreateSleeve_Patch), nameof(Window_SleeveCustomizationCreateSleeve_Patch.Finalizer))
            );

            //MP stuffs
            DontSync = AccessTools.Field(AccessTools.TypeByName("Multiplayer.Client.Multiplayer"), "dontSync");
            UseLocalIdsOverride = AccessTools.Field(AccessTools.TypeByName("Multiplayer.Client.Patches.UniqueIdsPatch"), "useLocalIdsOverride");
            IgnoreTraces = AccessTools.Field(AccessTools.TypeByName("Multiplayer.Client.Desyncs.DeferredStackTracing"), "ignoreTraces");

            MpCompat.harmony.Patch(AccessTools.Method(typeof(Pawn_NeedsTracker), "ShouldHaveNeed"),
                prefix: new HarmonyMethod(typeof(Pawn_NeedsTracker_ShouldHaveNeed_Patch), nameof(Pawn_NeedsTracker_ShouldHaveNeed_Patch.Prefix)));
            MpCompat.harmony.Patch(AccessTools.Method(typeof(Pawn_IdeoTracker), nameof(Pawn_IdeoTracker.ExposeData)),
                prefix: new HarmonyMethod(typeof(Pawn_IdeoTracker_ExposeData_Patch), nameof(Pawn_IdeoTracker_ExposeData_Patch.Prefix)),
                postfix: new HarmonyMethod(typeof(Pawn_IdeoTracker_ExposeData_Patch), nameof(Pawn_IdeoTracker_ExposeData_Patch.Postfix)));

            //Building_NeuralMatrix
            //delegates 0, 1 can raise refreshDummypawn, have to use watch instead
            MpCompat.RegisterLambdaDelegate(typeof(Window_NeuralMatrixManagement), nameof(Window_NeuralMatrixManagement.DrawStackEntry), 2, 3);

            MP.RegisterSyncMethod(typeof(Hediff_NeuralStack), nameof(Hediff_NeuralStack.NeedlecastTo));
            MP.RegisterSyncMethod(typeof(NeuralStack), nameof(NeuralStack.NeedlecastTo));
            MP.RegisterSyncMethod(typeof(Hediff_RemoteStack), nameof(Hediff_RemoteStack.EndNeedlecasting));
            MP.RegisterSyncMethod(typeof(NeuralStack), nameof(NeuralStack.InstallStackRecipe));

            MpCompat.RegisterLambdaDelegate(typeof(NeuralStack), nameof(NeuralStack.GetGizmos), 10);

            FieldCompNeuralCacheAllowColonistNeuralStacks = MP.RegisterSyncField(typeof(CompNeuralCache), nameof(CompNeuralCache.allowColonistNeuralStacks));
            FieldCompNeuralCacheAllowHostileNeuralStacks = MP.RegisterSyncField(typeof(CompNeuralCache), nameof(CompNeuralCache.allowHostileNeuralStacks));
            FieldCompNeuralCacheAllowStrangerNeuralStacks = MP.RegisterSyncField(typeof(CompNeuralCache), nameof(CompNeuralCache.allowStrangerNeuralStacks));

            MP.RegisterSyncWorker<CompNeuralCache>(SyncCompNeuralCache);
            MP.RegisterSyncWorker<NeuralData>(SyncNeuralData);
            MP.RegisterSyncWorker<IStackHolder>(SyncIStackHolder);
            MP.RegisterSyncWorker<INeedlecastable>(SyncINeedlecastable);
            MP.RegisterSyncWorker<StackInstallInfo>(SyncStackInstallInfo);

            SyncTypeNeuralData = new SyncType(typeof(NeuralData)) { expose = true };

            MP.RegisterSyncWorker<Window_NeuralMatrixManagement>(SyncWindow_NeuralMatrixManagement);

            MpCompat.harmony.Patch(AccessTools.Method(typeof(Window_NeuralMatrixManagement), nameof(Window_NeuralMatrixManagement.DoWindowContents)),
                prefix: new HarmonyMethod(typeof(Window_NeuralMatrixManagementDoWindowContents_Patch), nameof(Window_NeuralMatrixManagementDoWindowContents_Patch.Prefix)),
                finalizer: new HarmonyMethod(typeof(Window_NeuralMatrixManagementDoWindowContents_Patch), nameof(Window_NeuralMatrixManagementDoWindowContents_Patch.Finalizer)));

            MpCompat.harmony.Patch(AccessTools.Method(typeof(Window_NeuralMatrixManagement), nameof(Window_NeuralMatrixManagement.DrawRightPanel)),
                prefix: new HarmonyMethod(typeof(Window_NeuralMatrixManagementDrawRightPanel_Patch), nameof(Window_NeuralMatrixManagementDrawRightPanel_Patch.Prefix)),
                postfix: new HarmonyMethod(typeof(Window_NeuralMatrixManagementDrawRightPanel_Patch), nameof(Window_NeuralMatrixManagementDrawRightPanel_Patch.Finalizer)));

            MpCompat.harmony.Patch(AccessTools.Method(typeof(Window_NeuralMatrixManagement), nameof(Window_NeuralMatrixManagement.DrawStackEntry)),
                prefix: new HarmonyMethod(typeof(Window_NeuralMatrixManagementDrawStackEntry_Patch), nameof(Window_NeuralMatrixManagementDrawStackEntry_Patch.Prefix))
                );

            MpCompat.harmony.Patch(AccessTools.Method(typeof(Window_NeuralMatrixManagement), nameof(Window_NeuralMatrixManagement.DrawBottom)),
                prefix: new HarmonyMethod(typeof(Window_NeuralMatrixManagementDrawBottom_Patch), nameof(Window_NeuralMatrixManagementDrawBottom_Patch.Prefix)),
                finalizer: new HarmonyMethod(typeof(Window_NeuralMatrixManagementDrawBottom_Patch), nameof(Window_NeuralMatrixManagementDrawBottom_Patch.Finalizer))
                );

            //Window_StackEditor
            MpCompat.harmony.Patch(AccessTools.Method(typeof(Window_StackEditor), nameof(Window_StackEditor.ResetIndices)),
                prefix: new HarmonyMethod(typeof(Window_StackEditor_ResetIndices_Patch), nameof(Window_StackEditor_ResetIndices_Patch.Prefix))
                );
            MpCompat.harmony.Patch(AccessTools.Method(typeof(Window_StackEditor), nameof(Window_StackEditor.DoWindowContents)),
                prefix: new HarmonyMethod(typeof(Window_StackEditor_Draw_Patch), nameof(Window_StackEditor_Draw_Patch.PreDoWindowContents)),
                postfix: new HarmonyMethod(typeof(Window_StackEditor_Draw_Patch), nameof(Window_StackEditor_Draw_Patch.FinalizerDoWindowContents)));
            MpCompat.harmony.Patch(AccessTools.Method(typeof(Window_StackEditor), nameof(Window_StackEditor.DrawAcceptCancelButtons)),
                prefix: new HarmonyMethod(typeof(Window_StackEditor_Draw_Patch), nameof(Window_StackEditor_Draw_Patch.PreDrawAcceptCancelButtons)),
                postfix: new HarmonyMethod(typeof(Window_StackEditor_Draw_Patch), nameof(Window_StackEditor_Draw_Patch.FinalizerDrawAcceptCancelButtons)));

            FieldNeuralDataNeuralDataRewritten = MP.RegisterSyncField(AccessTools.Field(typeof(NeuralData), nameof(NeuralData.neuralDataRewritten)));
            FieldNeuralDataEditTime = MP.RegisterSyncField(AccessTools.Field(typeof(NeuralData), nameof(NeuralData.editTime)));
            FieldNeuralDataStackDegradationToAdd = MP.RegisterSyncField(AccessTools.Field(typeof(NeuralData), nameof(NeuralData.stackDegradation)));

            FieldNeuralDataTrackedToMatrix = MP.RegisterSyncField(typeof(NeuralData), nameof(NeuralData.trackedToMatrix));
            FieldNeuralStackAutoLoad = MP.RegisterSyncField(typeof(NeuralStack), nameof(NeuralStack.autoLoad));


            MpCompat.harmony.Patch(AccessTools.Method(typeof(NeuralData), nameof(NeuralData.RefreshDummyPawn)),
                prefix: new HarmonyMethod(typeof(NeuralDataRefreshDummyPawn_Patch), nameof(NeuralDataRefreshDummyPawn_Patch.Prefix)),
                postfix: new HarmonyMethod(typeof(NeuralDataRefreshDummyPawn_Patch), nameof(NeuralDataRefreshDummyPawn_Patch.Finalizer)));
        }
        static class Window_StackEditor_ResetIndices_Patch
        {
            public static void Prefix(Window_StackEditor __instance)
            {
                // TODO Filter out Spectator once we get api implemented
                __instance.allFactions.AddRange(Find.FactionManager.allFactions.FindAll(faction => faction.IsPlayer && faction.Hidden && !__instance.allFactions.Contains(faction)));
            }
        }

        static class NeuralDataRefreshDummyPawn_Patch
        {
            static bool preDontSync;
            static bool prevUseLocalId;
            public static void Prefix()
            {
                preDontSync = (bool)DontSync.GetValue(null);
                DontSync.SetValue(null, true);
                prevUseLocalId = (bool)UseLocalIdsOverride.GetValue(null);
                UseLocalIdsOverride.SetValue(null, true);
                IgnoreTraces.SetValue(null, (int)IgnoreTraces.GetValue(null) + 1);
                Rand.PushState();
            }
            public static void Finalizer()
            {
                Rand.PopState();
                IgnoreTraces.SetValue(null, (int)IgnoreTraces.GetValue(null) - 1);
                DontSync.SetValue(null, preDontSync);
                UseLocalIdsOverride.SetValue(null, prevUseLocalId);
            }
        }
        //forceconstruct
        static void SyncStackInstallInfo(SyncWorker sync, ref StackInstallInfo obj)
        {
            if (sync.isWriting)
            {
                StackInstallInfo info = obj;
                sync.Write<ThingDef>(AC_Utils.stackRecipesByDef.First(pair => pair.Value.recipe == info.recipe).Key);
            }
            else
            {
                AC_Utils.stackRecipesByDef.TryGetValue(sync.Read<ThingDef>(), obj);
            }
        }
        static void SyncINeedlecastable(SyncWorker sync, ref INeedlecastable obj)
        {
            if (sync.isWriting)
            {
                if (obj is NeuralStack)
                {
                    sync.Write<Thing>(obj as Thing);
                }
                else
                {
                    //it's heddif
                    sync.Write<Thing>(((Hediff_NeuralStack)obj)?.pawn);
                }
            }
            else
            {
                Thing t = sync.Read<Thing>();
                if (t is NeuralStack)
                {
                    obj = t as NeuralStack;
                }
                else
                {
                    obj = (t as Pawn).health.hediffSet.GetFirstHediff<Hediff_NeuralStack>();
                }
            }
        }
        static void SyncIStackHolder(SyncWorker sync, ref IStackHolder obj)
        {
            if (sync.isWriting)
            {
                if (obj is NeuralStack)
                {
                    sync.Write<Thing>(obj as Thing);
                }
                else
                {
                    //it's heddif
                    sync.Write<Thing>(((Hediff_NeuralStack)obj)?.pawn);
                }
            }
            else
            {
                Thing t = sync.Read<Thing>();
                if (t is NeuralStack)
                {
                    obj = t as NeuralStack;
                }
                else
                {
                    obj = (t as Pawn).health.hediffSet.GetFirstHediff<Hediff_NeuralStack>();
                }
            }
        }
        static void SyncWindow_NeuralMatrixManagement(SyncWorker sync, ref Window_NeuralMatrixManagement window)
        {
            if (sync.isWriting)
            {
                sync.Write<Building>((Building)window.matrix);
            }
            else
            {
                var matrix = sync.Read<Building>() as Building_NeuralMatrix;
                window = new Window_NeuralMatrixManagement(matrix);
            }
        }
        static void SyncCompNeuralCache(SyncWorker sync, ref CompNeuralCache comp)
        {
            if (sync.isWriting)
            {
                sync.Write<ThingWithComps>(comp.parent);
            }
            else
            {
                var thing = sync.Read<ThingWithComps>();
                comp = thing.GetComp<CompNeuralCache>();
            }
        }
        static void SyncNeuralData(SyncWorker sync, ref NeuralData neuralData)
        {
            if (sync.isWriting)
            {
                if (neuralData.host.GetNeuralData() == neuralData)
                {
                    sync.Write<bool>(true);
                    //trackable data
                    sync.Write<Thing>(neuralData.host);

                }
                else if (neuralData.hostPawn.GetNeuralData() == neuralData)
                {
                    sync.Write<bool>(true);
                    //trackable data
                    sync.Write<Pawn>(neuralData.hostPawn);
                }
                else
                {
                    sync.Write<bool>(false);
                    //copied data
                    sync.Write(neuralData, SyncTypeNeuralData);
                    sync.Write<Ideo>(neuralData.ideo);
                    neuralData.dummyPawn = null;
                }
            }
            else
            {
                if (sync.Read<bool>())
                {
                    neuralData = sync.Read<Thing>().GetNeuralData();
                }
                else
                {
                    neuralData = sync.Read<NeuralData>(SyncTypeNeuralData);
                    neuralData.ideo = sync.Read<Ideo>();
                    neuralData.dummyPawn = null;
                }
            }
        }
        static class Window_NeuralMatrixManagementDoWindowContents_Patch
        {
            public static void Prefix(Window_NeuralMatrixManagement __instance)
            {
                MP.WatchBegin();
                var matrix = __instance.matrix;
                var comp = matrix.compCache;
                FieldCompNeuralCacheAllowColonistNeuralStacks.Watch(comp);
                FieldCompNeuralCacheAllowHostileNeuralStacks.Watch(comp);
                FieldCompNeuralCacheAllowStrangerNeuralStacks.Watch(comp);
            }

            public static void Finalizer(Window __instance)
            {
                MP.WatchEnd();
            }
        }

        static class Window_NeuralMatrixManagementDrawStackEntry_Patch
        {
            public static void Prefix(IStackHolder stack)
            {
                FieldNeuralDataTrackedToMatrix.Watch(stack.NeuralData);
            }
        }
        static class Window_NeuralMatrixManagementDrawBottom_Patch
        {
            public static void Prefix(Window_NeuralMatrixManagement __instance)
            {
                DontSync.SetValue(null, false);
                NeuralStack neuralStack = __instance.selectedStack?.ThingHolder as NeuralStack;
                if (neuralStack != null)
                {
                    Log.Warning($"{neuralStack} {neuralStack.autoLoad}");
                    FieldNeuralStackAutoLoad.Watch(neuralStack);
                }
            }
            public static void Finalizer() => DontSync.SetValue(null, true);
        }
        static class Building_SleeveGestator_StartGrowth_Patch
        {
            public static void Postfix(Pawn newSleeve)
            {
                var instance = AlteredCarbonManager.Instance;
                var emptySleeves = instance.emptySleeves;
                emptySleeves.RemoveWhere(p => p.thingIDNumber < 0);
                emptySleeves.Add(newSleeve);
            }
        }

        static class Pawn_IdeoTracker_ExposeData_Patch
        {
            public static void Prefix(Pawn_IdeoTracker __instance)
            {
                if (__instance.pawn.story?.Childhood?.defName == "AC_VatGrownChild")
                    __instance.ideo = new Ideo();
            }

            public static void Postfix(Pawn_IdeoTracker __instance)
            {
                if (__instance.pawn.story?.Childhood?.defName == "AC_VatGrownChild")
                    __instance.ideo = null;
            }
        }

        static class Pawn_NeedsTracker_ShouldHaveNeed_Patch
        {
            static HashSet<string> bodyNeeds = new HashSet<string> { "Food", "Bladder", "Hygiene", "DBHThirst" };

            // Really not pretty but no other choice to bypass I guess?
            public static bool Prefix(Pawn ___pawn, NeedDef nd, ref bool __result)
            {
                if (___pawn.story?.Childhood?.defName != "AC_VatGrownChild")
                    return true;
                if (___pawn.ParentHolder != null && ___pawn.ParentHolder is not Building_SleeveGestator)
                    return true;
                if (___pawn.Spawned)
                    return true;
                __result = bodyNeeds.Contains(nd.defName);
                return false;
            }
        }
        static class Window_StackEditor_Draw_Patch
        {
            static bool preDontSync;
            public static void PreDoWindowContents()
            {
                preDontSync = (bool)DontSync.GetValue(null);
                DontSync.SetValue(null, true);
            }
            public static void PreDrawAcceptCancelButtons(Window_StackEditor __instance)
            {
                DontSync.SetValue(null, false);
                MP.WatchBegin();
                var data = __instance.thingWithStack.GetNeuralData(false);
                FieldNeuralDataNeuralDataRewritten.Watch(data);
                FieldNeuralDataEditTime.Watch(data);
                FieldNeuralDataStackDegradationToAdd.Watch(data);
            }

            public static void FinalizerDrawAcceptCancelButtons()
            {

                MP.WatchEnd();
                DontSync.SetValue(null, true);
            }
            public static void FinalizerDoWindowContents()
            {
                DontSync.SetValue(null, preDontSync);
            }
        }
        static class Window_NeuralMatrixManagementDrawRightPanel_Patch
        {
            static bool preDontSync;
            public static void Prefix()
            {
                preDontSync = (bool)DontSync.GetValue(null);
                DontSync.SetValue(null, true);
            }
            public static void Finalizer() => DontSync.SetValue(null, preDontSync);
        }
        static class Window_SleeveCustomizationCreateSleeve_Patch
        {
            static bool preDontSync;
            public static void Prefix()
            {
                preDontSync = (bool)DontSync.GetValue(null);
                DontSync.SetValue(null, true);
                Rand.PushState();
            }

            public static void Finalizer()
            {
                Rand.PopState();
                DontSync.SetValue(null, preDontSync);
            }
        }

        //Prevent Building_SleeveCasket from calling Building_Bed_set_Medical which is spamming
        //Tried just skip prev 2 + cur but second ldarg.0 got labels so can't skip
        static IEnumerable<CodeInstruction> TranspileBuilding_SleeveCasketGetInspectString(IEnumerable<CodeInstruction> instructions)
        {
            var Building_Bed_set_Medical = AccessTools.PropertySetter(typeof(Building_Bed), nameof(Building_Bed.Medical));

            var codes = new List<CodeInstruction>(instructions);
            for (int i = 0; i < codes.Count; i++)
            {
                var instr = codes[i];
                if (instr.opcode == OpCodes.Call && (MethodInfo)instr.operand == Building_Bed_set_Medical)
                {
                    codes.RemoveAt(i + 1);
                    codes.RemoveAt(i);
                    codes.RemoveAt(i - 1);

                    i = i - 2;

                }
            }
            return codes;
        }
    }
}