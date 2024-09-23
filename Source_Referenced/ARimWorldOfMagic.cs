using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using AbilityUser;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using TorannMagic;
using TorannMagic.Golems;
using TorannMagic.TMDefs;
using TorannMagic.Utils;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Multiplayer.Compat;

/// <summary>A RimWorld of Magic by Torann</summary>
/// <see href="https://github.com/TorannD/RWoM"/>
/// <see href="https://steamcommunity.com/workshop/filedetails/?id=1201382956"/>
[MpCompatFor("Torann.ARimworldOfMagic")]
public class ARimWorldOfMagic
{
    #region Fields

    // JobDriver_PortalDestination.<>c__DisplayClass7_0
    private static AccessTools.FieldRef<object, JobDriver_PortalDestination> portalDestinationInnerClassThisField;
    // Building_TMPortal.<>c__DisplayClass43_0
    private static AccessTools.FieldRef<object, Building_TMPortal> portalBuildingInnerClassThisField;

    // TMPawnGolem
    [MpCompatSyncField(typeof(TMPawnGolem), nameof(TMPawnGolem.showDormantPosition))]
    protected static ISyncField compGolemShowDormantPosition;
    // CompGolem sync fields
    [MpCompatSyncField(typeof(CompGolem), nameof(CompGolem.followsMaster))]
    protected static ISyncField compGolemFollowsMaster;
    [MpCompatSyncField(typeof(CompGolem), nameof(CompGolem.followsMasterDrafted))]
    protected static ISyncField compGolemFollowsMasterDrafted;
    [MpCompatSyncField(typeof(CompGolem), nameof(CompGolem.checkThreatPath))]
    protected static ISyncField compGolemCheckThreatPath;
    [MpCompatSyncField(typeof(CompGolem), nameof(CompGolem.remainDormantWhenUpgrading))]
    protected static ISyncField compGolemRemainDormantWhenUpgrading;
    [MpCompatSyncField(typeof(CompGolem), nameof(CompGolem.useAbilitiesWhenDormant))]
    protected static ISyncField compGolemUseAbilitiesWhenDormant;
    [MpCompatSyncField(typeof(CompGolem), nameof(CompGolem.threatRange), bufferChanges = true)]
    protected static ISyncField compGolemThreatRange;
    [MpCompatSyncField(typeof(CompGolem), nameof(CompGolem.minEnergyPctForAbilities), bufferChanges = true)]
    protected static ISyncField compGolemMinEnergyPctForAbilities;
    [MpCompatSyncField(typeof(CompGolem), nameof(CompGolem.energyPctShouldRest), bufferChanges = true)]
    protected static ISyncField compGolemEnergyPctShouldRest;
    [MpCompatSyncField(typeof(CompGolem), nameof(CompGolem.energyPctShouldAwaken), bufferChanges = true)]
    protected static ISyncField compGolemEnergyPctShouldAwaken;

    #endregion

    #region Main Patch

    public ARimWorldOfMagic(ModContentPack mod)
    {
        LongEventHandler.ExecuteWhenFinished(LatePatch);

        #region RNG Patching

        {
            var systemRngMethods = new[]
            {
                "TorannMagic.Laser_LightningBolt:Explosion",
                "TorannMagic.Projectile_Icebolt:Explosion",
                "TorannMagic.Projectile_LightningCloud:Explosion",
                "TorannMagic.TM_Action:DoAction_ApplySplashDamage",
                "TorannMagic.Projectile_LightningStorm:Explosion",
                "TorannMagic.Projectile_Overwhelm:Explosion",
                "TorannMagic.FlyingObject_Advanced_Icebolt:Explosion",
                "TorannMagic.Projectile_Snowball:Explosion",
                "TorannMagic.Projectile_SummonElemental:Impact",
                "TorannMagic.Projectile_SummonMinion:Impact",
                "TorannMagic.Projectile_SummonPoppi:Impact",
                "TorannMagic.Weapon.Projectile_FireWand:Explosion",
                "TorannMagic.Weapon.Projectile_LightningWand:Explosion",
                "TorannMagic.Weapon.SeerRing_Fire:Explosion",
                "TorannMagic.Weapon.SeerRing_Lightning:Explosion",
                "TorannMagic.Projectile_DisablingShot:Impact",
                "TorannMagic.Projectile_Fireball:Explosion",
                "TorannMagic.Projectile_Fireclaw:Explosion",
                "TorannMagic.Verb_ShootDifferentProjectiles:TryCastShot",
            };

            PatchingUtilities.PatchSystemRand(systemRngMethods, false);

            foreach (var method in AccessTools.GetDeclaredMethods(typeof(TM_MoteMaker)))
            {
                // Shouldn't happen as the type itself is static
                if (!method.IsStatic)
                {
                    Log.Warning($"{nameof(TM_MoteMaker)} had a non-static method: {method.Name}");
                    continue;
                }

                // Skip if returns anything (skip MakeOverlay calls, no need to patch them)
                if (method.ReturnType != typeof(void))
                    continue;

                var shouldPatch = method.Name switch
                {
                    // Handle ambiguous matches
                    "ThrowDiseaseMote" when method.GetParameters().Length != 6 => false,
                    "ThrowArcaneMote" when method.GetParameters().Length != 8 => false,
                    "ThrowShadowMote" when method.GetParameters().Length != 6 => false,
                    // ThrowTwinkle has ambiguous match, but we need to patch both.
                    _ => true,
                };

                if (shouldPatch)
                    PatchingUtilities.PatchPushPopRand(method);
            }
        }

        #endregion

        #region Gizmos

        {
            MP.RegisterSyncMethod(typeof(TM_Action), nameof(TM_Action.PromoteWanderer));
            MP.RegisterSyncMethod(typeof(TM_Action), nameof(TM_Action.PromoteWayfarer));
            MP.RegisterSyncMethod(typeof(TM_Action), nameof(TM_Action.RemoveSymbiosisCommand));

            // Target specific cell
            MP.RegisterSyncMethodLambda(typeof(Building_60mmMortar), nameof(Building_60mmMortar.GetGizmos), 0);
            // Replicate thing, remove replication bills (1)
            MpCompat.RegisterLambdaMethod(typeof(Building_TMArcaneForge), nameof(Building_TMArcaneForge.GetGizmos), 0, 1);
        }

        #endregion

        #region Golems

        {
            // Gizmos
            MP.RegisterSyncMethod(typeof(Building_TMGolemBase), nameof(Building_TMGolemBase.InterfaceChangeTargetTemperature));
            // Hold fire (1), force attack target (3), toggle glowing (4), reset temperature (8)
            MpCompat.RegisterLambdaMethod(typeof(Building_TMGolemBase), nameof(Building_TMGolemBase.GetGizmos), 1, 3, 4, 8);
            // Activate
            MP.RegisterSyncDelegateLambda(typeof(Building_TMGolemBase), nameof(Building_TMGolemBase.GetGizmos), 0);
            // Deactivate (0), set a rest position (1), draft (3), hold fire (4)
            MpCompat.RegisterLambdaMethod(typeof(TMPawnGolem), nameof(TMPawnGolem.GetGizmos), 0, 1, 3, 4);

            // Assign pawn as a golem's master
            MP.RegisterSyncDelegateLambda(typeof(GolemUtility), nameof(GolemUtility.MasterButton), 1);
        }

        #endregion

        // TODO:
        // Test Golems
        // Check for more stuff
    }

