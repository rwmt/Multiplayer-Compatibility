using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

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
            {
                (PatchItemProcessor, "Item Processor", false),
                (PatchOtherRng, "Other RNG", false),
                (PatchVFECoreDebug, "Debug Gizmos", false),
                (PatchAbilities, "Abilities", true),
                (PatchHireableFactions, "Hireable Factions", false),
                (PatchVanillaFurnitureExpanded, "Vanilla Furniture Expanded", false),
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
                (PatchSpecialTerrain, "Special Terrain", false),
                (PatchWeatherOverlayEffects, "Weather Overlay Effects", false),
                (PatchExtraPregnancyApproaches, "Extra Pregnancy Approaches", false),
            };

            foreach (var (patchMethod, componentName, latePatch) in patches)
            {
                try
                {
                    if (latePatch)
                        LongEventHandler.ExecuteWhenFinished(patchMethod);
                    else
                        patchMethod();
                }
                catch (Exception e)
                {
                    Log.Error($"Encountered an error patching {componentName} part of Vanilla Expanded Framework - this part of the mod may not work properly!");
                    Log.Error(e.ToString());
                }
            }
        }

        #region Shared sync workers and patches

        // MP - ThingsById
        private static Dictionary<int, Thing> thingsById;

        // Right now only used by teleporter doors. Could potentially be used by other mods.
        // It's a separate method instead of being always initialized in case this ever changes in MP and causes issues here.
        private static void EnsureThingsByIdDictionaryActive() => thingsById ??= (Dictionary<int, Thing>)AccessTools.Field(AccessTools.TypeByName("Multiplayer.Client.ThingsById"), "thingsById").GetValue(null);

        private static void SyncCommandWithBuilding(SyncWorker sync, ref Command command)
        {
            var traverse = Traverse.Create(command);
            var building = traverse.Field("building");

            if (sync.isWriting)
                sync.Write(building.GetValue() as Thing);
            else
                building.SetValue(sync.Read<Thing>());
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
                0, 1); // Disable extra approaches (0), set extra approach (1)
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
        private static ISyncField abilityAutoCastField;

        private static void PatchAbilities()
        {
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
            // There's another method taking LocalTargetInfo. Harmony grabs the one we need, but just in case specify the types to avoid ambiguity.
            MP.RegisterSyncMethod(type, "StartAbilityJob", new SyncType[] { typeof(GlobalTargetInfo[]) });
            MP.RegisterSyncWorker<ITargetingSource>(SyncVEFAbility, type, true);
            abilityAutoCastField = MP.RegisterSyncField(type, "autoCast");
            MpCompat.harmony.Patch(AccessTools.DeclaredMethod(type, "DoAction"),
                prefix: new HarmonyMethod(typeof(VanillaExpandedFramework), nameof(PreAbilityDoAction)),
                postfix: new HarmonyMethod(typeof(VanillaExpandedFramework), nameof(PostAbilityDoAction)));

            type = AccessTools.TypeByName("VFECore.CompShieldField");
            MpCompat.RegisterLambdaMethod(type, nameof(ThingComp.CompGetWornGizmosExtra), 0);
            MpCompat.RegisterLambdaMethod(type, "GetGizmos", 0, 2);
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
        private static AccessTools.FieldRef<object, ThingComp> setStoneBuildingField;
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
            MP.RegisterSyncWorker<Command>(SyncCommandWithBuilding, type, shouldConstruct: true);

            MpCompat.RegisterLambdaMethod("VanillaFurnitureExpanded.CompRockSpawner", "CompGetGizmosExtra", 0);

            type = AccessTools.TypeByName("VanillaFurnitureExpanded.Command_SetStoneType");
            setStoneBuildingField = AccessTools.FieldRefAccess<ThingComp>(type, "building");
            MpCompat.RegisterLambdaMethod(type, "ProcessInput", 0);
            MP.RegisterSyncWorker<Command>(SyncSetStoneTypeCommand, type, shouldConstruct: true);
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

        private static void SyncSetStoneTypeCommand(SyncWorker sync, ref Command obj)
        {
            if (sync.isWriting)
                sync.Write(setStoneBuildingField(obj));
            else
                setStoneBuildingField(obj) = sync.Read<ThingComp>();
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
        private static FastInvokeHandler mvcfPawnGetter;
        private static AccessTools.FieldRef<object, IList> mvcfVerbsField;

        // PawnVerbUtility
        private static AccessTools.FieldRef<object, object> mvcfPawnVerbUtilityField;
        private delegate object GetManager(Pawn p, bool createIfMissing);
        private static GetManager mvcfPawnVerbUtilityGetManager;

        // ManagedVerb
        private static FastInvokeHandler mvcfManagedVerbManagerGetter;

        // VerbWithComps
        private static AccessTools.FieldRef<object, IList> mvcfVerbWithCompsField;

        // VerbComp
        private static AccessTools.FieldRef<object, object> mvcfVerbCompParentField;

        private static void PatchMVCF()
        {
            MpCompat.harmony.Patch(AccessTools.Method(typeof(Pawn), nameof(Pawn.SpawnSetup)),
                postfix: new HarmonyMethod(typeof(VanillaExpandedFramework), nameof(EverybodyGetsVerbManager)));

            var type = AccessTools.TypeByName("MVCF.MVCF");
            // HashSet<string>, using it as IEnumerable<string> for a bit of extra safety in case it ever gets changed to a list or something.
            mvcfEnabledFeaturesSet = AccessTools.Field(type, "EnabledFeatures").GetValue(null) as IEnumerable<string>;
            if (mvcfEnabledFeaturesSet == null)
                Log.Warning("Cannot access the list of enabled MVCF features, this may cause issues");

            type = AccessTools.TypeByName("MVCF.Utilities.PawnVerbUtility");
            mvcfPawnVerbUtilityGetManager = AccessTools.MethodDelegate<GetManager>(AccessTools.Method(type, "Manager"));
            mvcfPawnVerbUtilityField = AccessTools.FieldRefAccess<object>(type, "managers");

            type = AccessTools.TypeByName("MVCF.VerbManager");
            MP.RegisterSyncWorker<object>(SyncVerbManager, type, isImplicit: true);
            mvcfPawnGetter = MethodInvoker.GetHandler(AccessTools.PropertyGetter(type, "Pawn"));
            mvcfVerbsField = AccessTools.FieldRefAccess<IList>(type, "verbs");

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
                sync.Write((Pawn)mvcfPawnGetter(obj, Array.Empty<object>()));
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
                var managedVerbsList = mvcfVerbsField(verbManager);
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
                    var managedVerbsList = mvcfVerbsField(verbManager);
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

        #endregion

        #region Pipe System

        private static Type deconstructPipeDesignatorType;
        private static AccessTools.FieldRef<Designator_Deconstruct, Def> deconstructPipeDesignatorNetDefField;

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
        }

        private static void SyncDeconstructPipeDesignator(SyncWorker sync, ref Designator_Deconstruct designator)
        {
            if (sync.isWriting)
                sync.Write(deconstructPipeDesignatorNetDefField(designator));
            else
                designator = (Designator_Deconstruct)Activator.CreateInstance(deconstructPipeDesignatorType, sync.Read<Def>());
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

        // DoorTeleporter.<>c__DisplayClass26_0
        private static Type innerClassDoorTeleporterLocalsType;
        private static AccessTools.FieldRef<object, ThingWithComps> innerClassDoorTeleporterThisField;
        private static AccessTools.FieldRef<object, Pawn> innerClassDoorTeleporterPawnField;

        // DoorTeleporter.<>c__DisplayClass26_1
        private static AccessTools.FieldRef<object, object> innerClassDoorTeleporterLocalsField;
        private static AccessTools.FieldRef<object, Thing> innerClassDoorTeleporterTargetField;

        private static void PatchDoorTeleporter()
        {
            var type = AccessTools.TypeByName("VFECore.DoorTeleporter");
            // Destroy
            MpCompat.RegisterLambdaMethod(type, "GetDoorTeleporterGismoz", 0).SetContext(SyncContext.None);
            // Teleport to x
            MpCompat.RegisterLambdaDelegate(type, nameof(ThingWithComps.GetFloatMenuOptions), 0);

            renameDoorTeleporterDialogType = AccessTools.TypeByName("VFECore.Dialog_RenameDoorTeleporter");
            renameDoorTeleporterDialogConstructor = AccessTools.DeclaredConstructor(renameDoorTeleporterDialogType, new[] { type });
            renameDoorTeleporterDialogThingField = AccessTools.FieldRefAccess<ThingWithComps>(renameDoorTeleporterDialogType, "DoorTeleporter");

            PatchingUtilities.PatchPushPopRand(renameDoorTeleporterDialogConstructor);
            MP.RegisterSyncWorker<Dialog_Rename>(SyncDialogRenameDoorTeleporter, renameDoorTeleporterDialogType);
            MP.RegisterSyncMethod(renameDoorTeleporterDialogType, nameof(Dialog_Rename.SetName))
                // Since we sync the "SetName" method and nothing else, it'll leave the dialog open for
                // players who didn't click the button to rename it - we need to manually close it.
                .SetPostInvoke((dialog, _) =>
                {
                    if (dialog is Window w)
                        Find.WindowStack.TryRemove(w);
                });

            var innerClassMethod = MpMethodUtil.GetLambda(type, nameof(ThingWithComps.GetFloatMenuOptions));

            if (innerClassMethod == null)
                Log.Error("Couldn't find inner class 1 for door teleporters, they won't work.");
            else
            {
                var fields = AccessTools.GetDeclaredFields(innerClassMethod.DeclaringType);
                if (fields.Count != 2)
                    Log.Error($"Found incorrect amount of fields while trying to register door teleporters (inner class 1) - found: {fields.Count}, expected: 2.");

                foreach (var field in fields)
                {
                    if (field.FieldType == type)
                        innerClassDoorTeleporterTargetField = AccessTools.FieldRefAccess<object, Thing>(field);
                    else
                    {
                        innerClassDoorTeleporterLocalsType = field.FieldType;
                        innerClassDoorTeleporterLocalsField = AccessTools.FieldRefAccess<object, object>(field);
                    }
                }

                if (innerClassDoorTeleporterLocalsType == null)
                {
                    Log.Error("Couldn't find inner class 0 for door teleporters, they won't work.");
                }
                else
                {
                    fields = AccessTools.GetDeclaredFields(innerClassDoorTeleporterLocalsType);
                    if (fields.Count != 2)
                        Log.Error($"Found incorrect amount of fields while trying to register door teleporters (inner class 0) - found: {fields.Count}, expected: 2.");

                    foreach (var field in fields)
                    {
                        if (field.FieldType == type)
                            innerClassDoorTeleporterThisField = AccessTools.FieldRefAccess<object, ThingWithComps>(field);
                        else if (field.FieldType == typeof(Pawn))
                            innerClassDoorTeleporterPawnField = AccessTools.FieldRefAccess<object, Pawn>(field);
                    }

                    EnsureThingsByIdDictionaryActive();
                    MP.RegisterSyncWorker<object>(SyncInnerDoorTeleporterClass, innerClassMethod.DeclaringType, shouldConstruct: true);
                    MP.RegisterSyncMethod(innerClassMethod);
                }
            }
        }

        private static void SyncDialogRenameDoorTeleporter(SyncWorker sync, ref Dialog_Rename dialog)
        {
            if (sync.isWriting)
            {
                sync.Write(renameDoorTeleporterDialogThingField(dialog));
                sync.Write(dialog.curName);
            }
            else
            {
                var doorTeleporter = sync.Read<ThingWithComps>();
                var name = sync.Read<string>();

                // The dialog may be already open
                dialog = Find.WindowStack.Windows.FirstOrDefault(x => x.GetType() == renameDoorTeleporterDialogType) as Dialog_Rename;
                // If the dialog is not open, or the open dialog is for a different door - create a new dialog instead
                if (dialog == null || renameDoorTeleporterDialogThingField(dialog) != doorTeleporter)
                    dialog = (Dialog_Rename)renameDoorTeleporterDialogConstructor.Invoke(new object[] { doorTeleporter });

                dialog.curName = name;
            }
        }

        private static void SyncInnerDoorTeleporterClass(SyncWorker sync, ref object obj)
        {
            if (sync.isWriting)
            {
                var locals = innerClassDoorTeleporterLocalsField(obj);
                var target = innerClassDoorTeleporterTargetField(obj);

                // The target is on a different map, so we can't just sync it as MP does not allow it.
                // We need to sync the ID number and manually get the target by ID instead.
                sync.Write(target.thingIDNumber);
                sync.Write(innerClassDoorTeleporterThisField(locals));
                sync.Write(innerClassDoorTeleporterPawnField(locals));
            }
            else
            {
                // shouldConstruct: true, so obj is constructed
                // but we need to construct the other object used for locals
                var locals = Activator.CreateInstance(innerClassDoorTeleporterLocalsType);
                innerClassDoorTeleporterLocalsField(obj) = locals;

                // Get the target by ID.
                innerClassDoorTeleporterTargetField(obj) = thingsById.GetValueSafe(sync.Read<int>());
                innerClassDoorTeleporterThisField(locals) = sync.Read<ThingWithComps>();
                innerClassDoorTeleporterPawnField(locals) = sync.Read<Pawn>();
            }
        }

        #endregion

        #region Special Terrain

        private static void PatchSpecialTerrain()
        {
            MpCompat.harmony.Patch(AccessTools.DeclaredMethod("VFECore.SpecialTerrainList:TerrainUpdate"),
                prefix: new HarmonyMethod(typeof(BiomesCore), nameof(RemoveTerrainUpdateTimeBudget)));
        }

        private static void RemoveTerrainUpdateTimeBudget(ref long timeBudget)
        {
            if (MP.IsInMultiplayer)
                timeBudget = long.MaxValue; // Basically limitless time

            // The method is limited in updating a max of 1/3 of all active special terrains.
            // If we'd want to work on having a performance option of some sort, we'd have to
            // base it around amount of terrain updates per tick, instead of basing it on actual time.
        }

        #endregion

        #region Patch Weather Overlay Effects

        private static Type weatherOverlayEffectsType;
        private static AccessTools.FieldRef<SkyOverlay, int> weatherOverlayEffectsNextDamageTickField;

        private static void PatchWeatherOverlayEffects()
        {
            // It'll likely have issues with async time, as there's only 1 timer for all maps.
            weatherOverlayEffectsType = AccessTools.TypeByName("VFECore.WeatherOverlay_Effects");
            weatherOverlayEffectsNextDamageTickField = AccessTools.FieldRefAccess<int>(weatherOverlayEffectsType, "nextDamageTick");

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
                        weatherOverlayEffectsNextDamageTickField(overlay) = 0;
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
    }
}