﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Multiplayer.Compat
{
    /// <summary>Vanilla Expanded Framework and other Vanilla Expanded mods by Oskar Potocki, Sarg Bjornson, Chowder, XeoNovaDan, Orion, Kikohi, erdelf, Taranchuk, and more</summary>
    /// <see href="https://github.com/Vanilla-Expanded/VanillaExpandedFramework"/>
    /// <see href="https://github.com/Vanilla-Expanded/VanillaCookingExpanded"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2023507013"/>
    [MpCompatFor("OskarPotocki.VanillaFactionsExpanded.Core")]
    class VanillaExpandedFramework
    {
        public VanillaExpandedFramework(ModContentPack mod)
        {
            (Action patchMethod, string componentName, bool latePatch)[] patches =
            [
                // Item Processor is obsolete and will be removed in a future update (likely during 1.6 update)
                (PatchItemProcessor, "Item Processor", false),
                (PatchAdvancedResourceProcessor, "Advanced Resource Processor", true),
                (PatchOtherRng, "Other RNG", false),
                (PatchVFECoreDebug, "Debug Gizmos", false),
                (PatchAbilities, "Abilities", true),
                (PatchHireableFactions, "Hireable Factions", false),
                (PatchVanillaFurnitureExpanded, "Vanilla Furniture Expanded", true),
                (PatchVanillaFactionMechanoids, "Vanilla Faction Mechanoids", false),
                (PatchAnimalBehaviour, "Animal Behaviour", false),
                (PatchExplosiveTrialsEffect, "Explosive Trials Effect", false),
                (PatchMVCF, "Multi-Verb Combat Framework", false),
                (PatchVanillaApparelExpanded, "Vanilla Apparel Expanded", false),
                (PatchVanillaWeaponsExpanded, "Vanilla Weapons Expanded", false),
                (PatchPipeSystem, "Pipe System", true),
                (PatchKCSG, "KCSG (custom structure generation)", false),
                (PatchFactionDiscovery, "Faction Discovery", false),
                (PatchVanillaGenesExpanded, "Vanilla Genes Expanded", false),
                (PatchVanillaCookingExpanded, "Vanilla Cooking Expanded", true),
                (PatchDoorTeleporter, "Teleporter Doors", true),
                (PatchSpecialTerrain, "Special Terrain", true),
                (PatchWeatherOverlayEffects, "Weather Overlay Effects", false),
                (PatchExtraPregnancyApproaches, "Extra Pregnancy Approaches", false),
                (PatchWorkGiverDeliverResources, "Building stuff requiring non-construction skill", false),
                (PatchExpandableProjectile, "Expandable projectile", false),
                (PatchStaticCaches, "Static caches", false),
                (PatchGraphicCustomizationDialog, "Graphic Customization Dialog", true),
                (PatchDraftedAi, "Drafted AI", true),
                (PatchMapObjectGeneration, "Thing spawning on map generation (ObjectSpawnsDef)", false),
                (PatchQuestChainsDevMode, "Quest chain dev mode window", false),
            ];

            foreach (var (patchMethod, componentName, latePatch) in patches)
            {
                if (latePatch)
                    LongEventHandler.ExecuteWhenFinished(ApplyPatch);
                else
                    ApplyPatch();

                void ApplyPatch()
                {
                    try
                    {
#if DEBUG
                        Log.Message($"Patching Vanilla Expanded Framework - {componentName}");
#endif

                        patchMethod();
                    }
                    catch (Exception e)
                    {
                        Log.Error($"Encountered an error patching {componentName} part of Vanilla Expanded Framework - this part of the mod may not work properly!");
                        Log.Error(e.ToString());
                    }
                }
            }
        }

        #region Shared sync workers and patches

        private static void SyncCommandWithBuilding(SyncWorker sync, ref Command command)
        {
            var traverse = Traverse.Create(command);
            var building = traverse.Field("building");

            if (sync.isWriting)
                sync.Write(building.GetValue() as Thing);
            else
                building.SetValue(sync.Read<Thing>());
        }
        private static void SyncCommandWithCompBuilding(SyncWorker sync, ref Command command)
        {
            var traverse = Traverse.Create(command);
            var building = traverse.Field("building");

            if (sync.isWriting)
                sync.Write(building.GetValue() as ThingComp);
            else
                building.SetValue(sync.Read<ThingComp>());
        }

        #endregion

        #region Small and generic patches

        // Generally, here's a place for patches that are a single method, without any stored fields, and aren't too long.

        private static void PatchOtherRng()
        {
            PatchingUtilities.PatchPushPopRand(new[]
            {
                // Uses GenView.ShouldSpawnMotesAt and uses RNG if it returns true,
                // and it's based on player camera position. Need to push/pop or it'll desync
                // unless all players looking when it's called
                "VFECore.HediffComp_Spreadable:ThrowFleck",
                // GenView.ShouldSpawnMotesAt again
                "VFECore.TerrainComp_MoteSpawner:ThrowMote",
                // Musket guns, etc
                "SmokingGun.Verb_ShootWithSmoke:TryCastShot",
                "VWEMakeshift.SmokeMaker:ThrowMoteDef",
                "VWEMakeshift.SmokeMaker:ThrowFleckDef",
            });
        }

        private static void PatchVFECoreDebug() 
            => MpCompat.RegisterLambdaMethod("VFECore.CompPawnDependsOn", "CompGetGizmosExtra", 0).SetDebugOnly();

        private static void PatchExplosiveTrialsEffect() 
            => PatchingUtilities.PatchPushPopRand("ExplosiveTrailsEffect.SmokeThrowher:ThrowSmokeTrail");

        private static void PatchVanillaApparelExpanded() 
            => MpCompat.RegisterLambdaMethod("VanillaApparelExpanded.CompSwitchApparel", "CompGetWornGizmosExtra", 0);

        private static void PatchVanillaWeaponsExpanded() 
            => MpCompat.RegisterLambdaMethod("VanillaWeaponsExpandedLaser.CompLaserCapacitor", "CompGetGizmosExtra", 1);

        private static void PatchVanillaFactionMechanoids()
        {
            var type = AccessTools.TypeByName("VFE.Mechanoids.CompMachineChargingStation");
            MpCompat.RegisterLambdaDelegate(type, "CompGetGizmosExtra", 1, 3).SetContext(SyncContext.MapSelected);

            // Dev recharge fully (0), attach turret (3)
            type = AccessTools.TypeByName("VFE.Mechanoids.CompMachine");
            MpCompat.RegisterLambdaMethod(type, "GetGizmos", 0, 3)[0].SetDebugOnly();
        }

        private static void PatchAnimalBehaviour()
        {
            // RNG
            PatchingUtilities.PatchSystemRand("AnimalBehaviours.DamageWorker_ExtraInfecter:ApplySpecialEffectsToPart", false);
            var rngFixConstructors = new[]
            {
                "AnimalBehaviours.CompAnimalProduct",
                "AnimalBehaviours.CompFilthProducer",
                "AnimalBehaviours.CompGasProducer",
                "AnimalBehaviours.CompInitialHediff",
                "AnimalBehaviours.DeathActionWorker_DropOnDeath",
                "AnimalBehaviours.HediffComp_FilthProducer",
            };
            PatchingUtilities.PatchSystemRandCtor(rngFixConstructors, false);

            // Gizmos
            var type = AccessTools.TypeByName("AnimalBehaviours.CompDestroyThisItem");
            MP.RegisterSyncMethod(type, "SetObjectForDestruction");
            MP.RegisterSyncMethod(type, "CancelObjectForDestruction");

            type = AccessTools.TypeByName("AnimalBehaviours.CompDieAndChangeIntoOtherDef");
            MP.RegisterSyncMethod(type, "ChangeDef");

            type = AccessTools.TypeByName("AnimalBehaviours.CompDiseasesAfterPeriod");
            MpCompat.RegisterLambdaMethod(type, "GetGizmos", 0).SetDebugOnly();

            type = AccessTools.TypeByName("AnimalBehaviours.Pawn_GetGizmos_Patch");
            MpCompat.RegisterLambdaDelegate(type, "Postfix", 1);
        }

        private static void PatchKCSG()
        {
            var type = AccessTools.TypeByName("KCSG.SettlementGenUtils");
            type = AccessTools.Inner(type, "Sampling");

            PatchingUtilities.PatchSystemRand(AccessTools.Method(type, "Sample"));

            // KCSG.SymbolResolver_ScatterStuffAround:Resolve uses seeder system RNG, should be fine
            // If not, will need patching

            PatchingUtilities.PatchPushPopRand(new[]
            {
                "KCSG.KCSG_Skyfaller:SaveImpact",
                "KCSG.KCSG_Skyfaller:Tick",
            });
        }

        private static void PatchVanillaGenesExpanded()
        {
            var type = AccessTools.TypeByName("VanillaGenesExpanded.CompHumanHatcher");
            PatchingUtilities.PatchSystemRand(AccessTools.Method(type, "Hatch"));
            MpCompat.RegisterLambdaMethod(type, "CompGetGizmosExtra", 0).SetDebugOnly();
        }

        private static void PatchExtraPregnancyApproaches()
        {
            MpCompat.RegisterLambdaDelegate(
                "VFECore.SocialCardUtility_DrawPregnancyApproach_Patch", 
                "AddPregnancyApproachOptions",
                0, 2); // Disable extra approaches (0), set extra approach (2)
        }

        private static void PatchExpandableProjectile()
        {
            MpCompat.harmony.Patch(AccessTools.DeclaredPropertyGetter("VFECore.ExpandableProjectile:StartingPosition"),
                prefix: new HarmonyMethod(typeof(VanillaExpandedFramework), nameof(PreStartingPositionGetter)),
                postfix: new HarmonyMethod(typeof(VanillaExpandedFramework), nameof(PostStartingPositionGetter)));
        }

        private static void PreStartingPositionGetter(Vector3 ___startingPosition, bool ___pawnMoved, ref (Vector3, bool)? __state)
        {
            // If in interface, store the current values.
            if (MP.InInterface)
                __state = (___startingPosition, ___pawnMoved);
        }

        private static void PostStartingPositionGetter(ref Vector3 ___startingPosition, ref bool ___pawnMoved, (Vector3, bool)? __state)
        {
            // If state not null (in interface), restore previous values.
            // Alternatively, we could also have separate values for interface and simulation,
            // but seems a bit pointless to do it like this since it's just a minor thing.
            if (__state != null)
                (___startingPosition, ___pawnMoved) = __state.Value;
        }

        #endregion

        #region Item Processor

        private static void PatchItemProcessor()
        {
            var type = AccessTools.TypeByName("ItemProcessor.Building_ItemProcessor");
            // _1, _5 and _7 are used to check if gizmo should be enabled, so we don't sync them
            MpCompat.RegisterLambdaMethod(type, "GetGizmos", 0, 2, 3, 4, 6, 8, 9, 10);

            type = AccessTools.TypeByName("ItemProcessor.Command_SetQualityList");
            MP.RegisterSyncWorker<Command>(SyncCommandWithBuilding, type, shouldConstruct: true);
            MP.RegisterSyncMethod(type, "AddQuality").SetContext(SyncContext.MapSelected);
            MpCompat.RegisterLambdaMethod(type, "ProcessInput", 7).SetContext(SyncContext.MapSelected);

            type = AccessTools.TypeByName("ItemProcessor.Command_SetOutputList");
            MP.RegisterSyncWorker<Command>(SyncCommandWithBuilding, type, shouldConstruct: true);
            MP.RegisterSyncMethod(type, "TryConfigureIngredientsByOutput");

            // Keep an eye on this in the future, seems like something the devs could combine into a single class at some point
            foreach (var ingredientNumber in new[] { "First", "Second", "Third", "Fourth" })
            {
                type = AccessTools.TypeByName($"ItemProcessor.Command_Set{ingredientNumber}ItemList");
                MP.RegisterSyncWorker<Command>(SyncSetIngredientCommand, type, shouldConstruct: true);
                MP.RegisterSyncMethod(type, $"TryInsert{ingredientNumber}Thing").SetContext(SyncContext.MapSelected);
                MpCompat.RegisterLambdaMethod(type, "ProcessInput", 0);
            }
        }

        private static void SyncSetIngredientCommand(SyncWorker sync, ref Command command)
        {
            var traverse = Traverse.Create(command);
            var building = traverse.Field("building");
            var ingredientList = traverse.Field("things");

            if (sync.isWriting)
            {
                sync.Write(building.GetValue() as Thing);
                var ingredientListValue = ingredientList.GetValue();
                if (ingredientListValue == null)
                {
                    sync.Write(false);
                }
                else
                {
                    sync.Write(true);
                    sync.Write(ingredientList.GetValue() as List<Thing>);
                }
            }
            else
            {
                building.SetValue(sync.Read<Thing>());
                if (sync.Read<bool>()) ingredientList.SetValue(sync.Read<List<Thing>>());
            }
        }

        #endregion

        #region Advanced Item Processor

        #region Fields

        // CompAdvancedResourceProcessor
        private static Type advancedResourceProcessorType;
        private static AccessTools.FieldRef<ThingComp, object> advancedResourceProcessorProcessStackField;
        // Process
        // Most of those have getters/setters, but given they're not virtual - not bothering to use them.
        private static AccessTools.FieldRef<object, ThingWithComps> processParentField;
        private static AccessTools.FieldRef<object, string> processIdField;
        private static AccessTools.FieldRef<object, Def> processDefField;
        private static AccessTools.FieldRef<object, int> processTargetCountField;
        private static AccessTools.FieldRef<object, float> processProgressField;
        private static AccessTools.FieldRef<object, bool> processSuspendedField;
        // ProcessStack
        private static FastInvokeHandler processStackAddProcessMethod;
        private static FastInvokeHandler processStackNotifyProcessChangeMethod;
        private static AccessTools.FieldRef<object, IList> processStackProcessesField;
        // ProcessUtility
        private static AccessTools.FieldRef<IDictionary> processUtilityClipboardField;

        #endregion

        #region Main Patch

        private static void PatchAdvancedResourceProcessor()
        {
            var type = advancedResourceProcessorType = AccessTools.TypeByName("PipeSystem.CompAdvancedResourceProcessor");
            advancedResourceProcessorProcessStackField = AccessTools.FieldRefAccess<object>(type, "processStack");
            // Add to process stack (2)
            MpCompat.RegisterLambdaDelegate(type, "ProcessesOptions", MethodType.Getter, 2);
            // Toggle: output to ground
            MpCompat.RegisterLambdaMethod(type, "Settings", MethodType.Getter, 0);
            // Extract at current quality (0), and debug: finish in 10 ticks (2), advance progress 1 day (3), empty wastepacks (4).
            MpCompat.RegisterLambdaMethod(type, nameof(ThingComp.CompGetGizmosExtra), 0, 2, 3).Skip(2).SetDebugOnly();
            // Handle paste (1) gizmo differently, since we need to also sync the clipboard.
            MpCompat.harmony.Patch(MpMethodUtil.GetLambda(type, nameof(ThingComp.CompGetGizmosExtra), lambdaOrdinal: 1),
                prefix: new HarmonyMethod(PrePasteProcessesGizmo));

            // The process, we'll need to sync it
            type = AccessTools.TypeByName("PipeSystem.Process");
            processParentField = AccessTools.FieldRefAccess<ThingWithComps>(type, "parent");
            processIdField = AccessTools.FieldRefAccess<string>(type, "id");
            processDefField = AccessTools.FieldRefAccess<Def>(type, "def");
            processTargetCountField = AccessTools.FieldRefAccess<int>(type, "targetCount");
            processProgressField = AccessTools.FieldRefAccess<float>(type, "progress");
            processSuspendedField = AccessTools.FieldRefAccess<bool>(type, "suspended");
            MP.RegisterSyncWorker<object>(SyncProcess, type, true);
            // Called (together with delete on process stack) when deleting. Other places this is called from don't matter.
            MP.RegisterSyncMethod(type, "ResetProcess");
            // Make x (0), make forever (1)
            MpCompat.RegisterLambdaMethod(type, "Options", MethodType.Getter, 0, 1);
            MP.RegisterSyncMethod(MpMethodUtil.MethodOf(SyncedProcessModifyTargetCountButton));
            MP.RegisterSyncMethod(MpMethodUtil.MethodOf(SyncedProcessSuspendButton));
            MpCompat.harmony.Patch(AccessTools.DeclaredMethod(type, "DoInterface"),
                transpiler: new HarmonyMethod(ReplaceProcessImageButtons));

            // Sync adding/removing from/reorganizing the process stack
            type = AccessTools.TypeByName("PipeSystem.ProcessStack");
            processStackAddProcessMethod = MethodInvoker.GetHandler(AccessTools.DeclaredMethod(type, "AddProcess"));
            processStackNotifyProcessChangeMethod = MethodInvoker.GetHandler(AccessTools.DeclaredMethod(type, "Notify_ProcessChange"));
            processStackProcessesField = AccessTools.FieldRefAccess<IList>(type, "processes");
            MpCompat.harmony.Patch(AccessTools.DeclaredMethod(type, "Reorder"),
                prefix: new HarmonyMethod(PreProcessStackReorder));
            // It's not easily possible to sync ProcessStack since it has no reference to its parent.
            // Its children do have reference to its parent, but it may have no children at the time,
            // so we need to transform the target to properly handle it.
            MP.RegisterSyncMethod(type, "AddProcess")
                .CancelIfAnyArgNull()
                .TransformTarget(Serializer.New((object _, object _, object[] args) => (ThingWithComps)args[1], GetProcessStackFromThingWithComps), true);
            MP.RegisterSyncMethod(type, "Delete")
                .CancelIfAnyArgNull()
                .TransformTarget(Serializer.New((object _, object _, object[] args) => processParentField(args[0]), GetProcessStackFromThingWithComps), true);
            MP.RegisterSyncMethod(type, "Reorder")
                .CancelIfAnyArgNull()
                .TransformTarget(Serializer.New((object _, object _, object[] args) => processParentField(args[0]), GetProcessStackFromThingWithComps), true);

            // Sync ITab_Processor
            type = AccessTools.TypeByName("PipeSystem.ProcessUtility");
            processUtilityClipboardField = AccessTools.StaticFieldRefAccess<IDictionary>(AccessTools.DeclaredField(type, "Clipboard"));

            type = AccessTools.TypeByName("PipeSystem.ITab_Processor");
            MP.RegisterSyncMethod(MpMethodUtil.MethodOf(SyncedPasteProcesses)).CancelIfAnyArgNull();
            MpCompat.harmony.Patch(AccessTools.DeclaredMethod(type, nameof(ITab.FillTab)),
                transpiler: new HarmonyMethod(ReplaceProcessorITabButtons));
        }

        #endregion

        #region Utility methods

        private static object GetProcessStackFromComp(ThingComp comp)
            => advancedResourceProcessorProcessStackField(comp);

        private static object GetProcessStackFromThingWithComps(ThingWithComps thing)
            => GetProcessStackFromComp(GetProcessorCompFromThingWithComps(thing));

        // We could technically use CachedCompAdvancedProcessor, but if the Thing is not spawned it would not be there.
        // It probably should never happen, but you can never be too safe.
        private static ThingComp GetProcessorCompFromThingWithComps(ThingWithComps thing)
            => thing.AllComps.Find(x => advancedResourceProcessorType.IsInstanceOfType(x));

        private static object GetProcessById(IList processes, string id)
            => processes?.Cast<object>().FirstOrDefault(process => processIdField(process) == id);

        #endregion

        #region Minor patches

        private static void PreProcessStackReorder(object process, ref int offset, IList ___processes)
        {
            if (!MP.IsExecutingSyncCommand)
                return;

            // Ensure that index + offset will always fit in range of the list.
            var index = ___processes.IndexOf(process);
            var num = Mathf.Clamp(offset + index, 0, ___processes.Count - 1);
            offset = num - index;
        }

        private static bool PrePasteProcessesGizmo(ThingComp __instance)
        {
            if (!MP.IsInMultiplayer)
                return true;

            var clipboard = processUtilityClipboardField();
            if (!clipboard.Contains(__instance.parent.def))
                return false;

            if (clipboard[__instance.parent.def] is not IList processes || processes.Count == 0)
                return false;

            // Rather than using the processes themselves, grab a list of their defs and target count
            var list = processes.Cast<object>().Select(process => (processDefField(process), processTargetCountField(process))).ToList();
            SyncedPasteProcesses(__instance, list);
            return false;
        }

        #endregion

        #region ITab Patches

        private static void SyncedPasteProcesses(ThingComp comp, List<(Def, int)> clipboard)
        {
            // Just in case
            if (clipboard.Empty())
                return;

            var processStack = GetProcessStackFromComp(comp);
            var processes = processStackProcessesField(processStack);
            // Clear before pasting
            processes.Clear();

            // Add all the pasted processes.
            foreach (var (process, targetCount) in clipboard) 
                processStackAddProcessMethod(processStack, process, comp.parent, targetCount);

            // Probably not needed since the list is cleared? But
            // do the same as the mod and reset the progress to 0.
            foreach (var process in processes)
                processProgressField(process) = 0f;
        }

        private static bool ReplacedPasteProcesses(Rect butRect, Texture2D tex, bool doMouseoverSound, string tooltip)
        {
            var result = Widgets.ButtonImage(butRect, tex, doMouseoverSound, tooltip);
            if (!result || !MP.IsInMultiplayer)
                return result;

            if (Find.Selector.SingleSelectedThing is not ThingWithComps thing)
                return false;

            // Grab the comp. Null shouldn't happen, but let's be safe anyway.
            var comp = GetProcessorCompFromThingWithComps(thing);
            if (comp == null)
                return false;

            var clipboard = processUtilityClipboardField();
            if (!clipboard.Contains(thing.def))
                return false;

            if (clipboard[thing.def] is not IList processes || processes.Count == 0)
                return false;

            // Rather than using the processes themselves, grab a list of their defs and target count
            var list = processes.Cast<object>().Select(process => (processDefField(process), processTargetCountField(process))).ToList();
            SyncedPasteProcesses(comp, list);

            return false;
        }

        private static void SyncedProcessModifyTargetCountButton(ThingComp comp, string processId, int offset)
        {
            var stack = GetProcessStackFromComp(comp);
            var process = GetProcessById(processStackProcessesField(stack), processId);
            if (process == null)
                return;

            ref var targetCount = ref processTargetCountField(process);

            if (targetCount == -1)
            {
                processTargetCountField(process) = 0;
                targetCount = 1;
            }
            else if (targetCount > -1)
            {
                // Ensure that if we're reducing the count that it will be at least 0
                targetCount = Mathf.Max(0, targetCount + offset);
            }
            // No handling for < -1, not intended to ever happen. No handling as a precaution for issues?

            processStackNotifyProcessChangeMethod(stack);
        }

        private static bool ReplacedProcessModifyTargetCountButton(WidgetRow row, Texture2D texture, string tooltip, Color? mouseoverColor, Color? backgroundColor, Color? mouseoverBackgroundColor, bool doMouseoverSound, float overrideSize, object process)
        {
            var result = row.ButtonIcon(texture, tooltip, mouseoverColor, backgroundColor, mouseoverBackgroundColor, doMouseoverSound, overrideSize);
            if (!result || !MP.IsInMultiplayer)
                return result;

            var parent = processParentField(process);
            if (parent == null)
                return false;
            var comp = GetProcessorCompFromThingWithComps(parent);
            if (comp == null)
                return false;

            var id = processIdField(process);
            var offset = GenUI.CurrentAdjustmentMultiplier();
            if (texture == TexButton.Minus)
                offset = -offset;
            SyncedProcessModifyTargetCountButton(comp, id, offset);
            
            SoundDefOf.DragSlider.PlayOneShotOnCamera();
            return false;
        }

        private static void SyncedProcessSuspendButton(ThingComp comp, string processId)
        {
            var stack = GetProcessStackFromComp(comp);
            var process = GetProcessById(processStackProcessesField(stack), processId);
            if (process == null)
                return;

            ref var suspended = ref processSuspendedField(process);
            suspended = !suspended;
            processStackNotifyProcessChangeMethod(stack);
        }

        private static bool ReplacedProcessSuspendButton(Rect butRect, Texture2D tex, Color baseColor, bool doMouseoverSound, string tooltip, object process)
        {
            var result = Widgets.ButtonImage(butRect, tex, baseColor, doMouseoverSound, tooltip);
            if (!result || !MP.IsInMultiplayer)
                return result;

            var parent = processParentField(process);
            if (parent == null)
                return false;
            var comp = GetProcessorCompFromThingWithComps(parent);
            if (comp == null)
                return false;

            var id = processIdField(process);
            SyncedProcessSuspendButton(comp, id);
            
            SoundDefOf.DragSlider.PlayOneShotOnCamera();
            return false;
        }

        private static IEnumerable<CodeInstruction> ReplaceProcessorITabButtons(IEnumerable<CodeInstruction> instr, MethodBase baseMethod)
        {
            var target = AccessTools.DeclaredMethod(typeof(Widgets), nameof(Widgets.ButtonImage),
                [typeof(Rect), typeof(Texture2D), typeof(bool), typeof(string)]);
            var replacement = MpMethodUtil.MethodOf(ReplacedPasteProcesses);

            return instr.ReplaceMethod(target, replacement, baseMethod, expectedReplacements: 1, targetText: "PipeSystem_PasteProcesses");
        }

        private static IEnumerable<CodeInstruction> ReplaceProcessImageButtons(IEnumerable<CodeInstruction> instr, MethodBase baseMethod)
        {
            var widgetRowTarget = AccessTools.DeclaredMethod(typeof(WidgetRow), nameof(WidgetRow.ButtonIcon));
            var widgetRowReplacement = MpMethodUtil.MethodOf(ReplacedProcessModifyTargetCountButton);
            var firstTargetField = AccessTools.DeclaredField(typeof(TexButton), nameof(TexButton.Plus));
            var secondTargetField = AccessTools.DeclaredField(typeof(TexButton), nameof(TexButton.Minus));

            var widgetButtonTarget = AccessTools.DeclaredMethod(typeof(Widgets), nameof(Widgets.ButtonImage),
                [typeof(Rect), typeof(Texture2D), typeof(Color), typeof(bool), typeof(string)]);
            var widgetButtonReplacement = MpMethodUtil.MethodOf(ReplacedProcessSuspendButton);
            var widgetButtonField = AccessTools.DeclaredField(typeof(TexButton), nameof(TexButton.Suspend));

            IEnumerable<CodeInstruction> ExtraInstructions(CodeInstruction _)
            {
                // Load this
                yield return CodeInstruction.LoadArgument(0);
            }

            return instr
                .ReplaceMethod(widgetRowTarget, widgetRowReplacement, baseMethod, extraInstructionsBefore: ExtraInstructions,
                    expectedReplacements: 2, targetInstruction: ci => ci.LoadsField(firstTargetField) || ci.LoadsField(secondTargetField))
                .ReplaceMethod(widgetButtonTarget, widgetButtonReplacement, baseMethod, extraInstructionsBefore: ExtraInstructions,
                    expectedReplacements: 1, targetInstruction: ci => ci.LoadsField(widgetButtonField));
        }

        #endregion

        #region Sync Workers

        private static void SyncProcess(SyncWorker sync, ref object process)
        {
            if (sync.isWriting)
            {
                if (process == null)
                {
                    sync.Write<ThingComp>(null);
                    Log.Error("Trying to sync a null process.");
                    return;
                }

                var parent = GetProcessorCompFromThingWithComps(processParentField(process));
                sync.Write(parent);

                // Probably not needed unless some weird bugs going on
                if (parent == null)
                {
                    Log.Error($"Trying to sync a process with no parent. Process={process}");
                    return;
                }

                sync.Write(processIdField(process));
            }
            else
            {
                var comp = sync.Read<ThingComp>();
                if (comp == null)
                    return;

                var stack = advancedResourceProcessorProcessStackField(comp);
                if (stack == null)
                {
                    Log.Error($"The process has no stack. Comp={comp}");
                    return;
                }

                var list = processStackProcessesField(stack);
                // Shouldn't happen
                if (list == null)
                {
                    Log.Error($"The process has ProcessStack with no process list. Comp={comp}");
                    return;
                }

                var id = sync.Read<string>();
                process = GetProcessById(list, id);
                if (process == null)
                    Log.Error($"Could not find the correct process. Comp={comp}, id={id}");
            }
        }

        #endregion

        #endregion

        #region Abilities

        // CompAbility
        private static Type compAbilitiesType;
        private static AccessTools.FieldRef<object, IEnumerable> learnedAbilitiesField;
        
        // CompAbilityApparel
        private static Type compAbilitiesApparelType;
        private static AccessTools.FieldRef<object, IEnumerable> givenAbilitiesField;
        private static FastInvokeHandler abilityApparelPawnGetter;
        
        // Ability
        private static FastInvokeHandler abilityInitMethod;
        private static AccessTools.FieldRef<object, Thing> abilityHolderField;
        private static AccessTools.FieldRef<object, Pawn> abilityPawnField;
        private static AccessTools.FieldRef<IExposable, int> abilityCooldownField;
        private static ISyncField abilityAutoCastField;

        private static void PatchAbilities()
        {
            PatchingUtilities.SetupAsyncTime();

            // Comp holding ability
            // CompAbility
            compAbilitiesType = AccessTools.TypeByName("VFECore.Abilities.CompAbilities");
            learnedAbilitiesField = AccessTools.FieldRefAccess<IEnumerable>(compAbilitiesType, "learnedAbilities");
            // Unlock ability, user-input use by Vanilla Psycasts Expanded
            MP.RegisterSyncMethod(compAbilitiesType, "GiveAbility");
            // CompAbilityApparel
            compAbilitiesApparelType = AccessTools.TypeByName("VFECore.Abilities.CompAbilitiesApparel");
            givenAbilitiesField = AccessTools.FieldRefAccess<IEnumerable>(compAbilitiesApparelType, "givenAbilities");
            abilityApparelPawnGetter = MethodInvoker.GetHandler(AccessTools.PropertyGetter(compAbilitiesApparelType, "Pawn"));
            //MP.RegisterSyncMethod(compAbilitiesApparelType, "Initialize");

            // Ability itself
            var type = AccessTools.TypeByName("VFECore.Abilities.Ability");

            abilityInitMethod = MethodInvoker.GetHandler(AccessTools.Method(type, "Init"));
            abilityHolderField = AccessTools.FieldRefAccess<Thing>(type, "holder");
            abilityPawnField = AccessTools.FieldRefAccess<Pawn>(type, "pawn");
            abilityCooldownField = AccessTools.FieldRefAccess<int>(type, "cooldown");
            // There's another method taking LocalTargetInfo. Harmony grabs the one we need, but just in case specify the types to avoid ambiguity.
            MP.RegisterSyncMethod(type, "StartAbilityJob", [typeof(GlobalTargetInfo[])])
                // Need to transform arguments to properly handle multiple maps
                .TransformArgument(0, Serializer.New(
                    (GlobalTargetInfo[] targets)
                        => targets.Select(x => (
                            thingId: x.thingInt?.thingIDNumber ?? -1,
                            tileId: x.tileInt,
                            worldObjectId: x.worldObjectInt?.ID ?? -1,
                            cell: x.cellInt,
                            mapId: x.mapInt?.uniqueID ?? -1)).ToList(),
                    result => result.Select(x =>
                    {
                        var (thingId, tileId, worldObjectId, cell, mapId) = x;
                        if (thingId != -1)
                        {
                            if (MP.TryGetThingById(thingId, out var thing))
                                return new GlobalTargetInfo(thing);
                        }
                        else if (tileId != -1)
                            return new GlobalTargetInfo(tileId);
                        else if (worldObjectId != -1)
                        {
                            var worldObject = Find.World.worldObjects.AllWorldObjects.Find(w => w.ID == worldObjectId) ??
                                      Find.World.pocketMaps.Find(p => p.ID == worldObjectId);
                            if (worldObject != null)
                                return new GlobalTargetInfo(worldObject);
                        }
                        else if (cell.IsValid)
                            return new GlobalTargetInfo(cell, Find.Maps.FirstOrDefault(x => x.uniqueID == mapId), true);
                        return GlobalTargetInfo.Invalid;
                    }).ToArray()));
            MP.RegisterSyncWorker<ITargetingSource>(SyncVEFAbility, type, true);
            abilityAutoCastField = MP.RegisterSyncField(type, "autoCast");
            MpCompat.harmony.Patch(AccessTools.DeclaredMethod(type, "DoAction"),
                prefix: new HarmonyMethod(typeof(VanillaExpandedFramework), nameof(PreAbilityDoAction)),
                postfix: new HarmonyMethod(typeof(VanillaExpandedFramework), nameof(PostAbilityDoAction)));

            foreach (var target in type.AllSubclasses().Concat(type))
            {
                // Fix timestamp.
                // We really could use implicit fixers, so we don't have to register one fixer per ability type.
                if (!target.IsAbstract)
                    PatchingUtilities.RegisterTimestampFixer(target, MpMethodUtil.MethodOf(FixAbilityTimestamp));

                // We need to set this up in all subtypes that override GetGizmo, as we need to make sure
                // that the call to Command_Ability constructor has the proper time. We could patch the
                // constructor of all subtypes of Command_Ability, but the issue with that is that the
                // gizmo are not guaranteed to be of that specific type. On top of that, their arguments
                // (which we'd need to use to setup the time snapshot) may have different names, and may
                // be in a different order. It's just simpler to patch this specific method for simplicity sake,
                // instead of trying to patch every relevant constructor.
                var method = AccessTools.DeclaredMethod(target, "GetGizmo");
                if (method != null)
                {
                    MpCompat.harmony.Patch(method,
                        prefix: new HarmonyMethod(MpMethodUtil.MethodOf(PreGetGizmo)),
                        finalizer: new HarmonyMethod(MpMethodUtil.MethodOf(RestoreProperTimeSnapshot)));
                }
            }

            type = AccessTools.TypeByName("VFECore.CompShieldField");
            MpCompat.RegisterLambdaMethod(type, nameof(ThingComp.CompGetWornGizmosExtra), 0);
            MpCompat.RegisterLambdaMethod(type, "GetGizmos", 0, 2);

            // Time snapshot fix for gizmo itself
            type = AccessTools.TypeByName("VFECore.Abilities.Command_Ability");
            foreach (var targetType in type.AllSubclasses().Concat(type))
            {
                var method = AccessTools.DeclaredMethod(targetType, nameof(Command.GizmoOnGUIInt));
                if (method != null)
                {
                    MpCompat.harmony.Patch(method,
                        prefix: new HarmonyMethod(MpMethodUtil.MethodOf(PreAbilityGizmoGui)),
                        finalizer: new HarmonyMethod(MpMethodUtil.MethodOf(RestoreProperTimeSnapshot)));
                }
            }
        }

        private static void SyncVEFAbility(SyncWorker sync, ref ITargetingSource source)
        {
            if (sync.isWriting)
            {
                sync.Write(abilityHolderField(source));
                sync.Write(source.GetVerb.GetUniqueLoadID());
            }
            else
            {
                var holder = sync.Read<Thing>();
                var uid = sync.Read<string>();
                if (holder is ThingWithComps thing)
                {
                    IEnumerable list = null;

                    var compAbilities = thing.AllComps.FirstOrDefault(c => compAbilitiesType.IsInstanceOfType(c));
                    ThingComp compAbilitiesApparel = null;
                    if (compAbilities != null)
                        list = learnedAbilitiesField(compAbilities);

                    if (list == null)
                    {
                        compAbilitiesApparel = thing.AllComps.FirstOrDefault(c => compAbilitiesApparelType.IsInstanceOfType(c));
                        if (compAbilitiesApparel != null)
                            list = givenAbilitiesField(compAbilitiesApparel);
                    }

                    if (list != null)
                    {
                        foreach (var o in list)
                        {
                            var its = o as ITargetingSource;
                            if (its?.GetVerb.GetUniqueLoadID() == uid)
                            {
                                source = its;
                                break;
                            }
                        }

                        if (source != null && compAbilitiesApparel != null)
                        {
                            // Set the pawn and initialize the Ability, as it might have been skipped
                            var pawn = abilityApparelPawnGetter(compAbilitiesApparel, Array.Empty<object>()) as Pawn;
                            abilityPawnField(source) = pawn;
                            abilityInitMethod(source, Array.Empty<object>());
                        }
                    }
                    else
                    {
                        Log.Error("MultiplayerCompat :: SyncVEFAbility : Holder is missing or of unsupported type");
                    }
                }
                else
                {
                    Log.Error("MultiplayerCompat :: SyncVEFAbility : Holder isn't a ThingWithComps");
                }
            }
        }

        private static void PreAbilityDoAction(object __instance)
        {
            if (!MP.IsInMultiplayer)
                return;

            MP.WatchBegin();
            abilityAutoCastField.Watch(__instance);
        }

        private static void PostAbilityDoAction()
        {
            if (!MP.IsInMultiplayer)
                return;

            MP.WatchEnd();
        }

        // We need to set the time snapshot when constructing the gizmo since it's disabled in
        // the constructor, meaning that it will be incorrectly disabled if we don't do it.
        private static void PreGetGizmo(object __instance, out PatchingUtilities.TimeSnapshot? __state)
            => __state = SetTemporaryTimeSnapshot(__instance);

        // We need to set the time snapshot when drawing the gizmo GUI, as otherwise it'll
        // display completely incorrect values for cooldown or will allow for the ability
        // usage while it should still be on cooldown.
        public static void PreAbilityGizmoGui(object ___ability, out PatchingUtilities.TimeSnapshot? __state)
            => __state = SetTemporaryTimeSnapshot(___ability);

        private static PatchingUtilities.TimeSnapshot? SetTemporaryTimeSnapshot(object ability)
        {
            if (!MP.IsInMultiplayer)
                return null;

            var target = abilityPawnField(ability) ?? abilityHolderField(ability);
            if (target?.Map == null)
                return null;

            return PatchingUtilities.TimeSnapshot.GetAndSetFromMap(target.Map);
        }

        public static void RestoreProperTimeSnapshot(PatchingUtilities.TimeSnapshot? __state)
            => __state?.Set();

        private static ref int FixAbilityTimestamp(IExposable ability)
            => ref abilityCooldownField(ability);

        #endregion

        #region Hireable Factions

        // Dialog_Hire
        private static Type hireDialogType;
        private static AccessTools.FieldRef<object, Dictionary<PawnKindDef, Pair<int, string>>> hireDataField;
        private static ISyncField daysAmountField;
        private static ISyncField currentFactionDefField;
        // Dialog_ContractInfo
        private static Type contractInfoDialogType;
        // HireableSystemStaticInitialization
        private static AccessTools.FieldRef<IList> hireablesList;

        private static void PatchHireableFactions()
        {
            hireDialogType = AccessTools.TypeByName("VFECore.Misc.Dialog_Hire");

            DialogUtilities.RegisterDialogCloseSync(hireDialogType, true);
            MP.RegisterSyncMethod(hireDialogType, nameof(Window.OnAcceptKeyPressed));
            MP.RegisterSyncWorker<Window>(SyncHireDialog, hireDialogType);
            MP.RegisterSyncMethod(typeof(VanillaExpandedFramework), nameof(SyncedSetHireData));
            hireDataField = AccessTools.FieldRefAccess<Dictionary<PawnKindDef, Pair<int, string>>>(hireDialogType, "hireData");

            // I don't think daysAmountBuffer needs to be synced, just daysAmount only
            daysAmountField = MP.RegisterSyncField(hireDialogType, "daysAmount");
            currentFactionDefField = MP.RegisterSyncField(hireDialogType, "curFaction");
            MpCompat.harmony.Patch(AccessTools.Method(hireDialogType, nameof(Window.DoWindowContents)),
                prefix: new HarmonyMethod(typeof(VanillaExpandedFramework), nameof(PreHireDialogDoWindowContents)),
                postfix: new HarmonyMethod(typeof(VanillaExpandedFramework), nameof(PostHireDialogDoWindowContents)));

            // There seems to be a 50/50 chance trying to open hiring window will fail and cause an error
            // this is here to fix that issue
            var type = AccessTools.TypeByName("VFECore.Misc.Hireable");
            MP.RegisterSyncWorker<object>(SyncHireable, type);
            MpCompat.RegisterLambdaDelegate(type, "CommFloatMenuOption", 0);

            hireablesList = AccessTools.StaticFieldRefAccess<IList>(AccessTools.Field(AccessTools.TypeByName("VFECore.Misc.HireableSystemStaticInitialization"), "Hireables"));

            contractInfoDialogType = AccessTools.TypeByName("VFECore.Misc.Dialog_ContractInfo");

            DialogUtilities.RegisterDialogCloseSync(contractInfoDialogType, true);
            MP.RegisterSyncWorker<Window>(SyncContractInfoDialog, contractInfoDialogType);
            MpCompat.RegisterLambdaMethod(contractInfoDialogType, "DoWindowContents", 0);

            MpCompat.RegisterLambdaDelegate("VFECore.HiringContractTracker", "CommFloatMenuOption", 0);
        }

        private static void SyncHireDialog(SyncWorker sync, ref Window dialog)
        {
            // The dialog should just be open
            if (!sync.isWriting)
                dialog = Find.WindowStack.Windows.FirstOrDefault(x => x.GetType() == hireDialogType);
        }

        private static void PreHireDialogDoWindowContents(Window __instance, Dictionary<PawnKindDef, Pair<int, string>> ___hireData, ref Dictionary<PawnKindDef, Pair<int, string>> __state)
        {
            if (!MP.IsInMultiplayer)
                return;

            MP.WatchBegin();
            daysAmountField.Watch(__instance);
            currentFactionDefField.Watch(__instance);

            __state = ___hireData.ToDictionary(x => x.Key, x => x.Value);
        }

        private static void PostHireDialogDoWindowContents(Window __instance, Dictionary<PawnKindDef, Pair<int, string>> ___hireData, Dictionary<PawnKindDef, Pair<int, string>> __state)
        {
            if (!MP.IsInMultiplayer)
                return;

            MP.WatchEnd();

            foreach (var (pawn, value) in __state)
            {
                if (value.First != ___hireData[pawn].First)
                {
                    hireDataField(__instance) = __state;
                    SyncedSetHireData(___hireData);
                    break;
                }
            }
        }

        private static void SyncedSetHireData(Dictionary<PawnKindDef, Pair<int, string>> hireData)
        {
            var dialog = Find.WindowStack.Windows.FirstOrDefault(x => x.GetType() == hireDialogType);

            if (dialog != null)
                hireDataField(dialog) = hireData;
        }

        private static void SyncContractInfoDialog(SyncWorker sync, ref Window window)
        {
            if (!sync.isWriting)
                window = Find.WindowStack.windows.FirstOrDefault(x => x.GetType() == contractInfoDialogType);
        }

        private static void SyncHireable(SyncWorker sync, ref object obj)
        {
            if (sync.isWriting)
                sync.Write(hireablesList().IndexOf(obj));
            else
                obj = hireablesList()[sync.Read<int>()];
        }

        #endregion

        #region Vanilla Furniture Expanded

        // Vanilla Furniture Expanded
        private static Type randomBuildingGraphicCompType;
        private static FastInvokeHandler randomBuildingGraphicCompChangeGraphicMethod;

        // Glowers
        private static Type dummyGlowerType;
        private static AccessTools.FieldRef<ThingComp, CompGlower> compGlowerExtendedGlowerField;
        private static AccessTools.FieldRef<ThingWithComps, ThingComp> dummyGlowerParentCompField;

        private static void PatchVanillaFurnitureExpanded()
        {
            MpCompat.RegisterLambdaMethod("VanillaFurnitureExpanded.CompConfigurableSpawner", "CompGetGizmosExtra", 0).SetDebugOnly();

            var type = AccessTools.TypeByName("VanillaFurnitureExpanded.Command_SetItemsToSpawn");
            MpCompat.RegisterLambdaDelegate(type, "ProcessInput", 1);
            MP.RegisterSyncWorker<Command>(SyncCommandWithCompBuilding, type, shouldConstruct: true);

            MpCompat.RegisterLambdaMethod("VanillaFurnitureExpanded.CompRockSpawner", "CompGetGizmosExtra", 0);

            type = AccessTools.TypeByName("VanillaFurnitureExpanded.Command_SetStoneType");
            MP.RegisterSyncWorker<Command>(SyncCommandWithCompBuilding, type, shouldConstruct: true);
            MpCompat.RegisterLambdaDelegate(type, "ProcessInput", 1);

            type = randomBuildingGraphicCompType = AccessTools.TypeByName("VanillaFurnitureExpanded.CompRandomBuildingGraphic");
            randomBuildingGraphicCompChangeGraphicMethod = MethodInvoker.GetHandler(AccessTools.DeclaredMethod(type, "ChangeGraphic"));
            MpCompat.RegisterLambdaMethod(type, "CompGetGizmosExtra", 0);

            // Preferably leave it at the end in case it fails - if it fails all the other stuff here will still get patched
            type = AccessTools.TypeByName("VanillaFurnitureExpanded.Dialog_ChooseGraphic");
            MpCompat.harmony.Patch(AccessTools.DeclaredMethod(type, "DoWindowContents"),
                transpiler: new HarmonyMethod(typeof(VanillaExpandedFramework), nameof(Dialog_ChooseGraphic_ReplaceSelectionButton)));
            MP.RegisterSyncMethod(typeof(VanillaExpandedFramework), nameof(Dialog_ChooseGraphic_SyncChange));

            // Glowers
            type = AccessTools.TypeByName("VanillaFurnitureExpanded.CompGlowerExtended");
            MP.RegisterSyncMethod(type, "SwitchColor");
            compGlowerExtendedGlowerField = AccessTools.FieldRefAccess<CompGlower>(type, "compGlower");
            // Inner method of CompGlowerExtended
            type = AccessTools.Inner(type, "CompGlower_SetGlowColorInternal_Patch");
            PatchingUtilities.PatchCancelInInterface(AccessTools.DeclaredMethod(type, "Postfix"));

            type = dummyGlowerType = AccessTools.TypeByName("VanillaFurnitureExpanded.DummyGlower");
            dummyGlowerParentCompField = AccessTools.FieldRefAccess<ThingComp>(type, "parentComp");

            // Syncing of wall-light type of glower doesn't work with MP, as what they do
            // is spawning a dummy thing, attaching the glower to it, followed by despawning
            // the dummy thing. If something is not spawned and doesn't have a holder MP won't
            // be able to sync it properly due to it missing parent/map it's attached to,
            // and will report it as inaccessible. This works as a workaround to all of this.
            // 
            // We normally sync CompGlower as ThingComp. If we add an explicit sync worker
            // for it, then we'll (basically) replace the vanilla worker for it. It'll be
            // used in situations where we're trying to sync CompGlower directly instead of ThingComp.
            // Can't be synced with `isImplicit: true`, as it'll cause it to sync it with ThingComp
            // sync worker first before syncing it using this specific sync worker.
            MP.RegisterSyncWorker<CompGlower>(SyncCompGlower);
        }

        private static bool Dialog_ChooseGraphic_ReplacementButton(Rect butRect, bool doMouseoverSound, Thing thingToChange, int index, Window window)
        {
            var result = Widgets.ButtonInvisible(butRect, doMouseoverSound);
            if (!MP.IsInMultiplayer || !result)
                return result;

            window.Close();

            Dialog_ChooseGraphic_SyncChange(index, thingToChange,
                // Filter the comps before syncing them
                Find.Selector.SelectedObjects
                    .OfType<ThingWithComps>()
                    .Where(thing => thing.def == thingToChange.def)
                    .Select(thing => thing.AllComps.FirstOrDefault(x => x.GetType() == randomBuildingGraphicCompType))
                    .Where(comp => comp != null));

            return false;
        }

        private static void Dialog_ChooseGraphic_SyncChange(int index, Thing thingToChange, IEnumerable<ThingComp> compsToChange)
        {
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                foreach (var comp in compsToChange)
                    randomBuildingGraphicCompChangeGraphicMethod(comp, false, index, false);
            });

            thingToChange.DirtyMapMesh(thingToChange.Map);
        }

        private static IEnumerable<CodeInstruction> Dialog_ChooseGraphic_ReplaceSelectionButton(IEnumerable<CodeInstruction> instr)
        {
            // Technically no need to replace specify the argument types, but including them just in case another method with same name gets added in the future
            var targetMethod = AccessTools.DeclaredMethod(typeof(Widgets), nameof(Widgets.ButtonInvisible), new[] { typeof(Rect), typeof(bool) });
            var replacementMethod = AccessTools.DeclaredMethod(typeof(VanillaExpandedFramework), nameof(Dialog_ChooseGraphic_ReplacementButton));

            var type = AccessTools.TypeByName("VanillaFurnitureExpanded.Dialog_ChooseGraphic");
            var thingToChangeField = AccessTools.DeclaredField(type, "thingToChange");
            FieldInfo indexField = null;

            foreach (var ci in instr)
            {
                if (indexField == null && (ci.opcode == OpCodes.Ldfld || ci.opcode == OpCodes.Stfld) && ci.operand is FieldInfo { Name: "i" } field)
                    indexField = field;

                if (ci.opcode == OpCodes.Call && ci.operand is MethodInfo method && method == targetMethod && indexField != null)
                {
                    ci.operand = replacementMethod;

                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldfld, thingToChangeField); // Load in the "thingToChange" (Thing) field
                    yield return new CodeInstruction(OpCodes.Ldloc_S, 5); // Load in the local of type "<>c__DisplayClass9_0"
                    yield return new CodeInstruction(OpCodes.Ldfld, indexField); // Load in the "i" (int) from the nested type
                    yield return new CodeInstruction(OpCodes.Ldarg_0); // Load in the instance (Dialog_ChooseGraphic)
                }

                yield return ci;
            }
        }

        private static void SyncCompGlower(SyncWorker sync, ref CompGlower glower)
        {
            if (sync.isWriting)
            {
                // Check if the glower's parent thing is of DummyGlower type
                if (dummyGlowerType.IsInstanceOfType(glower.parent))
                {
                    sync.Write(true);
                    // Sync the CompGlowerExtended
                    sync.Write(dummyGlowerParentCompField(glower.parent));
                }
                else
                {
                    // Handle this as normal ThingComp, letting MP sync it
                    sync.Write(false);
                    sync.Write<ThingComp>(glower);
                }
            }
            else
            {
                // Check if we're reading a normal glower or a glower
                // with a VFE dummy parent and handle appropriately.
                if (sync.Read<bool>())
                    glower = compGlowerExtendedGlowerField(sync.Read<ThingComp>());
                else
                    glower = sync.Read<ThingComp>() as CompGlower;
            }
        }

        #endregion

        #region MVCF

        // MVCF //
        // Core
        private static IEnumerable<string> mvcfEnabledFeaturesSet;

        // VerbManager
        private static FastInvokeHandler mvcfVerbManagerPawnGetter;
        private static FastInvokeHandler mvcfVerbManagerTickMethod;
        private static AccessTools.FieldRef<object, IList> mvcfVerbManagerVerbsField;

        // PawnVerbUtility
        private delegate object GetManager(Pawn p, bool createIfMissing);
        private static GetManager mvcfPawnVerbUtilityGetManager;

        // ManagedVerb
        private static FastInvokeHandler mvcfManagedVerbManagerGetter;

        // VerbWithComps
        private static AccessTools.FieldRef<object, IList> mvcfVerbWithCompsField;

        // VerbComp
        private static AccessTools.FieldRef<object, object> mvcfVerbCompParentField;

        // WorldComponent_MVCF
        private static AccessTools.FieldRef<WorldComponent> mvcfWorldCompInstanceField;
        private static AccessTools.FieldRef<WorldComponent, IList> mvcfWorldCompTickManagersField;

        // WeakReference<VerbManager>
        private static FastInvokeHandler mvcfWeakReferenceTryGetVerbManagerMethod;

        private static void PatchMVCF()
        {
            PatchingUtilities.SetupAsyncTime();

            MpCompat.harmony.Patch(AccessTools.Method(typeof(Pawn), nameof(Pawn.SpawnSetup)),
                postfix: new HarmonyMethod(typeof(VanillaExpandedFramework), nameof(EverybodyGetsVerbManager)));

            var type = AccessTools.TypeByName("MVCF.MVCF");
            // HashSet<string>, using it as IEnumerable<string> for a bit of extra safety in case it ever gets changed to a list or something.
            mvcfEnabledFeaturesSet = AccessTools.Field(type, "EnabledFeatures").GetValue(null) as IEnumerable<string>;
            if (mvcfEnabledFeaturesSet == null)
                Log.Warning("Cannot access the list of enabled MVCF features, this may cause issues");

            type = AccessTools.TypeByName("MVCF.Utilities.PawnVerbUtility");
            mvcfPawnVerbUtilityGetManager = AccessTools.MethodDelegate<GetManager>(AccessTools.Method(type, "Manager"));

            type = AccessTools.TypeByName("MVCF.VerbManager");
            MP.RegisterSyncWorker<object>(SyncVerbManager, type, isImplicit: true);
            mvcfVerbManagerPawnGetter = MethodInvoker.GetHandler(AccessTools.PropertyGetter(type, "Pawn"));
            mvcfVerbManagerTickMethod = MethodInvoker.GetHandler(AccessTools.DeclaredMethod(type, "Tick"));
            mvcfVerbManagerVerbsField = AccessTools.FieldRefAccess<IList>(type, "verbs");

            type = typeof(System.WeakReference<>).MakeGenericType(type);
            mvcfWeakReferenceTryGetVerbManagerMethod = MethodInvoker.GetHandler(AccessTools.DeclaredMethod(type, "TryGetTarget"));

            type = AccessTools.TypeByName("MVCF.ManagedVerb");
            mvcfManagedVerbManagerGetter = MethodInvoker.GetHandler(AccessTools.PropertyGetter(type, "Manager"));
            MP.RegisterSyncWorker<object>(SyncManagedVerb, type, isImplicit: true);
            // Seems like selecting the Thing that holds the verb inits some stuff, so we need to set the context
            MP.RegisterSyncMethod(type, "Toggle");

            type = AccessTools.TypeByName("MVCF.VerbWithComps");
            mvcfVerbWithCompsField = AccessTools.FieldRefAccess<IList>(type, "comps");

            type = AccessTools.TypeByName("MVCF.VerbComps.VerbComp");
            mvcfVerbCompParentField = AccessTools.FieldRefAccess<object>(type, "parent");
            MP.RegisterSyncWorker<object>(SyncVerbComp, type, isImplicit: true);

            type = AccessTools.TypeByName("MVCF.VerbComps.VerbComp_Switch");
            // Switch used verb
            MP.RegisterSyncMethod(type, "Enable");

            type = AccessTools.TypeByName("MVCF.Reloading.Comps.VerbComp_Reloadable_ChangeableAmmo");
            var innerMethod = MpMethodUtil.GetLambda(type, "AmmoOptions", MethodType.Getter, null, 1);
            MP.RegisterSyncDelegate(type, innerMethod.DeclaringType.Name, innerMethod.Name);

            type = AccessTools.TypeByName("MVCF.PatchSets.PatchSet_HumanoidGizmos");
            MpCompat.RegisterLambdaDelegate(type, "GetGizmos_Postfix", 3); // Toggle fire at will
            MpCompat.RegisterLambdaDelegate(type, "GetAttackGizmos_Postfix", 4); // Interrupt Attack

            MpCompat.RegisterLambdaDelegate("MVCF.PatchSets.PatchSet_Animals", "Pawn_GetGizmos_Postfix", 0); // Also interrupt Attack

            // Changes the verb, so when called before syncing (especially if the original method is canceled by another mod) - will cause issues.
            PatchingUtilities.PatchCancelInInterfaceSetResultToTrue("MVCF.PatchSets.PatchSet_MultiVerb:Prefix_OrderForceTarget");

            // Verb ticking
            type = AccessTools.TypeByName("MVCF.WorldComponent_MVCF");
            mvcfWorldCompInstanceField = AccessTools.StaticFieldRefAccess<WorldComponent>(AccessTools.DeclaredField(type, "Instance"));
            mvcfWorldCompTickManagersField = AccessTools.FieldRefAccess<IList>(type, "TickManagers");

            MpCompat.harmony.Patch(AccessTools.DeclaredMethod(type, nameof(WorldComponent.WorldComponentTick)),
                transpiler: new HarmonyMethod(typeof(VanillaExpandedFramework), nameof(ReplaceTickWithConditionalTick)));
            MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(MapComponentUtility), nameof(MapComponentUtility.MapComponentTick)),
                postfix: new HarmonyMethod(typeof(VanillaExpandedFramework), nameof(TickVerbManagersForMap)));
        }

        // Initialize the VerbManager early, we expect it to exist on every player.
        private static void EverybodyGetsVerbManager(Pawn __instance)
        {
            // No point in doing this out of MP
            if (!MP.IsInMultiplayer)
                return;

            // In the unlikely case the feature set we got is null, we'll let it run anyway just in case.
            if (mvcfEnabledFeaturesSet == null)
            {
                try
                {
                    mvcfPawnVerbUtilityGetManager(__instance, true);
                }
                catch (NullReferenceException)
                {
                    // Ignored
                }
            }
            // If none of the features is enabled, there's not really any point in using the managers.
            else if (mvcfEnabledFeaturesSet.Any())
                mvcfPawnVerbUtilityGetManager(__instance, true);
        }

        private static void SyncVerbManager(SyncWorker sync, ref object obj)
        {
            if (sync.isWriting)
                // Sync the pawn that has the VerbManager
                sync.Write((Pawn)mvcfVerbManagerPawnGetter(obj, Array.Empty<object>()));
            else
            {
                var pawn = sync.Read<Pawn>();

                // Either try getting the VerbManager from the comp, or create it if it's missing
                obj = mvcfPawnVerbUtilityGetManager(pawn, true);
                if (obj == null)
                    throw new Exception($"MpCompat :: VerbManager of {pawn} isn't initialized! NO WAY!");
            }
        }

        private static void SyncManagedVerb(SyncWorker sync, ref object obj)
        {
            if (sync.isWriting)
            {
                // Get the VerbManager from inside of the ManagedVerb itself
                var verbManager = mvcfManagedVerbManagerGetter(obj);
                // Find the ManagedVerb inside of list of all verbs
                var managedVerbsList = mvcfVerbManagerVerbsField(verbManager);
                var index = managedVerbsList.IndexOf(obj);

                // Sync the index of the verb as well as the manager (if it's valid)
                sync.Write(index);
                if (index >= 0)
                    SyncVerbManager(sync, ref verbManager);
            }
            else
            {
                // Read and check if the index is valid
                var index = sync.Read<int>();

                if (index >= 0)
                {
                    // Read the verb manager
                    object verbManager = null;
                    SyncVerbManager(sync, ref verbManager);

                    // Find the ManagedVerb with specific index inside of list of all verbs
                    var managedVerbsList = mvcfVerbManagerVerbsField(verbManager);
                    obj = managedVerbsList[index];
                }
            }
        }

        private static void SyncVerbComp(SyncWorker sync, ref object verbComp)
        {
            object verb = null;

            if (sync.isWriting)
            {
                verb = mvcfVerbCompParentField(verbComp);
                var index = mvcfVerbWithCompsField(verb).IndexOf(verbComp);

                SyncManagedVerb(sync, ref verb);
                sync.Write(index);
            }
            else
            {
                SyncManagedVerb(sync, ref verb);
                var index = sync.Read<int>();

                if (index >= 0)
                {
                    verbComp = mvcfVerbWithCompsField(verb)[index];
                }
            }
        }

        private static void TickVerbManagersForMap(Map map)
        {
            // Map-specific ticking is only enabled in MP with async on.
            if (!MP.IsInMultiplayer || !PatchingUtilities.IsAsyncTime)
                return;

            var managers = mvcfWorldCompTickManagersField(mvcfWorldCompInstanceField());
            // Null or empty check
            if (managers is not { Count: > 0 })
                return;

            // out parameter
            var args = new object[1];
            foreach (var weakRef in managers)
            {
                if ((bool)mvcfWeakReferenceTryGetVerbManagerMethod(weakRef, args))
                {
                    var manager = args[0];
                    if (mvcfVerbManagerPawnGetter(manager) is Pawn pawn && pawn.MapHeld == map)
                        mvcfVerbManagerTickMethod(manager);
                }
            }
        }

        private static void TickOnlyNonMapManagers(object manager)
        {
            // Normal ticking (tied to world). Only do if not in MP, async is off,
            // the pawn is null (shouldn't happen?) or the pawn has no map.
            if (!MP.IsInMultiplayer ||
                !PatchingUtilities.IsAsyncTime ||
                mvcfVerbManagerPawnGetter(manager) is not Pawn pawn ||
                pawn.MapHeld == null)
            {
                mvcfVerbManagerTickMethod(manager);
            }
        }

        private static IEnumerable<CodeInstruction> ReplaceTickWithConditionalTick(IEnumerable<CodeInstruction> instr, MethodBase baseMethod)
        {
            var target = AccessTools.DeclaredMethod("MVCF.VerbManager:Tick", Type.EmptyTypes);
            var replacement = AccessTools.DeclaredMethod(typeof(VanillaExpandedFramework), nameof(TickOnlyNonMapManagers));
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
                Log.Warning($"Patched incorrect number of VerbManager.Tick calls (patched {replacedCount}, expected {expected}) for method {name}");
            }
        }

        #endregion

        #region Pipe System

        // Deconstruct
        private static Type deconstructPipeDesignatorType;
        private static AccessTools.FieldRef<Designator_Deconstruct, Def> deconstructPipeDesignatorNetDefField;
        // Pipe net manager
        private static Type pipeNetManagerType;
        private static AccessTools.FieldRef<MapComponent, IList> pipeNetManagerPipeNetsListField;
        // Pipe net
        private static AccessTools.FieldRef<object, Map> pipeNetMapField;
        private static AccessTools.FieldRef<object, Def> pipeNetDefField;

        private static void PatchPipeSystem()
        {
            // Increase/decrease by 1/10
            MpCompat.RegisterLambdaMethod("PipeSystem.CompConvertToThing", "PostSpawnSetup", 0, 1, 2, 3);
            // (Dev) trigger countdown
            MpCompat.RegisterLambdaMethod("PipeSystem.CompExplosiveContent", "CompGetGizmosExtra", 0).SetDebugOnly();
            // Choose output
            MpCompat.RegisterLambdaMethod("PipeSystem.CompResourceProcessor", "PostSpawnSetup", 1);
            // Extract resource (0), toggle allow manual refill (2), transfer to other containers (3)
            MpCompat.RegisterLambdaMethod("PipeSystem.CompResourceStorage", "PostSpawnSetup", 0, 2, 3);
            // (Dev) fill/add 5/empty
            MpCompat.RegisterLambdaMethod("PipeSystem.CompResourceStorage", "CompGetGizmosExtra", 0, 1, 2).SetDebugOnly();
            // Spawn resource now
            MpCompat.RegisterLambdaMethod("PipeSystem.CompSpawnerOrNet", "CompGetGizmosExtra", 0).SetDebugOnly();

            // Designator
            var type = deconstructPipeDesignatorType = AccessTools.TypeByName("PipeSystem.Designator_DeconstructPipe");
            deconstructPipeDesignatorNetDefField = AccessTools.FieldRefAccess<Def>(type, "pipeNetDef");
            MP.RegisterSyncWorker<Designator_Deconstruct>(SyncDeconstructPipeDesignator, type);

            // Pipe net
            type = pipeNetManagerType = AccessTools.TypeByName("PipeSystem.PipeNetManager");
            pipeNetManagerPipeNetsListField = AccessTools.FieldRefAccess<IList>(type, "pipeNets");

            type = AccessTools.TypeByName("PipeSystem.PipeNet");
            pipeNetMapField = AccessTools.FieldRefAccess<Map>(type, "map");
            pipeNetDefField = AccessTools.FieldRefAccess<Def>(type, "def");
            MP.RegisterSyncWorker<object>(SyncPipeNet, type, true);
        }

        private static void SyncDeconstructPipeDesignator(SyncWorker sync, ref Designator_Deconstruct designator)
        {
            if (sync.isWriting)
                sync.Write(deconstructPipeDesignatorNetDefField(designator));
            else
                designator = (Designator_Deconstruct)Activator.CreateInstance(deconstructPipeDesignatorType, sync.Read<Def>());
        }

        private static void SyncPipeNet(SyncWorker sync, ref object pipeNet)
        {
            if (sync.isWriting)
            {
                // Sync null net as -1
                if (pipeNet == null)
                {
                    sync.Write(-1);
                    return;
                }

                // No map, can't get manager - log error and treat as null
                var map = pipeNetMapField(pipeNet);
                if (map == null)
                {
                    Log.Error($"Trying to sync a PipeNet with a null map. PipeNet={pipeNet}");
                    sync.Write(-1);
                    return;
                }

                // No manager for map, shouldn't happen - log error and treat as null
                var manager = map.GetComponent(pipeNetManagerType);
                if (manager == null)
                {
                    Log.Error($"Trying to sync a PipeNet with a map that doesn't have PipeNetManager. PipeNet={pipeNet}, Map={map}");
                    sync.Write(-1);
                    return;
                }

                var def = pipeNetDefField(pipeNet);
                var list = pipeNetManagerPipeNetsListField(manager);
                var index = -1;
                var found = false;

                foreach (var currentPipeNet in list)
                {
                    if (def == pipeNetDefField(currentPipeNet))
                    {
                        index++;
                        if (pipeNet == currentPipeNet)
                        {
                            found = true;
                            break;
                        }
                    }
                }

                if (!found)
                {
                    // We did not find the pipe net - log error and treat as null
                    Log.Error($"Trying to sync a PipeNet, but it's not held by the manager. PipeNet={pipeNet}, map={map}, manager={manager}");
                    sync.Write(-1);
                }
                else
                {
                    sync.Write(index);
                    sync.Write(def);
                    sync.Write(manager);
                }
            }
            else
            {
                var index = sync.Read<int>();
                // If negative - it's null
                if (index < 0)
                    return;

                var def = sync.Read<Def>();
                var manager = sync.Read<MapComponent>();
                var list = pipeNetManagerPipeNetsListField(manager);
                var currentIndex = 0;

                foreach (var currentPipeNet in list)
                {
                    if (def == pipeNetDefField(currentPipeNet))
                    {
                        if (currentIndex == index)
                        {
                            pipeNet = currentPipeNet;
                            break;
                        }

                        currentIndex++;
                    }
                }
            }
        }

        #endregion

        #region Faction Discovery

        // Dialog_NewFactionSpawning
        private static Type newFactionSpawningDialogType;
        private static AccessTools.FieldRef<object, FactionDef> factionDefField;

        private static void PatchFactionDiscovery()
        {
            newFactionSpawningDialogType = AccessTools.TypeByName("VFECore.Dialog_NewFactionSpawning");
            factionDefField = AccessTools.FieldRefAccess<FactionDef>(newFactionSpawningDialogType, "factionDef");

            MP.RegisterSyncMethod(MpMethodUtil.GetLocalFunc(newFactionSpawningDialogType, "SpawnWithBases", localFunc: "SpawnCallback"));
            MP.RegisterSyncMethod(newFactionSpawningDialogType, "SpawnWithoutBases");
            MP.RegisterSyncMethod(newFactionSpawningDialogType, "Ignore");
            MP.RegisterSyncWorker<Window>(SyncFactionDiscoveryDialog, newFactionSpawningDialogType);

            // This will only open the dialog for host only on game load, but will
            // allow other players to access it from the mod settings.
            var type = AccessTools.TypeByName("VFECore.Patch_GameComponentUtility");
            type = AccessTools.Inner(type, "LoadedGame");
            MpCompat.harmony.Patch(AccessTools.Method(type, "OnGameLoaded"),
                new HarmonyMethod(typeof(VanillaExpandedFramework), nameof(HostOnlyNewFactionDialog)));
        }

        private static void SyncFactionDiscoveryDialog(SyncWorker sync, ref Window window)
        {
            if (sync.isWriting)
                sync.Write(factionDefField(window));
            else
            {
                // For the person using the dialog, grab the existing one as we'll need to call the method on that instance
                // to open the next dialog with new faction.
                window = Find.WindowStack.Windows.FirstOrDefault(x => x.GetType() == newFactionSpawningDialogType);
                // We need to load the def, even if we don't use it - otherwise the synced method parameters will end up messed up
                var factionDef = sync.Read<FactionDef>();

                if (window == null)
                {
                    window ??= (Window)Activator.CreateInstance(
                        newFactionSpawningDialogType,
                        AccessTools.allDeclared,
                        null,
                        new object[] { new List<FactionDef>().GetEnumerator() },
                        null);
                    factionDefField(window) = factionDef;
                }
            }
        }

        private static bool HostOnlyNewFactionDialog() => !MP.IsInMultiplayer || MP.IsHosting;

        #endregion

        #region Door Teleporter

        // Dialog_RenameDoorTeleporter
        private static Type renameDoorTeleporterDialogType;
        private static ConstructorInfo renameDoorTeleporterDialogConstructor;
        private static AccessTools.FieldRef<object, ThingWithComps> renameDoorTeleporterDialogThingField;

        private static void PatchDoorTeleporter()
        {
            var type = AccessTools.TypeByName("VFECore.DoorTeleporter");
            // Destroy
            MpCompat.RegisterLambdaMethod(type, "GetDoorTeleporterGismoz", 0).SetContext(SyncContext.None);
            // Teleport to x
            MpCompat.RegisterLambdaDelegate(type, nameof(ThingWithComps.GetFloatMenuOptions), 0)[0]
                .TransformField("doorTeleporter", Serializer.New<Thing, int>(
                    t => t.thingIDNumber,
                    id =>
                    {
                        MP.TryGetThingById(id, out var thing);
                        return thing;
                    }),
                    true);

            renameDoorTeleporterDialogType = AccessTools.TypeByName("VFECore.Dialog_RenameDoorTeleporter");
            renameDoorTeleporterDialogConstructor = AccessTools.DeclaredConstructor(renameDoorTeleporterDialogType, new[] { type });
            renameDoorTeleporterDialogThingField = AccessTools.FieldRefAccess<ThingWithComps>(renameDoorTeleporterDialogType, "DoorTeleporter");

            PatchingUtilities.PatchPushPopRand(renameDoorTeleporterDialogConstructor);
        }

        #endregion

        #region Special Terrain

        private static Type terrainCompType;
        private static AccessTools.FieldRef<object, object> terrainCompParentField;
        private static AccessTools.FieldRef<object, IntVec3> terrainInstancePositionField;

        private static void PatchSpecialTerrain()
        {
            MpCompat.harmony.Patch(AccessTools.DeclaredMethod("VFECore.SpecialTerrainList:TerrainUpdate"),
                prefix: new HarmonyMethod(typeof(VanillaExpandedFramework), nameof(RemoveTerrainUpdateTimeBudget)));

            // Fix unsafe GetHashCode call
            terrainCompType = AccessTools.TypeByName("VFECore.TerrainComp");
            terrainCompParentField = AccessTools.FieldRefAccess<object>(terrainCompType, "parent");
            terrainInstancePositionField = AccessTools.FieldRefAccess<IntVec3>("VFECore.TerrainInstance:positionInt");

            MpCompat.harmony.Patch(AccessTools.DeclaredMethod("VFECore.ActiveTerrainUtility:HashCodeToMod"),
                prefix: new HarmonyMethod(MpMethodUtil.MethodOf(SaferGetHashCode)));
        }

        private static void RemoveTerrainUpdateTimeBudget(ref long timeBudget)
        {
            if (MP.IsInMultiplayer)
                timeBudget = long.MaxValue; // Basically limitless time

            // The method is limited in updating a max of 1/3 of all active special terrains.
            // If we'd want to work on having a performance option of some sort, we'd have to
            // base it around amount of terrain updates per tick, instead of basing it on actual time.
        }

        private static void SaferGetHashCode(ref object obj)
        {
            if (!MP.IsInMultiplayer || obj is IntVec3)
                return;
            if (terrainCompType.IsInstanceOfType(obj))
            {
                // Use parent IntVec3 position, since it'll be safe to call GetHashCode on
                obj = terrainInstancePositionField(terrainCompParentField(obj));
                return;
            }

            Log.ErrorOnce($"Potentially unsupported type for HashCodeToMod call in Multiplayer, desyncs likely to happen. Object type: {obj.GetType()}", obj.GetHashCode());
        }

        #endregion

        #region Patch Weather Overlay Effects

        private static Type weatherOverlayEffectsType;
        private static AccessTools.FieldRef<SkyOverlay, int> weatherOverlayEffectsNextDamageTickField;
        private static AccessTools.FieldRef<SkyOverlay, Dictionary<Map, int>> weatherOverlayEffectsNextDamageTickForMapField;

        private static void PatchWeatherOverlayEffects()
        {
            // It'll likely have issues with async time, as there's only 1 timer for all maps.
            weatherOverlayEffectsType = AccessTools.TypeByName("VFECore.WeatherOverlay_Effects");
            var nextDamageTickField = AccessTools.DeclaredField(weatherOverlayEffectsType, "nextDamageTick");
            var nextDamageTickForMapField = AccessTools.DeclaredField(weatherOverlayEffectsType, "nextDamageTickForMap");

            if (nextDamageTickForMapField != null)
                weatherOverlayEffectsNextDamageTickForMapField = AccessTools.FieldRefAccess<SkyOverlay, Dictionary<Map, int>>(nextDamageTickForMapField);
            else if (nextDamageTickField != null)
                weatherOverlayEffectsNextDamageTickField = AccessTools.FieldRefAccess<SkyOverlay, int>(nextDamageTickField);
            else
            {
                Log.Error("VFECore.WeatherOverlay_Effects:nextDamageTick field, patch failed.");
                return;
            }

            MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(GameComponentUtility), nameof(GameComponentUtility.FinalizeInit)),
                postfix: new HarmonyMethod(typeof(VanillaExpandedFramework), nameof(RefreshWeatherOverlayEffectCache)));
        }

        private static void RefreshWeatherOverlayEffectCache()
        {
            if (!MP.IsInMultiplayer)
                return;

            foreach (var def in DefDatabase<WeatherDef>.AllDefsListForReading)
            {
                if (def.Worker.overlays == null)
                    continue;

                foreach (var overlay in def.Worker.overlays)
                {
                    if (weatherOverlayEffectsType.IsInstanceOfType(overlay))
                    {
                        if (weatherOverlayEffectsNextDamageTickForMapField != null)
                            weatherOverlayEffectsNextDamageTickForMapField(overlay).Clear();
                        else
                            weatherOverlayEffectsNextDamageTickField(overlay) = 0;
                    }
                }
            }
        }

        #endregion

        #region Vanilla Cooking Expanded

        private static Type thoughtHediffType;

        private static void PatchVanillaCookingExpanded()
        {
            // Hediff is added the fist time MoodOffset is called, called during alert updates (not synced).
            thoughtHediffType = AccessTools.TypeByName("VanillaCookingExpanded.Thought_Hediff");
            if (thoughtHediffType != null)
            {
                // Only apply the patch if there's actually any ThoughtDef that uses this specific hediff type.
                // No point applying a patch and having it run if it'll never actually do anything useful.
                // An example of a mod using it would be Vanilla Cooking Expanded (used for gourmet meals).
                // This also required us to run this patch late, as otherwise the DefDatabase wouldn't be initialized yet.
                if (DefDatabase<ThoughtDef>.AllDefsListForReading.Any(def => thoughtHediffType.IsAssignableFrom(def.thoughtClass)))
                    PatchingUtilities.PatchTryGainMemory(TryGainThoughtHediff);
            }
            else Log.Error("Trying to patch `VanillaCookingExpanded.Thought_Hediff`, but the type is null. Did it get moved, renamed, or removed?");
        }

        private static bool TryGainThoughtHediff(Thought_Memory thought)
        {
            if (!thoughtHediffType.IsInstanceOfType(thought))
                return false;

            // Call MoodOffset to cause the method to add hediffs, etc.
            thought.MoodOffset();
            return true;
        }

        #endregion

        #region Building stuff requiring non-construction skill

        // DefModExtension
        private static Type thingDefExtensionType;
        private static AccessTools.FieldRef<DefModExtension, object> thingDefExtensionConstructionSkillRequirementField;
        private static AccessTools.FieldRef<object, WorkTypeDef> constructionSkillRequirementWorkTypeField;
        // Temp value
        private static IConstructible lastThing;

        private static void PatchWorkGiverDeliverResources()
        {
            MethodInfo rwMethod;
            MethodInfo mpMethod;

            try
            {
                const string rwMethodPath = "RimWorld.WorkGiver_ConstructDeliverResourcesToBlueprints:NoCostFrameMakeJobFor";
                rwMethod = AccessTools.DeclaredMethod(rwMethodPath);
                if (rwMethod == null)
                    throw new MissingMethodException($"Could not access method: {rwMethodPath}");

                const string mpMethodPath = "Multiplayer.Client.OnlyConstructorsPlaceNoCostFrames:IsConstruction";
                mpMethod = AccessTools.DeclaredMethod(mpMethodPath);
                if (mpMethod == null)
                    throw new MissingMethodException($"Could not access method: {mpMethodPath}");

                thingDefExtensionType = AccessTools.TypeByName("VFECore.ThingDefExtension");
                thingDefExtensionConstructionSkillRequirementField= AccessTools.FieldRefAccess<object>(
                    thingDefExtensionType, "constructionSkillRequirement");
                constructionSkillRequirementWorkTypeField = AccessTools.FieldRefAccess<WorkTypeDef>(
                    "VFECore.ConstructionSkillRequirement:workType");
            }
            catch (Exception)
            {
                // Cleanup stuff that won't be ever used if exception occurs before patching.
                thingDefExtensionType = null;
                thingDefExtensionConstructionSkillRequirementField = null;
                constructionSkillRequirementWorkTypeField = null;

                throw;
            }

            MpCompat.harmony.Patch(rwMethod, prefix: new HarmonyMethod(typeof(VanillaExpandedFramework), nameof(PreNoCostFrameMakeJobFor)));
            MpCompat.harmony.Patch(mpMethod, postfix: new HarmonyMethod(typeof(VanillaExpandedFramework), nameof(PostIsConstruction)));
        }

        private static void PreNoCostFrameMakeJobFor(IConstructible c) => lastThing = c;

        private static void PostIsConstruction(WorkGiver w, ref bool __result)
        {
            // If __result is true, the work type was construction, so MP allowed it.
            if (__result || lastThing is not Thing thing || thing.def?.entityDefToBuild?.modExtensions == null)
            {
                lastThing = null;
                return;
            }

            // Look for the VFE def mod extension
            foreach (var extension in thing.def.entityDefToBuild.modExtensions)
            {
                if (extension != null && thingDefExtensionType.IsInstanceOfType(extension))
                {
                    // Get the construction skill requirement object
                    var constructionRequirement = thingDefExtensionConstructionSkillRequirementField(extension);
                    if (constructionRequirement == null)
                        break;

                    // Get the WorkTypeDef of the correct work requirement
                    var constructionWorkType = constructionSkillRequirementWorkTypeField(constructionRequirement);
                    // Set the result based on if the construction work type is the same as extension's work type.
                    __result = w.def.workType == constructionWorkType;

                    break;
                }
            }

            lastThing = null;
        }

        #endregion

        #region Caches

        private static void PatchStaticCaches()
        {
            // TODO: Go through Vanilla Expanded Framework's VanillaGenesExpanded.StaticCollectionsClass and clean some of those on join.
            // While not critical for fixing MP desyncs, it should help with RAM usage as many of those have data unique to a specific
            // game, and leaving it will just leave garbage data. In the context of MP it could be especially useful in case of
            // frequent (unrelated) desyncs, as the players joining in would gain more and more garbage data each time they join.
        }

        #endregion

        #region Graphic Customization Dialog

        // Dialog_GraphicCustomization
        private static Type graphicCustomizationDialogType;
        internal static AccessTools.FieldRef<Window, ThingComp> graphicCustomizationCompField;
        private static AccessTools.FieldRef<Window, ThingComp> graphicCustomizationGeneratedNamesCompField;
        private static AccessTools.FieldRef<Window, Pawn> graphicCustomizationPawnField;
        private static AccessTools.FieldRef<Window, IList> graphicCustomizationCurrentVariantsField;
        private static AccessTools.FieldRef<Window, string> graphicCustomizationCurrentNameField;

        // TextureVariant
        private static SyncType variantsListType;
        private static AccessTools.FieldRef<object, string> textureVariantTexNameField;
        private static AccessTools.FieldRef<object, string> textureVariantTextureField;
        private static AccessTools.FieldRef<object, string> textureVariantOutlineField;
        private static AccessTools.FieldRef<object, object> textureVariantTextureVariantOverrideField;
        private static AccessTools.FieldRef<object, float> textureVariantChanceOverrideField;

        // TextureVariantOverride
        private static SyncType textureVariantOverrideType;
        private static AccessTools.FieldRef<object, float> textureVariantOverrideChanceField;
        private static AccessTools.FieldRef<object, string> textureVariantOverrideGroupNameField;
        private static AccessTools.FieldRef<object, string> textureVariantOverrideTexNameField;

        private static void PatchGraphicCustomizationDialog()
        {
            var type = graphicCustomizationDialogType = AccessTools.TypeByName("GraphicCustomization.Dialog_GraphicCustomization");
            graphicCustomizationCompField = AccessTools.FieldRefAccess<ThingComp>(type, "comp");
            graphicCustomizationGeneratedNamesCompField = AccessTools.FieldRefAccess<ThingComp>(type, "compGeneratedName");
            graphicCustomizationPawnField = AccessTools.FieldRefAccess<Pawn>(type, "pawn");
            graphicCustomizationCurrentVariantsField = AccessTools.FieldRefAccess<IList>(type, "currentVariants");
            graphicCustomizationCurrentNameField = AccessTools.FieldRefAccess<string>(type, "currentName");
            // Accept customization
            var method = MpMethodUtil.GetLambda(type, nameof(Window.DoWindowContents), 0);
            MP.RegisterSyncMethod(method);
            MpCompat.RegisterLambdaMethod(type, nameof(Window.DoWindowContents), 0);
            MP.RegisterSyncWorker<Window>(SyncGraphicCustomizationDialog, type, true);
            MpCompat.harmony.Patch(AccessTools.DeclaredMethod(type, "DrawConfirmButton"),
                prefix: new HarmonyMethod(CloseGraphicCustomizationDialogOnAccept));
            MpCompat.harmony.Patch(AccessTools.DeclaredMethod(type, nameof(Window.DoWindowContents)),
                prefix: new HarmonyMethod(CloseGraphicCustomizationWhenNoLongerValid));

            type = AccessTools.TypeByName("GraphicCustomization.TextureVariant");
            variantsListType = typeof(List<>).MakeGenericType(type);
            textureVariantTexNameField = AccessTools.FieldRefAccess<string>(type, "texName");
            textureVariantTextureField = AccessTools.FieldRefAccess<string>(type, "texture");
            textureVariantOutlineField = AccessTools.FieldRefAccess<string>(type, "outline");
            textureVariantChanceOverrideField = AccessTools.FieldRefAccess<float>(type, "chanceOverride");
            textureVariantTextureVariantOverrideField = AccessTools.FieldRefAccess<object>(type, "textureVariantOverride");
            MP.RegisterSyncWorker<object>(SyncTextureVariant, type, shouldConstruct: true);

            textureVariantOverrideType = type = AccessTools.TypeByName("GraphicCustomization.TextureVariantOverride");
            textureVariantOverrideChanceField = AccessTools.FieldRefAccess<float>(type, "chance");
            textureVariantOverrideGroupNameField = AccessTools.FieldRefAccess<string>(type, "groupName");
            textureVariantOverrideTexNameField = AccessTools.FieldRefAccess<string>(type, "texName");
            MP.RegisterSyncWorker<object>(SyncTextureVariantOverride, type, shouldConstruct: true);
        }

        private static void CloseGraphicCustomizationDialogOnAccept(Window __instance, ref Action action)
        {
            // The action is a synced method, and it handles closing the dialog. Since
            // the method ends up being synced the closing does not happen. This will
            // ensure that the dialog is closed when the accept button is pressed
            // and the proper method will be synced as well.
            var nonRefAction = action;
            action = (() => __instance.Close()) + nonRefAction;
        }

        private static void CloseGraphicCustomizationWhenNoLongerValid(Window __instance, ThingComp ___comp, Pawn ___pawn)
        {
            // Since the dialog doesn't pause, ensure we close it if the comp's parent
            // or the pawn ever get destroyed, despawned, or end up on different maps.
            if (MP.IsInMultiplayer && !IsGraphicCustomizationDialogValid(___comp, ___pawn))
                __instance.Close();
        }

        private static bool CancelGraphicCustomizationExecutionIfNoLongerValid(ThingComp ___comp, Pawn ___pawn)
            => !MP.IsInMultiplayer || IsGraphicCustomizationDialogValid(___comp, ___pawn);

        private static bool IsGraphicCustomizationDialogValid(ThingComp comp, Pawn pawn)
            => comp.parent != null && pawn != null &&
               comp.parent.Spawned && pawn.Spawned &&
               !comp.parent.Destroyed && !pawn.Destroyed &&
               !pawn.Dead && comp.parent.Map == pawn.Map;

        private static void SyncGraphicCustomizationDialog(SyncWorker sync, ref Window dialog)
        {
            ThingComp comp = null;

            if (sync.isWriting)
            {
                sync.Write(graphicCustomizationCompField(dialog));
            }
            else
            {
                // Skip constructor since it has a bunch of initialization we don't care about.
                dialog = (Window)FormatterServices.GetUninitializedObject(graphicCustomizationDialogType);

                comp = sync.Read<ThingComp>();
                graphicCustomizationCompField(dialog) = comp;
            }

            SyncGraphicCustomizationDialog(sync, ref dialog, comp);
        }

        internal static void SyncGraphicCustomizationDialog(SyncWorker sync, ref Window dialog, ThingComp graphicCustomizationComp)
        {
            if (sync.isWriting)
            {
                sync.Write(graphicCustomizationPawnField(dialog));
                sync.Write(graphicCustomizationCurrentVariantsField(dialog), variantsListType);
                sync.Write(graphicCustomizationCurrentNameField(dialog));
            }
            else
            {
                graphicCustomizationGeneratedNamesCompField(dialog) = graphicCustomizationComp?.parent.GetComp<CompGeneratedNames>();
                graphicCustomizationPawnField(dialog) = sync.Read<Pawn>();
                graphicCustomizationCurrentVariantsField(dialog) = sync.Read<IList>(variantsListType);
                graphicCustomizationCurrentNameField(dialog) = sync.Read<string>();
            }
        }

        private static void SyncTextureVariant(SyncWorker sync, ref object variant)
        {
            if (sync.isWriting)
            {
                if (variant == null)
                {
                    sync.Write(false);
                    return;
                }

                sync.Write(true);

                sync.Write(textureVariantTexNameField(variant));
                sync.Write(textureVariantTextureField(variant));
                sync.Write(textureVariantOutlineField(variant));
                sync.Write(textureVariantTextureVariantOverrideField(variant), textureVariantOverrideType);
                sync.Write(textureVariantChanceOverrideField(variant));
            }
            else if (sync.Read<bool>())
            {
                textureVariantTexNameField(variant) = sync.Read<string>();
                textureVariantTextureField(variant) = sync.Read<string>();
                textureVariantOutlineField(variant) = sync.Read<string>();
                textureVariantTextureVariantOverrideField(variant) = sync.Read<object>(textureVariantOverrideType);
                textureVariantChanceOverrideField(variant) = sync.Read<float>();
            }
        }

        private static void SyncTextureVariantOverride(SyncWorker sync, ref object variantOverride)
        {
            if (sync.isWriting)
            {
                if (variantOverride == null)
                {
                    sync.Write(false);
                    return;
                }

                sync.Write(true);
                sync.Write(textureVariantOverrideChanceField(variantOverride));
                sync.Write(textureVariantOverrideGroupNameField(variantOverride));
                sync.Write(textureVariantOverrideTexNameField(variantOverride));
            }
            else if (sync.Read<bool>())
            {
                textureVariantOverrideChanceField(variantOverride) = sync.Read<float>();
                textureVariantOverrideGroupNameField(variantOverride) = sync.Read<string>();
                textureVariantOverrideTexNameField(variantOverride) = sync.Read<string>();
            }
        }

        #endregion

        #region Drafted AI

        private static FastInvokeHandler getDraftedActionDataMethod;
        private static FastInvokeHandler draftedActionDataGetPawnMethod;

        private static void PatchDraftedAi()
        {
            // Drafted AI is used by player controlled insectoids,
            // and allows the player to toggle hunt mode (search
            // and destroy) as well as toggling specific/all
            // abilities to be autocasted.

            getDraftedActionDataMethod = MethodInvoker.GetHandler(
                AccessTools.DeclaredMethod("VFECore.AI.DraftedActionHolder:GetData"));
            draftedActionDataGetPawnMethod = MethodInvoker.GetHandler(
                AccessTools.DeclaredPropertyGetter("VFECore.AI.DraftedActionData:Pawn"));

            var type = AccessTools.TypeByName("VFECore.AI.DraftedActionData");
            MP.RegisterSyncMethod(type, "ToggleHuntMode");
            MP.RegisterSyncMethod(type, "ToggleAutoForAll");
            MP.RegisterSyncMethod(type, "ToggleAutoCastFor");
            MP.RegisterSyncWorker<object>(SyncDraftedActionData, type);
        }

        private static void SyncDraftedActionData(SyncWorker sync, ref object draftedActionData)
        {
            if (sync.isWriting)
            {
                if (draftedActionData == null)
                    sync.Write((Pawn)null);
                else
                    sync.Write((Pawn)draftedActionDataGetPawnMethod(draftedActionData));
            }
            else
            {
                var pawn = sync.Read<Pawn>();
                if (pawn != null)
                    getDraftedActionDataMethod(null, pawn);
            }
        }

        #endregion

        #region Thing spawning on map generation (ObjectSpawnsDef)

        private static void PatchMapObjectGeneration()
        {
            // When calling "LongEventHandler.ExecuteWhenFinished" inside a
            // "MapGenerator:GenerateMap" call the seed will differ between
            // players. Is this something we should look into in MP itself?
            MpCompat.harmony.Patch(AccessTools.DeclaredMethod("VFECore.MapGenerator_GenerateMap_Patch:DoMapSpawns"),
                prefix: new HarmonyMethod(PreDoSpawns),
                finalizer: new HarmonyMethod(PostDoSpawns));
        }

        private static void PreDoSpawns(Map map)
        {
            if (MP.IsInMultiplayer)
                Rand.PushState(Gen.HashCombineInt(Gen.HashCombineInt(map.uniqueID, map.Tile), Find.TickManager.TicksGame));
        }

        private static void PostDoSpawns()
        {
            if (MP.IsInMultiplayer)
                Rand.PopState();
        }

        #endregion

        #region Quest chain dev mode window

        // GameComponent_QuestChains
        private static AccessTools.FieldRef<GameComponent> questChainsGameCompInstanceField;
        private static AccessTools.FieldRef<GameComponent, IList> questChainsGameCompQuestsField;
        private static AccessTools.FieldRef<GameComponent, IList> questChainsGameCompFutureQuestsField;

        // QuestInfo
        private static AccessTools.FieldRef<object, Quest> questInfoQuestField;

        // FutureQuestInfo
        private static AccessTools.FieldRef<object, QuestScriptDef> futureQuestInfoQuestDefField;

        private static void PatchQuestChainsDevMode()
        {
            // The window normally opens from debug actions menu, which
            // MP syncs by default. This patch basically makes sure the
            // dialog only opens for the player that attempted to open it.
            MpCompat.harmony.Patch(AccessTools.DeclaredMethod("VFECore.QuestChainsDevWindow:ViewQuestChains"),
                prefix: new HarmonyMethod(CancelDevModeWindowIfNotExecutingSelfCommand));

            // Prepare field access for our patches/synced methods
            var type = AccessTools.TypeByName("VFECore.GameComponent_QuestChains");
            questChainsGameCompInstanceField = AccessTools.StaticFieldRefAccess<GameComponent>(
                AccessTools.DeclaredField(type, "Instance"));
            questChainsGameCompQuestsField = AccessTools.FieldRefAccess<IList>(type, "quests");
            questChainsGameCompFutureQuestsField = AccessTools.FieldRefAccess<IList>(type, "futureQuests");

            questInfoQuestField = AccessTools.FieldRefAccess<Quest>("VFECore.QuestInfo:quest");
            futureQuestInfoQuestDefField = AccessTools.FieldRefAccess<QuestScriptDef>("VFECore.FutureQuestInfo:questDef");

            // Sync our button replacement methods
            MP.RegisterSyncMethod(MpMethodUtil.MethodOf(SyncedQuestChainDevForceEnd)).CancelIfAnyArgNull().SetDebugOnly();
            MP.RegisterSyncMethod(MpMethodUtil.MethodOf(SyncedQuestChainDevFireNow)).CancelIfAnyArgNull().SetDebugOnly();

            // Replace mod buttons with our own
            MpCompat.harmony.Patch(AccessTools.DeclaredMethod("VFECore.QuestChainsDevWindow:DrawQuestInfo"),
                transpiler: new HarmonyMethod(ReplaceQuestChainsWindowSuccessFailButtons));
            MpCompat.harmony.Patch(AccessTools.DeclaredMethod("VFECore.QuestChainsDevWindow:DrawFutureQuestInfo"),
                transpiler: new HarmonyMethod(ReplaceQuestChainsWindowFireNowButton));
        }

        private static bool CancelDevModeWindowIfNotExecutingSelfCommand()
        {
            // Only allow the method to run if it's SP, not a synced
            // command, or a synced command issues by self.
            return !MP.IsInMultiplayer || !MP.IsExecutingSyncCommand || MP.IsExecutingSyncCommandIssuedBySelf;
        }

        private static IEnumerable<CodeInstruction> ReplaceQuestChainsWindowSuccessFailButtons(IEnumerable<CodeInstruction> instr, MethodBase baseMethod)
        {
            var target = AccessTools.DeclaredMethod(typeof(Widgets), nameof(Widgets.ButtonText),
                [typeof(Rect), typeof(string), typeof(bool), typeof(bool), typeof(bool), typeof(TextAnchor?)]);
            var forceSuccessReplacement = MpMethodUtil.MethodOf(ReplacedForceSuccessButton);
            var forceFailureReplacement = MpMethodUtil.MethodOf(ReplacedForceFailureButton);

            IEnumerable<CodeInstruction> ExtraInstructions(CodeInstruction _) =>
            [
                // Load the argument with index 2 (QuestInfo) to our method
                new CodeInstruction(OpCodes.Ldarg_2),
            ];

            return instr
                .ReplaceMethod(target, forceSuccessReplacement, baseMethod, ExtraInstructions, expectedReplacements: 1, targetText: "Force Success")
                .ReplaceMethod(target, forceFailureReplacement, baseMethod, ExtraInstructions, expectedReplacements: 1, targetText: "Force Fail");
        }

        private static IEnumerable<CodeInstruction> ReplaceQuestChainsWindowFireNowButton(IEnumerable<CodeInstruction> instr, MethodBase baseMethod)
        {
            var target = AccessTools.DeclaredMethod(typeof(Widgets), nameof(Widgets.ButtonText),
                [typeof(Rect), typeof(string), typeof(bool), typeof(bool), typeof(bool), typeof(TextAnchor?)]);
            var replacement = MpMethodUtil.MethodOf(ReplacedFireNowButton);

            IEnumerable<CodeInstruction> ExtraInstructions(CodeInstruction _) =>
            [
                // Load the argument with index 2 (FutureQuestInfo) to our method
                new CodeInstruction(OpCodes.Ldarg_2),
            ];

            return instr.ReplaceMethod(target, replacement, baseMethod, ExtraInstructions, expectedReplacements: 1, targetText: "Fire Now");
        }

        private static bool ReplacedForceSuccessButton(Rect rect, string label, bool drawBackground, bool doMouseoverSound, bool active, TextAnchor? overrideTextAnchor, object questInfo)
        {
            var result = Widgets.ButtonText(rect, label, drawBackground, doMouseoverSound, active, overrideTextAnchor);
            // Return result out of MP and return early if not pressed in MP.
            if (!MP.IsInMultiplayer || !result)
                return result;

            // If pressed and in MP, redirect the button
            SyncedQuestChainDevForceEnd(questInfoQuestField(questInfo), QuestEndOutcome.Success);

            return false;
        }

        private static bool ReplacedForceFailureButton(Rect rect, string label, bool drawBackground, bool doMouseoverSound, bool active, TextAnchor? overrideTextAnchor, object questInfo)
        {
            var result = Widgets.ButtonText(rect, label, drawBackground, doMouseoverSound, active, overrideTextAnchor);
            // Return result out of MP and return early if not pressed in MP.
            if (!MP.IsInMultiplayer || !result)
                return result;

            // If pressed and in MP, redirect the button
            SyncedQuestChainDevForceEnd(questInfoQuestField(questInfo), QuestEndOutcome.Fail);

            return false;
        }

        private static bool ReplacedFireNowButton(Rect rect, string label, bool drawBackground, bool doMouseoverSound, bool active, TextAnchor? overrideTextAnchor, object futureQuestInfo)
        {
            var result = Widgets.ButtonText(rect, label, drawBackground, doMouseoverSound, active, overrideTextAnchor);
            // Return result out of MP and return early if not pressed in MP.
            if (!MP.IsInMultiplayer || !result)
                return result;

            var def = futureQuestInfoQuestDefField(futureQuestInfo);
            var index = questChainsGameCompFutureQuestsField(questChainsGameCompInstanceField()).IndexOf(futureQuestInfo);
            // If pressed and in MP, redirect the button
            SyncedQuestChainDevFireNow(def, index);

            return false;
        }

        private static void SyncedQuestChainDevForceEnd(Quest quest, QuestEndOutcome outcome)
        {
            if (quest.State != QuestState.Ongoing)
                return;

            // Find the QuestInfo in GameComponent_QuestChains.
            // Assume there's only 1 match, as it wouldn't make much sense
            // for multiple QuestInfos to have the same quest assigned.

            // This check may be a bit pointless, but let's take it safe here
            // and check if there's a QuestInfo with our quest in the list.
            foreach (var questInfo in questChainsGameCompQuestsField(questChainsGameCompInstanceField()))
            {
                if (questInfoQuestField(questInfo) == quest)
                {
                    quest.End(outcome, false);
                    return;
                }
            }

            // Error log for person syncing the command.
            if (MP.IsExecutingSyncCommandIssuedBySelf)
                Log.Error($"Trying to find a quest chain's QuestInfo for quest with ID {quest.id} and name {quest.name}, but there was no matching QuestInfo. This should not happen.");
        }

        private static void SyncedQuestChainDevFireNow(QuestScriptDef def, int index)
        {
            // Make sure the index is 0 or bigger
            if (index < 0)
            {
                if (MP.IsExecutingSyncCommandIssuedBySelf)
                    Log.Error($"Failed syncing FutureQuestInfo, index too small: index={index}, def={def}");
                return;
            }

            // Make sure the index is within the list bounds
            var list = questChainsGameCompFutureQuestsField(questChainsGameCompInstanceField());
            if (index >= list.Count)
            {
                if (MP.IsExecutingSyncCommandIssuedBySelf)
                    Log.Error($"Failed syncing FutureQuestInfo, index too high: index={index}, count={list.Count}, def={def}");
                return;
            }

            // Make sure the FutureQuestInfo at the target index has the same target QuestScriptDef.
            // It could technically be a different one with the same quest, but we have no real
            // way to be able to distinguish them otherwise in here without doing much bigger patches.
            var futureQuestInfo = list[index];
            var localDef = futureQuestInfoQuestDefField(futureQuestInfo);
            if (localDef != def)
            {
                if (MP.IsExecutingSyncCommandIssuedBySelf)
                    Log.Error($"Failed syncing FutureQuestInfo, QuestScriptDef mismatch: index={index}, count={list.Count}, syncedDef={def}, localDef={localDef}");
                return;
            }

            // Create the quest and (if needed) send the letter
            var quest = QuestUtility.GenerateQuestAndMakeAvailable(def, StorytellerUtility.DefaultThreatPointsNow(Find.World));
            if (def.sendAvailableLetter)
                QuestUtility.SendLetterQuestAvailable(quest);
            // Remove the future quest from the list since it just became an active one
            list.Remove(futureQuestInfo);
        }

        #endregion
    }
}