    private static void LatePatch()
    {
        MpCompatPatchLoader.LoadPatch<ARimWorldOfMagic>();

        #region RNG

        {
            var systemRngMethods = new[]
            {
                "TorannMagic.Building_TMElementalRift_Defenders:DetermineElementalType",
                "TorannMagic.Building_TMElementalRift:DetermineElementalType",
                "TorannMagic.FlyingObject_ValiantCharge:Explosion",
                "TorannMagic.FlyingObject_Whirlwind:ApplyWhirlwindDamage",
                "TorannMagic.FlyingObject_DemonFlight:Explosion",
                "TorannMagic.Projectile_ValiantCharge:Explosion",
                "TorannMagic.MovingObject:ApplyWhirlwindDamage",
                "TorannMagic.Verb_Cleave:ApplyCleaveDamage",
            };

            PatchingUtilities.PatchSystemRand(systemRngMethods);
        }

        #endregion

        #region Gizmos

        {
            // Toggle: techno bit (3), techno bit repair (5), elemental shot (7)
            MpCompat.RegisterLambdaMethod(typeof(CompAbilityUserMagic), nameof(CompAbilityUserMagic.GetGizmoCommands), 3, 5, 7);

            // Toggle: cleave (2), CQC (4), psionic augmentation (6), psionic mind attack (8)
            MpCompat.RegisterLambdaMethod(typeof(CompAbilityUserMight), nameof(CompAbilityUserMight.GetGizmoCommands), 2, 4, 6, 8);
        }

        #endregion

        #region ITab

        {
            // Magic //
            // Dev mode level up/reset skills from the ITab
            MP.RegisterSyncMethod(typeof(CompAbilityUserMagic), nameof(CompAbilityUserMagic.LevelUp)).SetDebugOnly();
            MP.RegisterSyncMethod(typeof(CompAbilityUserMagic), nameof(CompAbilityUserMagic.ResetSkills)).SetDebugOnly();

            // Might //
            // Dev mode level up/reset skills from the ITab
            MP.RegisterSyncMethod(typeof(CompAbilityUserMight), nameof(CompAbilityUserMight.LevelUp)).SetDebugOnly();
            MP.RegisterSyncMethod(typeof(CompAbilityUserMight), nameof(CompAbilityUserMight.ResetSkills)).SetDebugOnly();
        }

        #endregion

        #region Portal

        {
            // Only sync 2 fields (map, <>4__this) as we don't need (or want) the other one (myMap) as it would cause issues.
            MpCompat.RegisterLambdaDelegate(typeof(JobDriver_PortalDestination), nameof(JobDriver_PortalDestination.ChooseWorldTarget), ["map", "<>4__this"], 1)[0]
                .CancelIfAnyFieldNull()
                // We don't use a real JobDriver, so we need to sync the important data.
                // We can't even expose it, as the mod doesn't do that for one of the fields
                // we need to sync. This field may cause issues with JobDriver on save/reload.
                .TransformField("<>4__this", Serializer.New(
                    (JobDriver_PortalDestination job) => (Pawn: job.pawn, Portal: job.portalBldg),
                    networked => new JobDriver_PortalDestination
                    {
                        pawn = networked.Pawn,
                        comp = networked.Pawn.GetCompAbilityUserMagic(),
                        portalBldg = networked.Portal,
                    }))
                // Syncing data on potentially multiple maps, and MP doesn't allow for that.
                // We need to manually sync the other map here to handle this.
                .TransformField("map", Serializer.New(
                    (Map map) => map.uniqueID,
                    id => Find.Maps.Find(map => map.uniqueID == id)));

            // Prepare field refs for compiled generated classes (over using slow Traverse)
            // JobDriver
            var lambda = MpMethodUtil.GetLambda(typeof(JobDriver_PortalDestination), nameof(JobDriver_PortalDestination.ChooseWorldTarget), lambdaOrdinal: 1);
            portalDestinationInnerClassThisField = AccessTools.FieldRefAccess<JobDriver_PortalDestination>(lambda.DeclaringType, "<>4__this");

            // Building itself
            lambda = MpMethodUtil.GetLambda(typeof(Building_TMPortal), nameof(Building.GetFloatMenuOptions), lambdaOrdinal: 0);
            portalBuildingInnerClassThisField = AccessTools.FieldRefAccess<Building_TMPortal>(lambda.DeclaringType, "<>4__this");
        }

        #endregion
    }

    #endregion

    #region Living Wall Multithreading

    // Living wall works by targeting a wall that will become "alive",
    // visible by an icon (projectile) on top of it. It'll move around
    // the wall when there's enemies on the map, trying to approach them.
    // It will attack adjacent enemies and repair parts of the wall it's in.
    // 
    // Living wall ticking uses MP unsafe multithreading, so we need to
    // get rid of it and replace it with a safe alternative.
    // 
    // Another issue here is with multiple maps. The living wall has a static
    // Pawn field for the target to attack, however it's assigned from one
    // thread, accessed in another, and if there's living walls on multiple
    // maps it may attempt to target the pawn from an inactive map. Sadly
    // not something we can fix without making more major changes.

    private static bool ReplacedDirectPathThread(bool threadLocked, FlyingObject_LivingWall instance)
    {
        // If not in MP, return the field that was accessed here.
        // It'll use multithreading out of MP.
        if (!MP.IsInMultiplayer)
            return threadLocked;

        // This thing does not care for the proper map of the
        // threat. Ignore executing if not the same map. Also,
        // this method will only be called if the closestThreat
        // is not null, so no reason to check it for null here.
        // Also, make sure to do the check when idleFor is 0,
        // as otherwise it'll keep repeatedly performing this
        // expensive operation on the first possible tick. This
        // will ensure we only do it on the last possible tick.
        if (instance.idleFor == 0 && instance.Map == FlyingObject_LivingWall.closestThreat.Map && instance.OccupiedWall != null)
        {
            // As opposed to the other thread, this one is a bit heavier.
            // It will, attempt to find a new path towards the closest
            // threat. It checks all the walls in the map for the closest
            // one, and then tries to calculate path towards it.
            // When this call ends it'll go idle for at least 5 or 60 ticks,
            // depending on if there's a target path or not.
            instance.DirectPath();
        }

        // If MP, return true (will be negated in the if statement) to prevent
        // the method from starting a new thread.
        return true;
    }

    private static bool ReplacedDoThreadedActionsThread(bool threadLocked, FlyingObject_LivingWall instance)
    {
        // If not in MP, return the field that was accessed here.
        // It'll use multithreading out of MP.
        if (!MP.IsInMultiplayer)
            return threadLocked;

        // Doesn't seem like there's much that would cause performance issues
        // if on main thread. There's 3 checks that are done periodically
        // based on current tick. There's also another check that's only
        // done when there's a target position (different from current one)
        // and there's no current path set.
        var wallUpdate = instance.nextWallUpdate;
        instance.DoThreadedActions();

        // Connected wall update is a pretty expensive operation,
        // make it less common. Normally happens once every 10-20
        // ticks, change it to 60-120.
        if (wallUpdate > instance.nextWallUpdate)
            instance.nextWallUpdate *= 6;

        // If MP, return true (will be negated in the if statement) to prevent
        // the method from starting a new thread.
        return true;
    }

    [MpCompatTranspiler(typeof(FlyingObject_LivingWall), nameof(FlyingObject_LivingWall.Tick))]
    private static IEnumerable<CodeInstruction> ReplaceLivingWallMultithreading(IEnumerable<CodeInstruction> instr, MethodBase baseMethod)
    {
        var targetField = AccessTools.DeclaredField(typeof(FlyingObject_LivingWall), nameof(FlyingObject_LivingWall.threadLocked));
        var firstTarget = MpMethodUtil.MethodOf(ReplacedDirectPathThread);
        var secondTarget = MpMethodUtil.MethodOf(ReplacedDoThreadedActionsThread);
        var encounteredFields = 0;

        foreach (var ci in instr)
        {
            yield return ci;

            if (ci.opcode == OpCodes.Ldfld && ci.operand is FieldInfo field && field == targetField)
            {
                if (encounteredFields < 2)
                {
                    // Insert "this" argument
                    yield return new CodeInstruction(OpCodes.Ldarg_0);

                    // Call our extra method
                    yield return new CodeInstruction(OpCodes.Call,
                        encounteredFields == 0
                            ? firstTarget
                            : secondTarget);
                }

                encounteredFields++;
            }
        }

        const int expected = 2;
        if (encounteredFields != expected)
        {
            var name = (baseMethod.DeclaringType?.Namespace).NullOrEmpty() ? baseMethod.Name : $"{baseMethod.DeclaringType!.Name}:{baseMethod.Name}";
            Log.Warning($"Tried to replace incorrect number of calls to FlyingObject_LivingWall.threadLocked field (encountered {encounteredFields}, expected {expected}) for method {name}");
        }
    }

    // Clear the active threat, as it's not synced when someone joins and will cause desync.
    [MpCompatPostfix(typeof(GameComponentUtility), nameof(GameComponentUtility.FinalizeInit))]
    private static void ClearTargetAfterLoading() => FlyingObject_LivingWall.closestThreat = null;

    #endregion

    #region Autocasting

    private static void SyncMagicPowerAutoCast(MagicPower magicPower, bool target, Pawn pawn)
    {
        if (!MP.IsInMultiplayer)
        {
            magicPower.AutoCast = target;
            return;
        }

        // Prevent spam
        // The gizmo normally has a 5 tick cooldown before being able to change the auto cast state
        if (magicPower.interactionTick >= Find.TickManager.TicksGame)
            return;
        magicPower.interactionTick = Find.TickManager.TicksGame + 5;

        SyncedSetMagicAutoCast(pawn, magicPower.abilityDef, target);
    }

