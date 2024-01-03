using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>XVI-MECHFRAME by 旋风(andery233xj), 青叶(AOBA)，Bill Doors(3HST有限公司), more</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2844617885"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3108258542"/>
    [MpCompatFor("andery233xj.mod.MechanicalPoweredArmor")]
    [MpCompatFor("glencoe2004.MechanicalPoweredArmor")]
    public class XVIMechframe
    {
        #region Fields

        // AI combat
        private static AccessTools.FieldRef<object, Apparel> aiTrackerParentField;
        private static AccessTools.FieldRef<Apparel, object> mechAiTrackerField;

        // Skill system
        // CompSkills
        private static Type compSkillsType;
        private static AccessTools.FieldRef<ThingComp, IList> compSkillSkillsList;
        // SkillObject
        private static AccessTools.FieldRef<object, ThingComp> skillObjectParentCompField;
        private static AccessTools.FieldRef<object, object> skillObjectSkillField;
        private static FastInvokeHandler skillVerbTrackerGetter;
        // Skill
        private static AccessTools.FieldRef<object, string> skillIdField;

        // MP
        private static AccessTools.FieldRef<List<object>> prevSelectedField;

        #endregion

        public XVIMechframe(ModContentPack mod)
        {
            Type type;

            #region RNG

            {
                PatchingUtilities.PatchPushPopRand(new[]
                {
                    "MParmorLibrary.DamageWorker_ExplosionWithDirection_Ice:ExplosionAffectCell",
                    // Used by a def in an "Unused" directory...
                    // Def is most likely unused in the mod, but let's not risk it.
                    "MParmorLibrary.Unused.DamageWorker_ExplosionWithDirection:ExplosionAffectCell",
                });
            }

            #endregion

            #region Gizmos

            {
                // Dev charge
                MpCompat.RegisterLambdaMethod("MParmorLibrary.Building_ChargingStation", nameof(Building.GetGizmos), 0).SetDebugOnly();

                MP.RegisterSyncMethod(AccessTools.DeclaredMethod("MParmorLibrary.Building_FabricationPit:StopFabricating"));

                // Set charging power, called from Command_SetChargingPower
                type = AccessTools.TypeByName("MParmorLibrary.IChargingEquipment");
                MP.RegisterSyncWorker<object>(SyncIChargingEquipment, type, isImplicit: true);
                // type.AllSubclasses doesn't work when used on an interface, so either
                // manually go through each implementation and sync them, or implement
                // something like `AllSubclasses` but for interfaces. The second one
                // is too much work to only use once for 2 classes, soooo... Simple solution it is!
                MP.RegisterSyncMethod(AccessTools.PropertySetter("MParmorLibrary.Building_ChargingStation:ChargingPower"));
                MP.RegisterSyncMethod(AccessTools.PropertySetter("MParmorLibrary.Building_FabricationPit:ChargingPower"));

                // Dev charge battery 10000/1000/1
                MpCompat.RegisterLambdaMethod("MParmorLibrary.CompMParmorBuilding", "CompGetGizmosExtra", 0, 1, 2).SetDebugOnly();

                // Attack (0)/defend (1) specific target
                MpCompat.RegisterLambdaMethod("MParmorLibrary.Drone", nameof(Pawn.GetGizmos), 0, 2);

                // Health_Shiled [sic], no reference to the parent,
                // will have to check selected object to find the parent.
                type = AccessTools.TypeByName("MParmorLibrary.Health_Shiled");
                MP.RegisterSyncWorker<object>(SyncHealthShield, type);
                // Dev reset shield
                MP.RegisterSyncMethodLambda(type, "GetGizmos", 0);

                // Getting out, called from gizmo
                type = AccessTools.TypeByName("MParmorLibrary.MParmorCore");
                mechAiTrackerField = AccessTools.FieldRefAccess<object>(type, "aiTracker");
                MP.RegisterSyncMethod(type, "GetOutOfMParmor");

                // Exit and self destruct, called from gizmo
                MP.RegisterSyncMethod(AccessTools.DeclaredMethod("MParmorLibrary.MParmorSelfDestruct:SelfDestruct"));

                // Toggle AI for suit
                type = AccessTools.TypeByName("MParmorLibrary.MParmorT_AITracker");
                aiTrackerParentField = AccessTools.FieldRefAccess<Apparel>(type, "core");
                // Toggle active
                MpCompat.RegisterLambdaMethod(type, "GetSwitchGizmo", 1);
                MP.RegisterSyncWorker<object>(SyncMechAITracker, type);

                // Change the type
                var field = AccessTools.DeclaredField("Multiplayer.Client.AsyncTimeComp:prevSelected");
                if (field != null)
                    prevSelectedField = AccessTools.StaticFieldRefAccess<List<object>>(field);
                else
                    Log.Warning("Couldn't find field Multiplayer.Client.AsyncTimeComp:prevSelected, re-selection of descending wall on use won't work.");
                
                type = AccessTools.TypeByName("MParmorLibrary.Wall_DescendingWall");
                var method = MpMethodUtil.GetLambda(type, "Change", lambdaOrdinal: 0);
                MP.RegisterSyncMethod(method);

                if (prevSelectedField != null)
                {
                    MpCompat.harmony.Patch(method,
                        transpiler: new HarmonyMethod(typeof(XVIMechframe), nameof(DescendingWallInterceptNewWall)));
                }

                // Toggle self repair
                MpCompat.RegisterLambdaMethod("MParmorLibrary.Wall_SelfRepairing", nameof(Building.GetGizmos), 1);

                // Skills
                compSkillSkillsList = AccessTools.FieldRefAccess<IList>("MParmorLibrary.SkillSystem.CompSkills:skills");

                type = AccessTools.TypeByName("MParmorLibrary.SkillSystem.SkillObject");
                skillObjectParentCompField = AccessTools.FieldRefAccess<ThingComp>(type, "parent");
                skillObjectSkillField = AccessTools.FieldRefAccess<object>(type, "skill");
                skillVerbTrackerGetter = MethodInvoker.GetHandler(AccessTools.DeclaredPropertyGetter(type, "VerbTracker"));
                MP.RegisterSyncWorker<object>(SyncSkillObject, type);
                MP.RegisterSyncMethodLambda(type, "GetGizmoPetSkill", 0);
            }

            #endregion

            #region Dynamic Defs

            {
                // MP syncs defs by using their short hash.
                // Defs generated by this mod are missing the short hash,
                // thus causing MP to fail. Give them hashes to fix this.
                // The defs aren't given a short hash by RimWorld due to
                // the mod creating those defs in [StaticConstructorOnStartup],
                // which happens to run way after hashes are given.

                type = AccessTools.TypeByName("MParmorLibrary.RecipeGenerator");
                MpCompat.harmony.Patch(AccessTools.DeclaredMethod(type, "GenerateImpliedDefs_PreResolve"),
                    postfix: new HarmonyMethod(typeof(XVIMechframe), nameof(ReinitializeRecipeDefShortHashDictionary)));
                MpCompat.harmony.Patch(AccessTools.DeclaredMethod(type, "DefsFromRecipeMakers"),
                    postfix: new HarmonyMethod(typeof(XVIMechframe), nameof(GiveHashesToDefs)));
            }

            #endregion

            #region Caches

            // The mod uses quite a lot of static caches. Thankfully, it
            // seems that they should be MP safe as they operate on them
            // smartly (add on spawn, remove on despawn). Not only that,
            // they also clear most of their caches on ExposeData
            // (specifically, in post load init).

            // Bugfix: ShieldsBarrierCircle adds istelf to cache both
            // on spawn and despawn, but never removes itself.
            // Reporting it doesn't look like it'll achieve much, as
            // the mod seems to not really receive much support at the
            // moment. On top of that, it's closed source, so even if
            // we wanted to - we can't really contribute ourselves.
            // Probably the simplest way to fix this universally would
            // be to release a separate bugfix mod that would tackle
            // issues with the mod itself.
            MpCompat.harmony.Patch(AccessTools.DeclaredMethod("MParmorLibrary.ShieldsBarrierCircle:DeSpawn"),
                transpiler: new HarmonyMethod(typeof(XVIMechframe), nameof(FixCacheBug)));

            #endregion

            #region Verb Owner Syncing

            {
                // Insert VehicleHandler as supported thing holder for syncing.
                // The mod uses VehicleHandler as IThingHolder and ends up being synced.
                // We should add support for adding more supported thing holders soon... I think the PokéWorld mod would benefit from it as well.
                const string supportedVerbOwnersFieldPath = "Multiplayer.Client.RwImplSerialization:supportedVerbOwnerTypes";
                var supportedThingHoldersField = AccessTools.DeclaredField(supportedVerbOwnersFieldPath);
                var targetVerbOwnerType = AccessTools.TypeByName("MParmorLibrary.SkillSystem.SkillObject");
                if (supportedThingHoldersField == null)
                    Log.Error($"Trying to access {supportedVerbOwnersFieldPath} failed, field is null.");
                else if (!supportedThingHoldersField.IsStatic)
                    Log.Error($"Trying to access {supportedVerbOwnersFieldPath} failed, field is non-static.");
                else if (supportedThingHoldersField.GetValue(null) is not Type[] array)
                    Log.Error($"Trying to access {supportedVerbOwnersFieldPath} failed, the value is null or not Type[]. Value={supportedThingHoldersField.GetValue(null)}");
                else if (targetVerbOwnerType == null)
                    Log.Error($"Trying to insert into {supportedVerbOwnersFieldPath} failed, target type is null.");
                else
                {
                    // Increase size by 1
                    Array.Resize(ref array, array.Length + 1);
                    // Fill the last element
                    array[array.Length - 1] = targetVerbOwnerType;
                    // Set the original field to the value we set up
                    supportedThingHoldersField.SetValue(null, array);
                }
            }

            #endregion

            #region Shared Cross Refs

            {
                skillIdField = AccessTools.FieldRefAccess<string>("MParmorLibrary.SkillSystem.Skill:id");

                type = compSkillsType = AccessTools.TypeByName("MParmorLibrary.SkillSystem.CompSkills");
                PatchingUtilities.InitializeSharedCrossRefs();

                MpCompat.harmony.Patch(AccessTools.DeclaredMethod(type, "ResetAllSkills"),
                    prefix: new HarmonyMethod(typeof(XVIMechframe), nameof(PreResetAllSkills)),
                    postfix: new HarmonyMethod(typeof(XVIMechframe), nameof(PostResetAllSkills)));
                MpCompat.harmony.Patch(AccessTools.DeclaredMethod(type, "PostExposeData"),
                    postfix: new HarmonyMethod(typeof(XVIMechframe), nameof(PostCompSkillExposeData)));
                MpCompat.harmony.Patch(AccessTools.DeclaredMethod("MParmorLibrary.MParmorCore:GetOutOfMParmorForce"),
                    postfix: new HarmonyMethod(typeof(XVIMechframe), nameof(PostGetOutOfArmor)));
                MpCompat.harmony.Patch(AccessTools.DeclaredMethod("MParmorLibrary.MParmorCore:SelfDestruct"),
                    postfix: new HarmonyMethod(typeof(XVIMechframe), nameof(PostGetOutOfArmor)));

                // Make verb ID truly unique
                MpCompat.harmony.Patch(AccessTools.DeclaredMethod("MParmorLibrary.SkillSystem.SkillObject:UniqueVerbOwnerID"),
                    prefix: new HarmonyMethod(typeof(XVIMechframe), nameof(PreSkillObjectUniqueVerbOwnerID)));
            }

            #endregion
        }

        #region Sync workers

        private static void SyncIChargingEquipment(SyncWorker sync, ref object equipment)
        {
            if (sync.isWriting)
            {
                // Should only ever be a subtype of Thing, likely it should never be null
                if (equipment is Thing t)
                {
                    sync.Write(t);
                }
                else
                {
                    if (equipment != null)
                        Log.Error($"Trying to sync IChargingEquipment, but it's not of type (or a subtype of) Thing. Actual type={equipment.GetType()}, object={equipment}");
                    sync.Write<Thing>(null);
                }
            }
            else
            {
                equipment = sync.Read<Thing>();
            }
        }

        private static void SyncHealthShield(SyncWorker sync, ref object shield)
        {
            // Normally I'd use FieldRef, but there's multiple types
            // that could be handled here, each has the same name
            // for the field... Plus, it's only for dev mode gizmo
            // anyway, so not bothering optimizing this sync worker.
            static object TryGetShield(Thing t) => Traverse.Create(t).Field("shieldClass").GetValue();

            if (sync.isWriting)
            {
                if (shield == null)
                {
                    sync.Write<Thing>(null);
                }
                else
                {
                    foreach (var selected in Find.Selector.SelectedObjects)
                    {
                        if (selected is Thing thing && shield == TryGetShield(thing))
                        {
                            sync.Write(thing);
                            break;
                        }
                    }
                }
            }
            else
            {
                var parent = sync.Read<Thing>();
                if (parent != null)
                    shield = TryGetShield(parent);
            }
        }

        private static void SyncMechAITracker(SyncWorker sync, ref object aiTracker)
        {
            if (sync.isWriting)
                sync.Write(aiTrackerParentField(aiTracker));
            else
                aiTracker = mechAiTrackerField(sync.Read<Apparel>());
        }

        private static void SyncSkillObject(SyncWorker sync, ref object skillObject)
        {
            if (sync.isWriting)
            {
                if (skillObject == null)
                {
                    sync.Write(byte.MaxValue);
                    return;
                }

                var comp = skillObjectParentCompField(skillObject);
                if (comp == null)
                {
                    Log.Error($"Trying to sync SkillObject with no parent comp, target={skillObject}");
                    sync.Write(byte.MaxValue);
                    return;
                }

                var index = compSkillSkillsList(comp).IndexOf(skillObject);
                if (index < 0)
                {
                    Log.Error($"Trying to sync SkillObject but the parent comp doesn't contain it, target={skillObject}, comp={comp}");
                    sync.Write(byte.MaxValue);
                    return;
                }

                // Sync the index as a byte, as there's always less than 10 abilities in the parent
                sync.Write((byte)index);
                sync.Write(comp);
            }
            else
            {
                var index = sync.Read<byte>();
                if (index != byte.MaxValue)
                    skillObject = compSkillSkillsList(sync.Read<ThingComp>())[index];
            }
        }

        #endregion

        #region Dynamic Def

        private static IEnumerable<RecipeDef> GiveHashesToDefs(IEnumerable<RecipeDef> defs)
        {
            if (!ShortHashGiver.takenHashesPerDeftype.TryGetValue(typeof(RecipeDef), out var usedHashes))
            {
                usedHashes = new HashSet<ushort>();
                ShortHashGiver.takenHashesPerDeftype.Add(typeof(RecipeDef), usedHashes);
            }

            foreach (var def in defs)
            {
                if (def.shortHash != default)
                {
                    // This shouldn't ever happen unless the mod actually decides to fix it.
                    Log.Warning($"Trying to give a new {nameof(Def.shortHash)} to {def.defName}, but it already has one: {def.shortHash}");
                }
                else
                {
                    // Second parameter is unused by the method.
                    // May as well provide it just in case.
                    ShortHashGiver.GiveShortHash(def, typeof(RecipeDef), usedHashes);
                }

                // Since it's a pass through postfix, we need to yield return the values.
                yield return def;
            }

            // Can't reinitialize the dictionary here, as the defs
            // haven't been added to database yet.
        }

        private static void ReinitializeRecipeDefShortHashDictionary()
            => DefDatabase<RecipeDef>.InitializeShortHashDictionary();

        #endregion

        #region Selector

        private static void InterceptNewWall(Building oldWall, Building newWall)
        {
            if (!MP.IsInMultiplayer)
                return;

            // Find.Selector will return an empty list of selected objects
            // due to us being in a synced method, same with Pre/Post invoke.
            // Likewise, if we select something, that will end up being discarded.
            // As a workaround, we directly check and modify original selector list
            // that MP stores in a separate field.
            // 
            // An alternative approach to this would be to use some other workaround,
            // like checking all selected objects each tick or storing a reference to
            // the true selector here that MP won't be able to modify.
            var prevSelector = prevSelectedField();
            if (prevSelector != null && prevSelector.Contains(oldWall))
                prevSelector.Add(newWall); // The old wall will be auto-deselected
        }

        private static IEnumerable<CodeInstruction> DescendingWallInterceptNewWall(IEnumerable<CodeInstruction> instr, MethodBase baseMethod)
        {
            var patchedAnything = false;

            foreach (var ci in instr)
            {
                yield return ci;

                // There should ever be a single Stloc_2 call
                if (!patchedAnything && ci.opcode == OpCodes.Stloc_2)
                {
                    patchedAnything = true;

                    // Push `this` to the top of the stack
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    // Push the new `Wall_DescendingWall` object to the top of the stack
                    yield return new CodeInstruction(OpCodes.Ldloc_2);
                    // Call our method to get the newly created wall
                    yield return CodeInstruction.Call(typeof(XVIMechframe), nameof(InterceptNewWall));
                }
            }

            if (!patchedAnything)
            {
                var name = (baseMethod.DeclaringType?.Namespace).NullOrEmpty() ? baseMethod.Name : $"{baseMethod.DeclaringType!.Name}:{baseMethod.Name}";
                Log.Warning($"Failed to intercept assignment to Wall_DescendingWall for method {name}");
            }
        }

        #endregion

        #region Shared Cross Refs

        private static bool PreSkillObjectUniqueVerbOwnerID(object __instance, ThingComp ___parent, ref string __result)
        {
            // SkillObject uses "MParmorSkillSystem_" + `Thing.ThingID` as its ID.
            // Due to each comp having multiple skills, this results in multiple
            // verbs sharing the same ID, causing issues with MP syncing.

            // Use the ID that the skills are assigned for the extra part in the ID.
            var skillId = skillIdField(skillObjectSkillField(__instance));
            __result = $"MParmorSkillSystem_{skillId}_{___parent.parent.ThingID}";
            return false;
        }

        private static void PreResetAllSkills(IList ___skills)
        {
            // Check for inactive scribe, method could be called from PostExposeData
            if (MP.IsInMultiplayer && Scribe.mode == LoadSaveMode.Inactive)
                UnregisterCrossRefs(___skills); // The method clears the skill list
        }

        private static void PostResetAllSkills(IList ___skills)
        {
            // Check for inactive scribe, method could be called from PostExposeData
            if (MP.IsInMultiplayer && Scribe.mode == LoadSaveMode.Inactive)
                RegisterCrossRefs(___skills); // And recreates it afterwards
        }

        private static void PostCompSkillExposeData(IList ___skills)
        {
            if (MP.IsInMultiplayer && Scribe.mode == LoadSaveMode.PostLoadInit)
                RegisterCrossRefs(___skills);
        }

        private static void PostGetOutOfArmor(Apparel __instance)
        {
            if (!MP.IsInMultiplayer || !MP.IsExecutingSyncCommand)
                return;

            foreach (var comp in __instance.AllComps)
            {
                if (compSkillsType.IsInstanceOfType(comp))
                    UnregisterCrossRefs(compSkillSkillsList(comp));
            }
        }

        private static void RegisterCrossRefs(IList skills)
        {
            foreach (var skill in skills)
            {
                // We could technically (un)register the skill object here
                // as well. However, it would end up being completely pointless.
                if (skillVerbTrackerGetter(skill) is VerbTracker verbs)
                {
                    foreach (var verb in verbs.AllVerbs)
                        PatchingUtilities.RegisterSharedCrossRef(verb);
                }
            }
        }

        private static void UnregisterCrossRefs(IList skills)
        {
            foreach (var skill in skills)
            {
                // We could technically (un)register the skill object here
                // as well. However, it would end up being completely pointless.
                if (skillVerbTrackerGetter(skill) is VerbTracker verbs)
                {
                    foreach (var verb in verbs.AllVerbs)
                        PatchingUtilities.UnregisterSharedCrossRef(verb);
                }
            }
        }

        #endregion

        #region Bugfix

        private static IEnumerable<CodeInstruction> FixCacheBug(IEnumerable<CodeInstruction> instr, MethodBase baseMethod)
        {
            var type = AccessTools.TypeByName("MParmorLibrary.Intercepts");
            var target = AccessTools.DeclaredMethod(type, "AddNewInstance");
            var replacement = AccessTools.DeclaredMethod(type, "RemoveInstance");
            var safePatch = target != null && replacement != null;
            var replacedCount = 0;

            foreach (var ci in instr)
            {
                if (safePatch && ci.opcode == OpCodes.Call && ci.operand as MethodInfo == target)
                {
                    ci.operand = replacement;
                    replacedCount++;
                }

                yield return ci;
            }

            const int expected = 1;
            if (!safePatch)
            {
                var name = (baseMethod.DeclaringType?.Namespace).NullOrEmpty() ? baseMethod.Name : $"{baseMethod.DeclaringType!.Name}:{baseMethod.Name}";
                Log.Warning($"Failed to replace Intercepts.AddNewInstance calls to Intercepts.RemoveInstance (Add null={target == null}, Remove null={replacement == null}) for method {name}");
            }
            else if (replacedCount != expected)
            {
                var name = (baseMethod.DeclaringType?.Namespace).NullOrEmpty() ? baseMethod.Name : $"{baseMethod.DeclaringType!.Name}:{baseMethod.Name}";
                Log.Warning($"Replaced incorrect number of Intercepts.AddNewInstance calls to Intercepts.RemoveInstance (is it still needed?) (replaced {replacedCount}, expected {expected}) for method {name}");
            }
        }

        #endregion
    }
}