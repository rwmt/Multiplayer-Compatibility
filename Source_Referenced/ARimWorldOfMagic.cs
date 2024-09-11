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

        // TODO:
        // Might/MagicCardUtility.DrawMight/MagicCard - a lot of attempts in the UI to add/remove specific abilities
        // Golems
        // TorannMagic.TM_Calc:GetSpriteArea - uses CurrentMap rather than the correct one
        // Maybe TorannMagic.ModOptions.TM_DebugTools:SpawnSpirit
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

    #region Multithreading

    // It'll break the thingy from working properly, but for now let's keep it off to prevent desyncs
    [MpCompatPrefix(typeof(FlyingObject_LivingWall), nameof(FlyingObject_LivingWall.DoThreadedActions))]
    private static bool CancelCall() => !MP.IsInMultiplayer;

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

        var index = pawn.GetCompAbilityUserMagic().MagicData.AllMagicPowers.IndexOf(magicPower);
        if (index >= 0)
            SyncedSetMagicAutoCast(pawn, index, target);
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

        var index = pawn.GetCompAbilityUserMight().MightData.AllMightPowers.IndexOf(mightPower);
        if (index >= 0)
            SyncedSetMightAutoCast(pawn, index, target);
    }

    [MpCompatSyncMethod(cancelIfAnyArgNull = true)]
    private static void SyncedSetMagicAutoCast(Pawn pawn, int powerIndex, bool target)
    {
        if (powerIndex < 0)
            return;
        var powers = pawn.GetCompAbilityUserMagic()?.MagicData?.AllMagicPowers;
        if (powers == null || powers.Count <= powerIndex)
            return;

        var power = powers[powerIndex];
        // Reset the interaction tick, otherwise the setter may not change the value as it was "interacted" with too recently
        power.interactionTick = 0;
        power.AutoCast = target;
    }

    [MpCompatSyncMethod(cancelIfAnyArgNull = true)]
    private static void SyncedSetMightAutoCast(Pawn pawn, int powerIndex, bool target)
    {
        if (powerIndex < 0)
            return;
        var powers = pawn.GetCompAbilityUserMight()?.MightData?.AllMightPowers;
        if (powers == null || powers.Count <= powerIndex)
            return;

        var power = powers[powerIndex];
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
        var replacedLearnButton = ReplaceMethod(instr, baseMethod, targetTextButton, textButtonReplacement, ExtraInstructions, "TM_Learn", 1);
        // Replace the image button to level-up a power
        return ReplaceMethod(replacedLearnButton, baseMethod, targetImageButton, imageButtonReplacement, ExtraInstructions, null, 1);
    }

    #endregion

    #endregion

    #region Shared

    private static IEnumerable<CodeInstruction> ReplaceMethod(IEnumerable<CodeInstruction> instr, MethodBase baseMethod, MethodInfo target, MethodInfo replacement, Func<IEnumerable<CodeInstruction>> extraInstructions, string buttonText, int expectedReplacements)
    {
        // Check for text only if expected text isn't null
        var isCorrectText = buttonText == null;
        var replacedCount = 0;

        foreach (var ci in instr)
        {
            if (isCorrectText)
            {
                if (ci.Calls(target))
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
            else if (ci.opcode == OpCodes.Ldstr && ci.operand is string s && s == buttonText)
                isCorrectText = true;

            yield return ci;
        }

        if (replacedCount != expectedReplacements)
        {
            var name = (baseMethod.DeclaringType?.Namespace).NullOrEmpty() ? baseMethod.Name : $"{baseMethod.DeclaringType!.Name}:{baseMethod.Name}";
            Log.Warning($"Patched incorrect number of {target.DeclaringType?.Name ?? "null"}.{target.Name} calls (patched {replacedCount}, expected {expectedReplacements}) for method {name}");
        }
    }

    #endregion
}