    private static void SyncMightPowerAutoCast(MightPower mightPower, bool target, Pawn pawn)
    {
        if (!MP.IsInMultiplayer)
        {
            mightPower.AutoCast = target;
            return;
        }

        // Prevent spam
        // The gizmo normally has a 5 tick cooldown before being able to change the auto cast state
        if (mightPower.interactionTick >= Find.TickManager.TicksGame)
            return;
        mightPower.interactionTick = Find.TickManager.TicksGame + 5;

        SyncedSetMightAutoCast(pawn, mightPower.abilityDef, target);
    }

    [MpCompatSyncMethod(cancelIfAnyArgNull = true)]
    private static void SyncedSetMagicAutoCast(Pawn pawn, AbilityUser.AbilityDef abilityDef, bool target)
    {
        var power = pawn.GetCompAbilityUserMagic()?.MagicData?.AllMagicPowers?.Find(p => p.abilityDef == abilityDef);
        if (power == null)
            return;

        // Reset the interaction tick, otherwise the setter may not change the value as it was "interacted" with too recently
        power.interactionTick = 0;
        power.AutoCast = target;
    }

    [MpCompatSyncMethod(cancelIfAnyArgNull = true)]
    private static void SyncedSetMightAutoCast(Pawn pawn, AbilityUser.AbilityDef abilityDef, bool target)
    {
        var power = pawn.GetCompAbilityUserMight()?.MightData?.AllMightPowers?.Find(p => p.abilityDef == abilityDef);
        if (power == null)
            return;

        // Reset the interaction tick, otherwise the setter may not change the value as it was "interacted" with too recently
        power.interactionTick = 0;
        power.AutoCast = target;
    }

    [MpCompatTranspiler(typeof(TM_Action), nameof(TM_Action.DrawAutoCastForGizmo))]
    private static IEnumerable<CodeInstruction> SyncAutocastingSetter(IEnumerable<CodeInstruction> instr)
    {
        var magicAutoCastTarget = AccessTools.DeclaredPropertySetter(typeof(MagicPower), nameof(MagicPower.AutoCast));
        var mightAutoCastTarget = AccessTools.DeclaredPropertySetter(typeof(MightPower), nameof(MightPower.AutoCast));
        var magicAutoCastReplacement = AccessTools.Method(typeof(ARimWorldOfMagic), nameof(SyncMagicPowerAutoCast));
        var mightAutoCastReplacement = AccessTools.Method(typeof(ARimWorldOfMagic), nameof(SyncMightPowerAutoCast));

        var pawnAbilityField = AccessTools.Field(typeof(Command_PawnAbility), nameof(Command_PawnAbility.pawnAbility));
        var pawnGetter = AccessTools.PropertyGetter(typeof(PawnAbility), nameof(PawnAbility.Pawn));

        foreach (var ci in instr)
        {
            if (ci.opcode == OpCodes.Callvirt && ci.operand is MethodInfo method)
            {
                var loadPawnParam = false;

                if (method == magicAutoCastTarget)
                {
                    // Replace the method call with our own
                    ci.operand = magicAutoCastReplacement;
                    loadPawnParam = true;
                }
                else if (method == mightAutoCastTarget)
                {
                    // Replace the method call with our own
                    ci.operand = mightAutoCastReplacement;
                    loadPawnParam = true;
                }

                if (loadPawnParam)
                {
                    // Load the command
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    // Get the PawnAbility field
                    yield return new CodeInstruction(OpCodes.Ldfld, pawnAbilityField);
                    // Call the Pawn getter, to be passed as argument to our method
                    yield return new CodeInstruction(OpCodes.Callvirt, pawnGetter);
                }
            }

            yield return ci;
        }
    }

    #endregion

    #region Verify synced methods pre execution

    [MpCompatPrefix(typeof(TM_Action), nameof(TM_Action.PromoteWanderer))]
    private static bool ShouldCancelPromoteWanderer(Pawn pawn) => ShouldCancelPromotion(pawn, "TM_Gifted");

    [MpCompatPrefix(typeof(TM_Action), nameof(TM_Action.PromoteWayfarer))]
    private static bool ShouldCancelPromoteWayfarer(Pawn pawn) => ShouldCancelPromotion(pawn, "PhysicalProdigy");

    private static bool ShouldCancelPromotion(Pawn pawn, string requiredTraitDefName)
    {
        if (!MP.IsInMultiplayer || !MP.IsExecutingSyncCommand || pawn?.story?.traits?.allTraits == null)
            return true;

        // Only allow execution if the pawn still has the required trait
        return pawn.story.traits.allTraits.Any(trait => trait.def.defName == requiredTraitDefName);
    }

    [MpCompatPrefix(typeof(Building_TMArcaneForge), nameof(Building_TMArcaneForge.GetGizmos), 0)]
    private static bool PreReplicateRecipe(Building_WorkTable __instance)
    {
        if (!MP.IsInMultiplayer || !MP.IsExecutingSyncCommand)
            return true;

        // The mod doesn't let you pick a new object to replicate if
        // there's a replication recipe already active.
        return __instance.BillStack.Bills.All(bill => bill.recipe.defName != "ArcaneForge_Replication");
    }

    [MpCompatPrefix(typeof(JobDriver_PortalDestination), nameof(JobDriver_PortalDestination.ChooseWorldTarget), 1)]
    private static bool PreChoosePortalDestination(object __instance)
    {
        if (!MP.IsInMultiplayer || !MP.IsExecutingSyncCommand)
            return true;

        var jobDriver = portalDestinationInnerClassThisField(__instance);
        // Cannot setup a portal if it has a target already
        if (jobDriver.portalBldg == null || jobDriver.portalBldg.isPaired)
            return false;

        var comp = jobDriver.pawn?.GetCompAbilityUserMagic();
        if (comp == null)
            return false;

        // Make sure the pawn has the prerequisite spell, the field
        // needs to be true or spell's power needs to be learned.
        if (!comp.spell_FoldReality && !comp.MagicData.MagicPowersA.FirstOrDefault(p => p.abilityDef == TorannMagicDefOf.TM_FoldReality).learned)
            return false;

        // Make sure pawn still has enough mana
        return comp.Mana is { CurLevel: >= 0.7f };
    }

    #endregion

    #region Fix comp syncing

    // Magic/might comps fails to sync (likely due to not having props)
    // so we make a sync worker delegate for them both to handle them.
    [MpCompatSyncWorker(typeof(CompAbilityUserMagic))]
    private static void SyncMagicUserComp(SyncWorker sync, ref ThingComp comp)
    {
        if (sync.isWriting)
            sync.Write(comp?.parent);
        else
            comp = sync.Read<ThingWithComps>()?.GetCompAbilityUserMagic();
    }

    [MpCompatSyncWorker(typeof(CompAbilityUserMight))]
    private static void SyncMightUserComp(SyncWorker sync, ref ThingComp comp)
    {
        if (sync.isWriting)
            sync.Write(comp?.parent);
        else
            comp = sync.Read<ThingWithComps>()?.GetCompAbilityUserMight();
    }

    #endregion

    #region Improve portal target selection

    // Portals require you to order a pawn to pick destination, which creates a job,
    // which then opens world map and requires you to select a destination. In MP,
    // this is rather inconvenient. Skip the job entirely and just let the player pick.

    [MpCompatPrefix(typeof(Building_TMPortal), nameof(Building_TMPortal.GetFloatMenuOptions), 0)]
    private static bool PreStartChoosingDestinationJob(Pawn ___myPawn, object __instance)
    {
        if (!MP.IsInMultiplayer)
            return true;

        var building = portalBuildingInnerClassThisField(__instance);
        // Setup dummy job driver and start choosing destination
        new JobDriver_PortalDestination
        {
            pawn = ___myPawn,
            portalBldg = building,
            job = new Job(null, building),
        }.StartChoosingDestination();

        return false;
    }

    #endregion

    #region Skill level-up handling

    // Handle "+" level-up buttons.

    #region Magic

