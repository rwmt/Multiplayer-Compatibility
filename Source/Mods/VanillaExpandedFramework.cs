using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
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
        //// VFECore ////
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

        // Dialog_Hire
        private static Type hireDialogType;
        private static AccessTools.FieldRef<object, Dictionary<PawnKindDef, Pair<int, string>>> hireDataField;
        private static ISyncField daysAmountField;
        private static ISyncField currentFactionDefField;

        // Vanilla Furniture Expanded
        private static AccessTools.FieldRef<object, ThingComp> setStoneBuildingField;

        // Dialog_NewFactionSpawning
        private static Type newFactionSpawningDialogType;
        private static AccessTools.FieldRef<object, FactionDef> factionDefField;


        //// MVCF ////
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


        //// System ////
        // ConditionalWeakTable
        private static FastInvokeHandler conditionalWeakTableTryGetValueMethod;

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

        #region Main patches

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

        private static void PatchOtherRng()
        {
            PatchingUtilities.PatchPushPopRand(new[]
            {
                // AddHediff desyncs with Arbiter, but seems fine without it
                "VanillaCookingExpanded.Thought_Hediff:MoodOffset",
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
        {
            MpCompat.RegisterLambdaMethod("VFECore.CompPawnDependsOn", "CompGetGizmosExtra", 0).SetDebugOnly();
        }

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
            MP.RegisterSyncMethod(type, "CreateCastJob");
            MP.RegisterSyncWorker<ITargetingSource>(SyncVEFAbility, type, true);
            abilityAutoCastField = MP.RegisterSyncField(type, "autoCast");
            MpCompat.harmony.Patch(AccessTools.Method(type, "DoAction"),
                prefix: new HarmonyMethod(typeof(VanillaExpandedFramework), nameof(PreAbilityDoAction)),
                postfix: new HarmonyMethod(typeof(VanillaExpandedFramework), nameof(PostAbilityDoAction)));

            type = AccessTools.TypeByName("VFECore.CompShieldField");
            MpCompat.RegisterLambdaMethod(type, nameof(ThingComp.CompGetWornGizmosExtra), 0);
            MpCompat.RegisterLambdaMethod(type, "GetGizmos", 0, 2);
        }

        private static void PatchHireableFactions()
        {
            hireDialogType = AccessTools.TypeByName("VFECore.Misc.Dialog_Hire");

            MP.RegisterSyncMethod(hireDialogType, "OnAcceptKeyPressed");
            MP.RegisterSyncWorker<Window>(SyncHireDialog, hireDialogType);
            MP.RegisterSyncMethod(typeof(VanillaExpandedFramework), nameof(SyncedSetHireData));
            MP.RegisterSyncMethod(typeof(VanillaExpandedFramework), nameof(SyncedCloseHireDialog));
            hireDataField = AccessTools.FieldRefAccess<Dictionary<PawnKindDef, Pair<int, string>>>(hireDialogType, "hireData");
            // I don't think daysAmountBuffer needs to be synced, just daysAmount only
            daysAmountField = MP.RegisterSyncField(hireDialogType, "daysAmount");
            currentFactionDefField = MP.RegisterSyncField(hireDialogType, "curFaction");
            MpCompat.harmony.Patch(AccessTools.Method(hireDialogType, "DoWindowContents"),
                prefix: new HarmonyMethod(typeof(VanillaExpandedFramework), nameof(PreHireDialogDoWindowContents)),
                postfix: new HarmonyMethod(typeof(VanillaExpandedFramework), nameof(PostHireDialogDoWindowContents)));
        }

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

            type = AccessTools.TypeByName("VanillaFurnitureExpanded.CompRandomBuildingGraphic");
            MpCompat.RegisterLambdaMethod(type, "CompGetGizmosExtra", 0);

            type = AccessTools.TypeByName("VanillaFurnitureExpanded.CompGlowerExtended");
            MP.RegisterSyncMethod(type, "SwitchColor");
        }

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

            var conditionalWeakTableType = typeof(ConditionalWeakTable<,>).MakeGenericType(typeof(Pawn), type);
            conditionalWeakTableTryGetValueMethod = MethodInvoker.GetHandler(AccessTools.Method(conditionalWeakTableType, "TryGetValue"));

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

            type = AccessTools.TypeByName("MVCF.Reloading.Comps.VerbComp_Reloadable_ChangeableAmmo");
            var innerMethod = MpMethodUtil.GetLambda(type, "AmmoOptions", MethodType.Getter, null, 1);
            MP.RegisterSyncDelegate(type, innerMethod.DeclaringType.Name, innerMethod.Name);

            type = AccessTools.TypeByName("MVCF.Features.Feature_Humanoid");
            MpCompat.RegisterLambdaDelegate(type, "GetGizmos_Postfix", 1); // Fire at will
            MpCompat.RegisterLambdaDelegate(type, "GetAttackGizmos_Postfix", 4); // Interrupt Attack

            MpCompat.RegisterLambdaDelegate("MVCF.Features.Feature_RangedAnimals", "Pawn_GetGizmos_Postfix", 0); // Also interrupt Attack
        }

        private static void PatchExplosiveTrialsEffect()
        {
            // RNG
            PatchingUtilities.PatchPushPopRand("ExplosiveTrailsEffect.SmokeThrowher:ThrowSmokeTrail");
        }

        private static void PatchVanillaApparelExpanded()
        {
            MpCompat.RegisterLambdaMethod("VanillaApparelExpanded.CompSwitchApparel", "CompGetWornGizmosExtra", 0);
        }

        private static void PatchVanillaWeaponsExpanded()
        {
            MpCompat.RegisterLambdaMethod("VanillaWeaponsExpandedLaser.CompLaserCapacitor", "CompGetGizmosExtra", 1);
        }

        private static void PatchPipeSystem()
        {
            // Increase/decrease by 1/10
            MpCompat.RegisterLambdaMethod("PipeSystem.CompConvertToThing", "PostSpawnSetup", 0, 1, 2, 3);
            // (Dev) trigger countdown
            MpCompat.RegisterLambdaMethod("PipeSystem.CompExplosiveContent", "CompGetGizmosExtra", 0).SetDebugOnly();
            // Choose output
            MpCompat.RegisterLambdaMethod("PipeSystem.CompResourceProcessor", "PostSpawnSetup", 1);
            // Transfer/extract
            MpCompat.RegisterLambdaMethod("PipeSystem.CompResourceStorage", "PostSpawnSetup", 0, 1);
            // (Dev) fill/empty
            MpCompat.RegisterLambdaMethod("PipeSystem.CompResourceStorage", "CompGetGizmosExtra", 0, 1);
        }

        private static void PatchKCSG()
        {
            var type = AccessTools.TypeByName("KCSG.SettlementGenUtils");
            type = AccessTools.Inner(type, "Sampling");

            PatchingUtilities.PatchSystemRand(AccessTools.Method(type, "Sample"));

            // KCSG.SymbolResolver_ScatterStuffAround:Resolve uses seeder system RNG, should be fine
            // If not, will need patching
        }

        private static void PatchFactionDiscovery()
        {
            newFactionSpawningDialogType = AccessTools.TypeByName("VFECore.Dialog_NewFactionSpawning");
            factionDefField = AccessTools.FieldRefAccess<FactionDef>(newFactionSpawningDialogType, "factionDef");

            MP.RegisterSyncMethod(newFactionSpawningDialogType, "<SpawnWithBases>g__SpawnCallback|7_0");
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

        private static void PatchVanillaGenesExpanded()
        {
            var type = AccessTools.TypeByName("VanillaGenesExpanded.CompHumanHatcher");
            PatchingUtilities.PatchSystemRand(AccessTools.Method(type, "Hatch"));
            MpCompat.RegisterLambdaMethod(type, "CompGetGizmosExtra", 0).SetDebugOnly();
        }

        #endregion

        #region SyncWorkers and other sync stuff

        private static void SyncCommandWithBuilding(SyncWorker sync, ref Command command)
        {
            var traverse = Traverse.Create(command);
            var building = traverse.Field("building");

            if (sync.isWriting)
                sync.Write(building.GetValue() as Thing);
            else
                building.SetValue(sync.Read<Thing>());
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

        private static void SyncSetStoneTypeCommand(SyncWorker sync, ref Command obj)
        {
            if (sync.isWriting)
                sync.Write(setStoneBuildingField(obj));
            else
                setStoneBuildingField(obj) = sync.Read<ThingComp>();
        }

        private static void SyncVerbManager(SyncWorker sync, ref object obj)
        {
            if (sync.isWriting)
                // Sync the pawn that has the VerbManager
                sync.Write((Pawn)mvcfPawnGetter(obj, Array.Empty<object>()));
            else
            {
                var pawn = sync.Read<Pawn>();

                var weakTable = mvcfPawnVerbUtilityField(null);

                var outParam = new object[] { pawn, null };

                // Either try getting the VerbManager from the comp, or create it if it's missing
                if ((bool)conditionalWeakTableTryGetValueMethod(weakTable, outParam))
                    obj = outParam[1];
                else
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

                    var compAbilities = thing.AllComps.FirstOrDefault(c => c.GetType() == compAbilitiesType);
                    ThingComp compAbilitiesApparel = null;
                    if (compAbilities != null)
                        list = learnedAbilitiesField(compAbilities);

                    if (list == null)
                    {
                        compAbilitiesApparel = thing.AllComps.FirstOrDefault(c => c.GetType() == compAbilitiesApparelType);
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

            if (!Find.WindowStack.IsOpen(__instance))
                SyncedCloseHireDialog();
        }

        private static void SyncedSetHireData(Dictionary<PawnKindDef, Pair<int, string>> hireData)
        {
            var dialog = Find.WindowStack.Windows.FirstOrDefault(x => x.GetType() == hireDialogType);

            if (dialog != null)
                hireDataField(dialog) = hireData;
        }

        private static void SyncedCloseHireDialog()
            => Find.WindowStack.TryRemove(hireDialogType);

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
    }
}