    [MpCompatSyncMethod]
    private static void SyncedLevelUpMagicSkill(Pawn pawn, AbilityUser.AbilityDef abilityDef, int magicPowerSkillIndex, int currentLevel)
    {
        // Can't use cancelIfAnyArgNull as we expect abilityDef to
        // potentially be null, so include a null pawn check in here.
        if (pawn == null)
            return;

        // Make sure indices aren't negative
        if (magicPowerSkillIndex < 0)
            return;

        var comp = pawn.GetCompAbilityUserMagic();
        // Make sure that the comp and lists aren't null and that they contain the correct data
        if (comp?.MagicData?.AllMagicPowerSkills == null || comp.MagicData.AllMagicPowerSkills.Count <= magicPowerSkillIndex)
            return;

        // If there's one, make sure the parent power is not null and learned
        if (abilityDef != null)
        {
            var power = comp.MagicData.AllMagicPowers.Find(p => p.abilityDef == abilityDef);
            if (power is not { learned: true })
                return;
        }

        var skill = comp.MagicData.AllMagicPowerSkills[magicPowerSkillIndex];
        // Make sure that we aren't accidentally leveling up when unwanted
        if (skill.level != currentLevel)
            return;
        // Make sure we aren't overleveling
        if (skill.level >= skill.levelMax)
            return;
        // Make sure we can level up
        if (comp.MagicData.MagicAbilityPoints < skill.costToLevel)
            return;

        // Level up, remove ability points
        skill.level++;
        comp.MagicData.MagicAbilityPoints -= skill.costToLevel;

        // Special handling for specific abilities
        if (skill.label == "TM_Cantrips_eff")
        {
            if (skill.level >= 15)
                TM_PawnTracker.ResolveMightComp(pawn.GetCompAbilityUserMight());
        }
        else if (skill.label == "TM_LightSkip_pwr")
        {
            if (skill.level == 1)
                comp.AddPawnAbility(TorannMagicDefOf.TM_LightSkipMass);
            else if (skill.level == 2)
                comp.AddPawnAbility(TorannMagicDefOf.TM_LightSkipGlobal);
        }
    }

    private static bool ReplacedLevelUpMagicButton(Rect rect, string label, bool drawBackground, bool doMouseoverSound, bool active, TextAnchor? overrideTextAnchor,
        CompAbilityUserMagic compMagic, MagicPower power, List<MagicPowerSkill>.Enumerator skillEnumerator)
    {
        var result = Widgets.ButtonText(rect, label, drawBackground, doMouseoverSound, active, overrideTextAnchor);
        // If not in MP, the button not pressed, or the faction is not player faction, return as-is.
        if (!MP.IsInMultiplayer || !result || compMagic.Pawn.Faction != Faction.OfPlayer)
            return result;

        var pawn = compMagic.Pawn;

        // The mod itself Doesn't check for genes in the mod itself,
        // it also doesn't check for enum flags and instead only the value.
        if (power != null && pawn.story is { DisabledWorkTagsBackstoryAndTraits: WorkTags.Violent } && power.abilityDef.MainVerb.isViolent)
        {
            // The mod originally has this as a historical message, it's a bit pointless.
            // It also only provides the first argument, but not second one, causing an error.
            Messages.Message("IsIncapableOfViolenceLower".Translate(compMagic.parent.LabelShort, compMagic.parent), MessageTypeDefOf.RejectInput, false);
            return false;
        }

        var skill = skillEnumerator.Current;
        var magicPowerSkillIndex = compMagic.MagicData.AllMagicPowerSkills.IndexOf(skill);
        if (magicPowerSkillIndex < 0)
            return false;

        SyncedLevelUpMagicSkill(pawn, power?.abilityDef, magicPowerSkillIndex, skill!.level);
        return false;
    }

    private static bool ReplacedGlobalLevelUpMagicButton(Rect rect, string label, bool drawBackground, bool doMouseoverSound, bool active, TextAnchor? overrideTextAnchor,
        CompAbilityUserMagic compMagic, List<MagicPowerSkill>.Enumerator clarityEnumerator, List<MagicPowerSkill>.Enumerator focusEnumerator, List<MagicPowerSkill>.Enumerator spiritEnumerator)
    {
        List<MagicPowerSkill>.Enumerator target;
        if (clarityEnumerator.Current != null)
            target = clarityEnumerator;
        else if (focusEnumerator.Current != null)
            target = focusEnumerator;
        else if (spiritEnumerator.Current != null)
            target = spiritEnumerator;
        // Shouldn't happen
        else
            return false;

        return ReplacedLevelUpMagicButton(rect, label, drawBackground, doMouseoverSound, active, overrideTextAnchor, compMagic, null, target);
    }

    #endregion

    #region Might

    [MpCompatSyncMethod(cancelIfAnyArgNull = true)]
    private static void SyncedLevelUpMightSkill(Pawn pawn, int mightPowerSkillIndex, int currentLevel)
    {
        // We don't need to check MightPower, similarly to how magic does with MagicPower,
        // as might doesn't have checks here if the base power is learned at all.
        // Seems like it allows you to upgrade mutually-exclusive skill upgrades for
        // super soldier pawns. However, in general might skills don't need learning,
        // so that's likely why there's no checks for learned powers in here,

        // Make sure indices aren't negative
        if (mightPowerSkillIndex < 0)
            return;

        var comp = pawn.GetCompAbilityUserMight();
        // Make sure that the comp and lists aren't null and that they contain the correct data
        if (comp?.MightData?.AllMightPowerSkills == null || comp.MightData.AllMightPowerSkills.Count <= mightPowerSkillIndex)
            return;

        var skill = comp.MightData.AllMightPowerSkills[mightPowerSkillIndex];
        // Make sure that we aren't accidentally leveling up when unwanted
        if (skill.level != currentLevel)
            return;
        // Make sure we aren't overleveling
        if (skill.level >= skill.levelMax)
            return;
        // Make sure we can level up
        if (comp.MightData.MightAbilityPoints < skill.costToLevel)
            return;

        // Level up, remove ability points
        skill.level++;
        comp.MightData.MightAbilityPoints -= skill.costToLevel;

        // Special handling for specific abilities
        if (skill.label == "TM_FieldTraining_eff" && skill.level >= 15)
            TM_PawnTracker.ResolveMagicComp(pawn.GetCompAbilityUserMagic());
    }

    private static bool ReplacedLevelUpMightButton(Rect rect, string label, bool drawBackground, bool doMouseoverSound, bool active, TextAnchor? overrideTextAnchor, CompAbilityUserMight compMight, MightPower power, List<MightPowerSkill>.Enumerator skillEnumerator)
    {
        var result = Widgets.ButtonText(rect, label, drawBackground, doMouseoverSound, active, overrideTextAnchor);
        // If not in MP, the button not pressed, or the faction is not player faction, return as-is.
        if (!MP.IsInMultiplayer || !result || compMight.Pawn.Faction != Faction.OfPlayer)
            return result;

        var pawn = compMight.Pawn;

        // The mod itself Doesn't check for genes in the mod itself,
        // it also doesn't check for enum flags and instead only the value.
        if (power != null && pawn.story is { DisabledWorkTagsBackstoryAndTraits: WorkTags.Violent } && power.abilityDef.MainVerb.isViolent)
        {
            // The mod originally has this as a historical message, it's a bit pointless.
            // It also only provides the first argument, but not second one, causing an error.
            Messages.Message("IsIncapableOfViolenceLower".Translate(compMight.parent.LabelShort, compMight.parent), MessageTypeDefOf.RejectInput, false);
            return false;
        }

        var skill = skillEnumerator.Current;
        var mightPowerSkillIndex = compMight.MightData.AllMightPowerSkills.IndexOf(skill);
        if (mightPowerSkillIndex < 0)
            return false;

        SyncedLevelUpMightSkill(pawn, mightPowerSkillIndex, skill!.level);
        return false;
    }

    private static bool ReplacedGlobalLevelUpMightButton(Rect rect, string label, bool drawBackground, bool doMouseoverSound, bool active, TextAnchor? overrideTextAnchor,
        CompAbilityUserMight compMight, List<MightPowerSkill>.Enumerator fitnessEnumerator, List<MightPowerSkill>.Enumerator coordinationEnumerator, List<MightPowerSkill>.Enumerator strengthEnumerator, List<MightPowerSkill>.Enumerator enduranceEnumerator)
    {
        List<MightPowerSkill>.Enumerator target;
        if (fitnessEnumerator.Current != null)
            target = fitnessEnumerator;
        else if (coordinationEnumerator.Current != null)
            target = coordinationEnumerator;
        else if (strengthEnumerator.Current != null)
            target = strengthEnumerator;
        else if (enduranceEnumerator.Current != null)
            target = enduranceEnumerator;
        // Shouldn't happen
        else
            return false;

        return ReplacedLevelUpMightButton(rect, label, drawBackground, doMouseoverSound, active, overrideTextAnchor, compMight, null, target);
    }

    #endregion

    #region Shared

    [MpCompatTranspiler(typeof(MagicCardUtility), nameof(MagicCardUtility.CustomSkillHandler))]
    [MpCompatTranspiler(typeof(MightCardUtility), nameof(MightCardUtility.CustomSkillHandler))]
    private static IEnumerable<CodeInstruction> UniversalReplaceLevelUpPlusButton(IEnumerable<CodeInstruction> instr, MethodBase baseMethod)
    {
        var target = AccessTools.DeclaredMethod(typeof(Widgets), nameof(Widgets.ButtonText),
            [typeof(Rect), typeof(string), typeof(bool), typeof(bool), typeof(bool), typeof(TextAnchor?)]);
        MethodInfo replacement;

        if (baseMethod.DeclaringType == typeof(MagicCardUtility))
            replacement = MpMethodUtil.MethodOf(ReplacedLevelUpMagicButton);
        else if (baseMethod.DeclaringType == typeof(MightCardUtility))
            replacement = MpMethodUtil.MethodOf(ReplacedLevelUpMightButton);
        // Shouldn't happen
        else throw new Exception($"Trying to apply transpiler ({nameof(UniversalReplaceLevelUpPlusButton)}) for an unsupported type ({baseMethod.DeclaringType.FullDescription()}).");

        IEnumerable<CodeInstruction> ExtraInstructions() =>
        [
            // Load the magic/might comp parameter
            new CodeInstruction(OpCodes.Ldarg_1),
            // Load the magic/might power parameter
            new CodeInstruction(OpCodes.Ldarg_2),
            // Load the List<Might/MagicPowerSkill>.Enumerator,
            // it's simpler than calling CS$<>8__locals1.skill.
            new CodeInstruction(OpCodes.Ldloc_0),
        ];

        return ReplaceMethod(instr, baseMethod, target, replacement, ExtraInstructions, "+", 1);
    }

    [MpCompatTranspiler(typeof(MagicCardUtility), nameof(MagicCardUtility.DrawLevelBar))]
    [MpCompatTranspiler(typeof(MightCardUtility), nameof(MightCardUtility.DrawLevelBar))]
    private static IEnumerable<CodeInstruction> UniversalReplaceGlobalLevelUpPlusButton(IEnumerable<CodeInstruction> instr, MethodBase baseMethod)
    {
        var target = AccessTools.DeclaredMethod(typeof(Widgets), nameof(Widgets.ButtonText),
            [typeof(Rect), typeof(string), typeof(bool), typeof(bool), typeof(bool), typeof(TextAnchor?)]);
        MethodInfo replacement;
        int expected;
        Func<IEnumerable<CodeInstruction>> extraInstructions;

        if (baseMethod.DeclaringType == typeof(MagicCardUtility))
        {
            replacement = MpMethodUtil.MethodOf(ReplacedGlobalLevelUpMagicButton);
            expected = 3;
            extraInstructions = () =>
            [
                // Load the pawn argument
                new CodeInstruction(OpCodes.Ldarg_1),
                // Load all the List<MagicPowerSkill>.Enumerator locals
                new CodeInstruction(OpCodes.Ldloc_S, 19),
                new CodeInstruction(OpCodes.Ldloc_S, 28),
                new CodeInstruction(OpCodes.Ldloc_S, 36),
            ];
        }
        else if (baseMethod.DeclaringType == typeof(MightCardUtility))
        {
            replacement = MpMethodUtil.MethodOf(ReplacedGlobalLevelUpMightButton);
            expected = 4;
            extraInstructions = () =>
            [
                // Load the pawn argument
                new CodeInstruction(OpCodes.Ldarg_1),
                // Load all the List<MightPowerSkill>.Enumerator locals
                new CodeInstruction(OpCodes.Ldloc_S, 22),
                new CodeInstruction(OpCodes.Ldloc_S, 31),
                new CodeInstruction(OpCodes.Ldloc_S, 39),
                new CodeInstruction(OpCodes.Ldloc_S, 47),
            ];
        }
        // Shouldn't happen
        else throw new Exception($"Trying to apply transpiler ({nameof(UniversalReplaceLevelUpPlusButton)}) for an unsupported type ({baseMethod.DeclaringType.FullDescription()}).");
        
        return ReplaceMethod(instr, baseMethod, target, replacement, extraInstructions, "+", expected);
    }

    #endregion

    #endregion

    #region Power learn/level-up handling

    // Handle the "Learn" (text) and level-up (image) buttons.

    #region Magic

    #region Learning

    [MpCompatSyncMethod(cancelIfAnyArgNull = true)]
    private static void SyncedLearnMagicSkill(Pawn pawn, TMAbilityDef abilityDef)
    {
        var compMagic = pawn.GetCompAbilityUserMagic();
        var power = compMagic?.MagicData?.AllMagicPowers?.Find(p => p.abilityDef == abilityDef);
        // Make sure the power is not null, not learned yet, and (shouldn't happen here) doesn't require a scroll
        if (power == null || power.learned || power.requiresScroll)
            return;
        // Make sure we have enough ability points to level up
        if (compMagic.MagicData.MagicAbilityPoints < power.costToLevel)
            return;

        // No max level checks, etc. as some skills that can only
        // be learned (but not leveled up) have a max level of 0.

        // Execute everything in the same order as in the mod
        power.learned = true;

        if (abilityDef.shouldInitialize && abilityDef.defName != "TM_TechnoBit")
            compMagic.AddPawnAbility(abilityDef);
        if (abilityDef.defName == "TM_TechnoWeapon")
        {
            compMagic.AddPawnAbility(TorannMagicDefOf.TM_NanoStimulant);
            compMagic.MagicData.MagicPowersStandalone.FirstOrDefault(
                p => p.abilityDef == TorannMagicDefOf.TM_NanoStimulant).learned = true;
        }
        if (abilityDef.childAbilities is { Count: > 0 })
        {
            // The mod uses a for loop. A foreach loop should be safe,
            // but let's not risk it in case the mod does some weird stuff.
            for (var i = 0; i < abilityDef.childAbilities.Count; i++)
            {
                var childAbilityDef = abilityDef.childAbilities[i];
                if (childAbilityDef.shouldInitialize)
                    compMagic.AddPawnAbility(childAbilityDef);
            }
        }

        compMagic.MagicData.MagicAbilityPoints -= power.learnCost;
    }

    private static bool ReplacedLearnMagicSkillButton(Rect rect, string label, bool drawBackground, bool doMouseoverSound, bool active, TextAnchor? overrideTextAnchor,
        CompAbilityUserMagic compMagic, List<MagicPower>.Enumerator powerEnumerator)
    {
        var result = Widgets.ButtonText(rect, label, drawBackground, doMouseoverSound, active, overrideTextAnchor);
        // If not in MP, the button not pressed, or the faction is not player faction, return as-is.
        if (!MP.IsInMultiplayer || !result || compMagic.Pawn.Faction != Faction.OfPlayer)
            return result;

        var power = powerEnumerator.Current;
        if (power != null)
            SyncedLearnMagicSkill(compMagic.Pawn, (TMAbilityDef)power.abilityDef);

        return false;
    }

    #endregion

    #region Level-up

    [MpCompatSyncMethod(cancelIfAnyArgNull = true)]
    private static void SyncedLevelUpMagicPower(Pawn pawn, AbilityUser.AbilityDef abilityDef, int currentLevel)
    {
        var comp = pawn.GetCompAbilityUserMagic();
        // Make sure that the comp and lists aren't null and that they contain the correct data
        if (comp?.MagicData?.AllMagicPowerSkills == null)
            return;

        var power = comp.MagicData.AllMagicPowers.Find(p => p.abilityDef == abilityDef);
        if (power is not { learned: true })
            return;

        // Make sure that we aren't accidentally leveling up when unwanted
        if (power.level != currentLevel)
            return;
        // Make sure we aren't overleveling
        if (power.level >= power.maxLevel)
            return;
        // Make sure we can level up
        if (comp.MagicData.MagicAbilityPoints < power.costToLevel)
            return;

        comp.LevelUpPower(power);
        comp.MagicData.MagicAbilityPoints -= power.costToLevel;
    }

    private static bool ReplacedLevelUpMagicPowerButton(Rect butRect, Texture2D tex, bool doMouseoverSound, string tooltip,
        CompAbilityUserMagic compMagic, List<MagicPower>.Enumerator powerEnumerator)
    {
        var result = Widgets.ButtonImage(butRect, tex, doMouseoverSound, tooltip);
        // If not in MP, the button not pressed, or the faction is not player faction, return as-is.
        if (!MP.IsInMultiplayer || !result || compMagic.Pawn.Faction != Faction.OfPlayer)
            return result;

        var power = powerEnumerator.Current;
        if (power != null)
            SyncedLevelUpMagicPower(compMagic.Pawn, power.abilityDef, power.level);

        return false;
    }

    #endregion

    #endregion

    #region Might

    #region Learning

    [MpCompatSyncMethod(cancelIfAnyArgNull = true)]
    private static void SyncedLearnMightSkill(Pawn pawn, TMAbilityDef abilityDef)
    {
        var compMight = pawn.GetCompAbilityUserMight();
        var power = compMight?.MightData?.AllMightPowers?.Find(p => p.abilityDef == abilityDef);
        // Make sure the power is not null and not learned yet
        if (power == null || power.learned)
            return;

        // No max level checks, etc. as some skills that can only
        // be learned (but not leveled up) have a max level of 0.
        // Don't bother with cost checks. The mod technically
        // does a cost check, but never reduces the ability points.

        power.learned = true;

        if (abilityDef == TorannMagicDefOf.TM_PistolSpec)
        {
            compMight.AddPawnAbility(TorannMagicDefOf.TM_PistolWhip);
            compMight.skill_PistolWhip = true;
        }
        else if (abilityDef == TorannMagicDefOf.TM_RifleSpec)
        {
            compMight.AddPawnAbility(TorannMagicDefOf.TM_SuppressingFire);
            compMight.skill_SuppressingFire = true;
            compMight.AddPawnAbility(TorannMagicDefOf.TM_Mk203GL);
            compMight.skill_Mk203GL = true;
        }
        else if (abilityDef == TorannMagicDefOf.TM_ShotgunSpec)
        {
            compMight.AddPawnAbility(TorannMagicDefOf.TM_Buckshot);
            compMight.skill_Buckshot = true;
            compMight.AddPawnAbility(TorannMagicDefOf.TM_BreachingCharge);
            compMight.skill_BreachingCharge = true;
        }
    }

    private static bool ReplacedLearnMightSkillButton(Rect rect, string label, bool drawBackground, bool doMouseoverSound, bool active, TextAnchor? overrideTextAnchor,
        CompAbilityUserMight compMight, List<MightPower>.Enumerator powerEnumerator)
    {
        var result = Widgets.ButtonText(rect, label, drawBackground, doMouseoverSound, active, overrideTextAnchor);
        // If not in MP, the button not pressed, or the faction is not player faction, return as-is.
        if (!MP.IsInMultiplayer || !result || compMight.Pawn.Faction != Faction.OfPlayer)
            return result;

        var power = powerEnumerator.Current;
        if (power != null)
            SyncedLearnMightSkill(compMight.Pawn, (TMAbilityDef)power.abilityDef);

        return false;
    }

    #endregion

    #region Level-up

    [MpCompatSyncMethod(cancelIfAnyArgNull = true)]
    private static void SyncedLevelUpMightPower(Pawn pawn, AbilityUser.AbilityDef abilityDef, int currentLevel)
    {
        var comp = pawn.GetCompAbilityUserMight();
        // Make sure that the comp and lists aren't null and that they contain the correct data
        if (comp?.MightData?.AllMightPowerSkills == null)
            return;

        var power = comp.MightData.AllMightPowers.Find(p => p.abilityDef == abilityDef);
        if (power is not { learned: true })
            return;

        // Make sure that we aren't accidentally leveling up when unwanted
        if (power.level != currentLevel)
            return;
        // Make sure we aren't overleveling
        if (power.level >= power.maxLevel)
            return;
        // Make sure we can level up
        if (comp.MightData.MightAbilityPoints < power.costToLevel)
            return;

        comp.LevelUpPower(power);
        comp.MightData.MightAbilityPoints -= power.costToLevel;
    }

    private static bool ReplacedLevelUpMightPowerButton(Rect butRect, Texture2D tex, bool doMouseoverSound, string tooltip,
        CompAbilityUserMight compMight, List<MightPower>.Enumerator powerEnumerator)
    {
        var result = Widgets.ButtonImage(butRect, tex, doMouseoverSound, tooltip);
        // If not in MP, the button not pressed, or the faction is not player faction, return as-is.
        if (!MP.IsInMultiplayer || !result || compMight.Pawn.Faction != Faction.OfPlayer)
            return result;

        var power = powerEnumerator.Current;
        if (power != null)
            SyncedLevelUpMightPower(compMight.Pawn, power.abilityDef, power.level);

        return false;
    }

    #endregion

    #endregion

    #region Shared

    [MpCompatTranspiler(typeof(MagicCardUtility), nameof(MagicCardUtility.CustomPowersHandler))]
    [MpCompatTranspiler(typeof(MightCardUtility), nameof(MightCardUtility.CustomPowersHandler))]
    private static IEnumerable<CodeInstruction> ReplaceLearnSkillButton(IEnumerable<CodeInstruction> instr, MethodBase baseMethod)
    {
        var targetTextButton = AccessTools.DeclaredMethod(typeof(Widgets), nameof(Widgets.ButtonText),
            [typeof(Rect), typeof(string), typeof(bool), typeof(bool), typeof(bool), typeof(TextAnchor?)]);
        var targetImageButton = AccessTools.DeclaredMethod(typeof(Widgets), nameof(Widgets.ButtonImage),
            [typeof(Rect), typeof(Texture2D), typeof(bool), typeof(string)]);
        MethodInfo textButtonReplacement;
        MethodInfo imageButtonReplacement;
        int enumeratorIndex;

        if (baseMethod.DeclaringType == typeof(MagicCardUtility))
        {
            textButtonReplacement = MpMethodUtil.MethodOf(ReplacedLearnMagicSkillButton);
            imageButtonReplacement = MpMethodUtil.MethodOf(ReplacedLevelUpMagicPowerButton);
            enumeratorIndex = 4;
        }
        else if (baseMethod.DeclaringType == typeof(MightCardUtility))
        {
            textButtonReplacement = MpMethodUtil.MethodOf(ReplacedLearnMightSkillButton);
            imageButtonReplacement = MpMethodUtil.MethodOf(ReplacedLevelUpMightPowerButton);
            enumeratorIndex = 5;
        }
        // Shouldn't happen
        else throw new Exception($"Trying to apply transpiler ({nameof(ReplaceLearnSkillButton)}) for an unsupported type ({baseMethod.DeclaringType.FullDescription()}).");

        IEnumerable<CodeInstruction> ExtraInstructions() =>
        [
            // Load the magic/might comp parameter
            new CodeInstruction(OpCodes.Ldarg_1),
            // Load the List<Might/MagicPowerSkill>.Enumerator,
            // it's simpler than calling CS$<>8__locals1.power.
            new CodeInstruction(OpCodes.Ldloc_S, enumeratorIndex),
        ];

        // Replace the "TM_Learn" button to learn a power
        var replacedLearnButton = ReplaceMethod(instr, baseMethod, targetTextButton, textButtonReplacement, ExtraInstructions, "TM_Learn", 1, "TM_MCU_PointsToLearn");
        // Replace the image button to level-up a power
        return ReplaceMethod(replacedLearnButton, baseMethod, targetImageButton, imageButtonReplacement, ExtraInstructions, null, 1);
    }

    #endregion

    #endregion

    #region Sprite area fixes

    // Two possible approaches here.
    // First - replace call to TM_Calc.GetSpriteArea with our method with
    // another argument, and insert self (or some other argument), and
    // call it with the correct map ourselves.
    // Second (done here) - replace the ldnull used for the map argument
    // and insert the correct map (without replacing the method).

    [MpCompatTranspiler(typeof(Verb_EarthSprites), nameof(Verb_EarthSprites.TryCastShot))]
    private static IEnumerable<CodeInstruction> SpriteAreaInsertMapToVerbEarthSprites(IEnumerable<CodeInstruction> instr, MethodBase baseMethod)
    {
        var casterPawnGetter = AccessTools.PropertyGetter(typeof(Verb_EarthSprites), nameof(Verb_EarthSprites.CasterPawn));
        var mapGetter = AccessTools.PropertyGetter(typeof(Pawn), nameof(Pawn.Map));

        IEnumerable<CodeInstruction> ExtraInstructions(CodeInstruction ci)
        {
            // Replace the null with "this.CasterPawn.Map".

            // Replace the null with "this" (Verb_EarthSprites)
            ci.opcode = OpCodes.Ldarg_0;
            // insert "CasterPawn" getter
            yield return new CodeInstruction(OpCodes.Callvirt, casterPawnGetter);
            // insert "Map" getter
            yield return new CodeInstruction(OpCodes.Callvirt, mapGetter);
        }

        return PatchSpriteAreaMethod(instr, ExtraInstructions, baseMethod, 2);
    }

    [MpCompatTranspiler(typeof(CompAbilityUserMagic), nameof(CompAbilityUserMagic.ResolveEarthSpriteAction))]
    private static IEnumerable<CodeInstruction> SpriteAreaInsertMapToCompAbilityUserMagic(IEnumerable<CodeInstruction> instr, MethodBase baseMethod)
    {
        var earthSpriteMapField = AccessTools.DeclaredField(typeof(CompAbilityUserMagic), nameof(CompAbilityUserMagic.earthSpriteMap));

        IEnumerable<CodeInstruction> ExtraInstructions(CodeInstruction ci)
        {
            // Replace the null with "this.earthSpriteMap".
            // The sprites have their map remembered, and
            // it may be different from the pawn's map.
            // This field is used for basically everything,
            // besides getting the area for the sprites.

            // Replace the null with "this" (CompAbilityUserMagic)
            ci.opcode = OpCodes.Ldarg_0;
            // Insert "earthSpriteMap" field
            yield return new CodeInstruction(OpCodes.Ldfld, earthSpriteMapField);
        }

        return PatchSpriteAreaMethod(instr, ExtraInstructions, baseMethod, 4);
    }

    private static IEnumerable<CodeInstruction> PatchSpriteAreaMethod(IEnumerable<CodeInstruction> instr, Func<CodeInstruction, IEnumerable<CodeInstruction>> method, MethodBase baseMethod, int expectedPatches)
    {
        var target = MpMethodUtil.MethodOf(TM_Calc.GetSpriteArea);
        var patchedCount = 0;
        var instrArr = instr.ToArray();

        for (var i = 0; i < instrArr.Length; i++)
        {
            var ci = instrArr[i];

            yield return ci;

            if (ci.opcode == OpCodes.Ldnull && i + 2 < instrArr.Length && instrArr[i + 2].Calls(target))
            {
                foreach (var newInstr in method(ci))
                    yield return newInstr;

                patchedCount++;
            }
        }

        if (patchedCount != expectedPatches)
        {
            var name = (baseMethod.DeclaringType?.Namespace).NullOrEmpty() ? baseMethod.Name : $"{baseMethod.DeclaringType!.Name}:{baseMethod.Name}";
            Log.Warning($"Patched incorrect number of TM_Calc.GetSpriteArea calls (patched {patchedCount}, expected {expectedPatches}) for method {name}");
        }
    }

    #endregion

    #region Golem ITab field watching

    [MpCompatPrefix(typeof(ITab_GolemPawn), nameof(ITab_GolemPawn.FillTab))]
    [MpCompatPrefix(typeof(ITab_GolemWorkstation), nameof(ITab_GolemWorkstation.FillTab))]
    private static void PreITabGolemFillTab(out bool __state)
    {
        if (!MP.IsInMultiplayer)
        {
            __state = false;
            return;
        }

        var selected = Find.Selector.SingleSelectedThing;
        // ITab_GolemPawn uses TMPawnGolem, ITab_GolemWorkstation uses Building_TMGolemBase
        var golem = selected as TMPawnGolem ?? (selected as Building_TMGolemBase)?.GolemPawn;
        if (golem == null)
        {
            __state = false;
            return;
        }

        MP.WatchBegin();
        __state = true;

        // TMPawnGolem
        compGolemShowDormantPosition.Watch(golem);

        // CompGolem
        compGolemFollowsMaster.Watch(golem.Golem);
        compGolemFollowsMasterDrafted.Watch(golem.Golem);
        compGolemCheckThreatPath.Watch(golem.Golem);
        compGolemRemainDormantWhenUpgrading.Watch(golem.Golem);
        compGolemUseAbilitiesWhenDormant.Watch(golem.Golem);
        compGolemThreatRange.Watch(golem.Golem);
        compGolemMinEnergyPctForAbilities.Watch(golem.Golem);
        compGolemEnergyPctShouldRest.Watch(golem.Golem);
        compGolemEnergyPctShouldAwaken.Watch(golem.Golem);
    }

    [MpCompatFinalizer(typeof(ITab_GolemPawn), nameof(ITab_GolemPawn.FillTab))]
    [MpCompatFinalizer(typeof(ITab_GolemWorkstation), nameof(ITab_GolemWorkstation.FillTab))]
    private static void PostITabGolemFillTab(bool __state)
    {
        if (__state)
            MP.WatchEnd();
    }

    #endregion

    #region Golem abilities and work types changing

    [MpCompatSyncMethod(cancelIfAnyArgNull = true)]
    private static void SyncedApplyChangesToGolemAbilitiesAndWorkTypes(CompGolem cg, List<TM_GolemUpgrade> upgrades, List<TM_GolemDef.GolemWorkTypes> workTypes)
        => new GolemAbilitiesWindow
        {
            cg = cg,
            upgrades = upgrades,
            workTypes = workTypes
        }.Close();

    [MpCompatPrefix(typeof(GolemAbilitiesWindow), nameof(GolemAbilitiesWindow.Close))]
    private static bool PreCloseDialog(GolemAbilitiesWindow __instance, bool doCloseSound)
    {
        // If not in MP or not in interface (can't sync, would end up in endless loop)
        if (!MP.IsInMultiplayer || !MP.InInterface)
            return true;

        // Sync the "Close" method.
        SyncedApplyChangesToGolemAbilitiesAndWorkTypes(__instance.cg, __instance.upgrades, __instance.workTypes);

        // Close the dialog manually, since we canceled the close method.
        Find.WindowStack.TryRemove(__instance, doCloseSound);
        // We cannot let the close method run, as it would change game state in interface.
        return false;
    }

    #endregion

    #region Golem renaming

    // The golem renaming dialog sets CompGolem.GolemName and Pawn.Name at the same time.
    // The issue happens due to Pawn.Name using CompGolem.GolemName, which isn't synced yet

    [MpCompatSyncMethod(cancelIfAnyArgNull = true)]
    private static void SyncedSetGolemName(CompGolem golem, string targetName)
        => golem.PawnGolem.Name = golem.GolemName = NameTriple.FromString(targetName);

    private static bool ReplacedApplyGolemNameButton(Rect rect, string label, bool drawBackground, bool doMouseoverSound, bool active, TextAnchor? overrideTextAnchor, GolemNameWindow window)
    {
        var result = Widgets.ButtonText(rect, label, drawBackground, doMouseoverSound, active, overrideTextAnchor);
        if (!MP.IsInMultiplayer || !result)
            return result;

        SyncedSetGolemName(window.cg, window.golemName);
        return false;
    }

    [MpCompatTranspiler(typeof(GolemNameWindow), nameof(GolemNameWindow.DoWindowContents))]
    private static IEnumerable<CodeInstruction> ReplaceApplyGolemNameButtonTranspiler(IEnumerable<CodeInstruction> instr, MethodBase baseMethod)
    {
        var target = AccessTools.DeclaredMethod(typeof(Widgets), nameof(Widgets.ButtonText),
            [typeof(Rect), typeof(string), typeof(bool), typeof(bool), typeof(bool), typeof(TextAnchor?)]);
        var replacement = MpMethodUtil.MethodOf(ReplacedApplyGolemNameButton);

        IEnumerable<CodeInstruction> ExtraInstructions() =>
        [
            // Load in "this" (GolemNameWindow)
            new CodeInstruction(OpCodes.Ldarg_0),
        ];

        // The "Apply" text isn't translated in the mod...
        return ReplaceMethod(instr, baseMethod, target, replacement, ExtraInstructions, "Apply", 1);
    }

    #endregion

    #region Shared

    private static IEnumerable<CodeInstruction> ReplaceMethod(IEnumerable<CodeInstruction> instr, MethodBase baseMethod, MethodInfo target, MethodInfo replacement, Func<IEnumerable<CodeInstruction>> extraInstructions = null, string buttonText = null, int expectedReplacements = -1, string excludedText = null)
    {
        // Check for text only if expected text isn't null
        var isCorrectText = buttonText == null;
        var skipNextCall = false;
        var replacedCount = 0;

        foreach (var ci in instr)
        {
            if (ci.opcode == OpCodes.Ldstr && ci.operand is string s)
            {
                // Excluded text (if not null) will cancel replacement of the next occurrence
                // of the method. Used by `MagicCardUtility:CustomPowersHandler`, as the text
                // `TM_Learn` appears twice there, but in a single case it's combined with
                // `TM_MCU_PointsToLearn`, in which case we ignore the button (as the
                // button does nothing in that particular case).
                if (excludedText != null && s == excludedText)
                    skipNextCall = true;
                else if (s == buttonText)
                    isCorrectText = true;
            }
            else if (isCorrectText)
            {
                if (ci.Calls(target))
                {
                    if (skipNextCall)
                    {
                        skipNextCall = false;
                    }
                    else
                    {
                        if (extraInstructions != null)
                        {
                            foreach (var extraInstr in extraInstructions())
                                yield return extraInstr;
                        }

                        // Replace method with our own
                        ci.opcode = OpCodes.Call;
                        ci.operand = replacement;

                        replacedCount++;
                        // Check for text only if expected text isn't null
                        isCorrectText = buttonText == null;
                    }
                }
            }

            yield return ci;
        }

        string MethodName() => (baseMethod.DeclaringType?.Namespace).NullOrEmpty() ? baseMethod.Name : $"{baseMethod.DeclaringType!.Name}:{baseMethod.Name}";
        if (replacedCount != expectedReplacements && expectedReplacements >= 0)
            Log.Warning($"Patched incorrect number of {target.DeclaringType?.Name ?? "null"}.{target.Name} calls (patched {replacedCount}, expected {expectedReplacements}) for method {MethodName()}");
        // Special case (-2) - expected some patched methods, but amount unspecified
        else if (replacedCount == 0 && expectedReplacements == -2)
            Log.Warning($"No calls of {target.DeclaringType?.Name ?? "null"}.{target.Name} were patched for method {MethodName()}");
    }

    #endregion

    #region Optimizations

    [MpCompatPrefix(typeof(TM_Calc), nameof(TM_Calc.FindConnectedWalls))]
    private static bool FasterFindConnectedWalls(Building start, float maxAllowedDistance, float maxDistanceFromStart, ref List<Building> __result)
    {
        // Tested original and 2 optimized versions
        // by running all of them 1000 times.
        // Original:                74945.925 ms
        // Optimized:               179.0462 ms
        // Optimized with HashSets: 165.098 ms

        var map = start.Map;
        var connectedBuildings = new HashSet<Building> { start };
        var newBuildings = new HashSet<Building> { start };
        var addedBuilding = new HashSet<Building>();

        // We avoid call to LengthHorizontal and instead use LengthHorizontalSquared.
        // because of this we also need a squared value for maxDistanceFromStart and
        // maxAllowedDistance. After all, multiplying (especially only once) is going
        // to be faster than squaring a number (especially if done multiple times).
        var maxDistanceSquared = maxDistanceFromStart * maxDistanceFromStart;
        var maxAllowedDistanceSquared = maxAllowedDistance * maxAllowedDistance;

        // Rather than using ListerThings to search for buildings, use ListerBuildings.
        // Since there's no global "all buildings", concat the colonist and non-colonist
        // buildings as that should cover everything. We also don't need to check if
        // the current thing is a Building, since we only work on buildings. Likewise,
        // we don't need to cast it all to Building at the end either. Finally, We call
        // ToList at the end to avoid multiple enumerations on the list, which the mod does.
        var allBuildings = map.listerBuildings.allBuildingsColonist.Concat(map.listerBuildings.allBuildingsNonColonist)
            .Where(b => TM_Calc.IsWall(b) && (b.Position - start.Position).LengthHorizontalSquared <= maxDistanceSquared)
            .ToList();

        for (var i = 0; i < 200; i++)
        {
            addedBuilding.Clear();
            foreach (var b in newBuildings)
            {
                foreach (var t in allBuildings)
                {
                    if ((t.Position - b.Position).LengthHorizontalSquared <= maxAllowedDistanceSquared && connectedBuildings.Add(t))
                    {
                        addedBuilding.Add(t);
                    }
                }
            }

            newBuildings.Clear();
            newBuildings.AddRange(addedBuilding);
            if(newBuildings.Count <= 0)
                break;
        }

        // Needs to be ordered to be deterministic
        __result = connectedBuildings.OrderBy(x => x.thingIDNumber).ToList();

        return false;
    }

    [MpCompatPrefix(typeof(FlyingObject_LivingWall), nameof(FlyingObject_LivingWall.FindClosestWallFromTarget))]
    private static bool FasterFindClosestWallFromTarget(FlyingObject_LivingWall __instance, ref Thing __result)
    {
        // Tested original and optimized by running both 1000 times:
        // Original:  1099.7684 ms
        // Optimized: 39.382 ms
        
        Thing tmp = null;
        // The mod uses 999, but since we operate on squared
        // values we need to square the 999 as well.
        var closest = 998001f;
    
        // No need to call ToList, as it's iterated over only once.
        var allThings = __instance.Map.listerBuildings.allBuildingsColonist.Concat(__instance.Map.listerBuildings.allBuildingsNonColonist)
            .Where(TM_Calc.IsWall);
    
        foreach(var b in allThings)
        {
            var dist = (b.Position - FlyingObject_LivingWall.closestThreat.Position).LengthHorizontalSquared;
            if (dist < closest)
            {
                closest = dist;
                tmp = b;
            }
        }
    
        __result = tmp;
    
        return false;
    }

    [MpCompatPrefix(typeof(TM_Calc), nameof(TM_Calc.FindNearestWall))]
    private static bool FasterFindNearestWall(Map map, IntVec3 center, Faction faction, ref Building __result)
    {
        // Tested original and optimized by running both 1000 times:
        // Original:  395.6876 ms
        // Optimized: 2.7747 ms

        foreach(var b in map.listerBuildings.allBuildingsColonist.Concat(map.listerBuildings.allBuildingsNonColonist))
        {
            if(TM_Calc.IsWall(b) && (b.Position - center).LengthHorizontalSquared <= 1.9599999F)
            {
                if(faction != null)
                {
                    if (faction == b.Faction)
                    {
                        __result = b;
                        return false;
                    }
                }
                else
                {
                    __result = b;
                    return false;
                }
            }
        }
    
        __result = null;
        return false;
    }

    /*
    // Seems to have issues, would need to test more to see what's safe and what's not.
    // Leaving as-is for now. Probably won't have enough impact on things anyway.

    // [MpCompatPrefix(typeof(FlyingObject_LivingWall), nameof(FlyingObject_LivingWall.FindClosestWallToTarget))]
    [MpCompatPrefix(typeof(FlyingObject_LivingWall), nameof(FlyingObject_LivingWall.DirectPath))]
    // Ignore FlyingObject_LivingWall.FindClosestWallFromTarget, as we replace the method with a faster one.
    // Ignore FlyingObject_LivingWall.FindClosestThreat, as the search range of 999 would not be squared.
    [MpCompatPrefix(typeof(FlyingObject_PsiStorm), nameof(FlyingObject_PsiStorm.DrawBoltMeshes))]
    [MpCompatPrefix(typeof(JobGiver_AIClean), nameof(JobGiver_AIClean.TryGiveJob))]
    [MpCompatPrefix(typeof(TM_Calc), nameof(TM_Calc.FindClosestCellPlus1VisibleToTarget))]
    [MpCompatPrefix(typeof(TM_Calc), nameof(TM_Calc.SnipPath))]
    // Results for this one are ignored... Changing it won't have any effect whatsoever on gameplay.
    [MpCompatPrefix(typeof(AoECombat), nameof(AoECombat.Evaluate))]
    // We could patch more code, especially a lot of autocasting code, but that would require
    // changing extra code. Too much work for too little gain. Patch for this is included specifically
    // due to living wall having some performance issues already (if not multithreaded).
    private static IEnumerable<CodeInstruction> ReplaceLengthHorizontalCalls(IEnumerable<CodeInstruction> instr, MethodBase baseMethod)
    {
        // Whenever it won't affect the code, replace the usage of
        // IntVec3's LengthHorizontal with LengthHorizontalSquared,
        // as both call the same code but non-squared one calls
        // GenMath.Sqrt (Math.Sqrt), which tends to be quite slow.
        // Especially useful when trying to determine the closest
        // target - it won't change which one is closest or not,
        // only that the numbers we work on are bigger (weren't
        // squared). Same applies when comparing which of the 2
        // values is bigger, it will be the same both before and
        // after squaring the numbers.
        var target = AccessTools.DeclaredPropertyGetter(typeof(IntVec3), nameof(IntVec3.LengthHorizontal));
        var replacement = AccessTools.DeclaredPropertyGetter(typeof(IntVec3), nameof(IntVec3.LengthHorizontalSquared));

        return ReplaceMethod(instr, baseMethod, target, replacement, expectedReplacements: -2);
    }
    */

    #endregion
}