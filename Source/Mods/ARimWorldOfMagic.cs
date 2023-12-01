using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Multiplayer.API;
using UnityEngine;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>A RimWorld of Magic by Torann</summary>
    /// <see href="https://github.com/TorannD/RWoM"/>
    /// <see href="https://steamcommunity.com/workshop/filedetails/?id=1201382956"/>
    [MpCompatFor("Torann.ARimworldOfMagic")]
    public class ARimWorldOfMagic
    {
        //// Magic ////
        // MagicPower
        private static FastInvokeHandler magicPowerAutoCastSetter;
        private static AccessTools.FieldRef<object, int> magicPowerInteractionTickField;

        // MagicData
        private static FastInvokeHandler magicDataAllMagicPowersGetter;

        // CompAbilityUserMagic
        private static FastInvokeHandler compMagicUserMagicDataGetter;

        //// Might ////
        // MightPower
        private static FastInvokeHandler mightPowerAutoCastSetter;
        private static AccessTools.FieldRef<object, int> mightPowerInteractionTickField;

        // MightData
        private static FastInvokeHandler mightDataAllMightPowersGetter;

        // CompAbilityUserMight
        private static FastInvokeHandler compMightUserMightDataGetter;

        //// Shared ////
        // TM_Calc
        private static FastInvokeHandler getMagicUserCompMethod;
        private static FastInvokeHandler getMightUserCompMethod;

        public ARimWorldOfMagic(ModContentPack mod)
        {
            LongEventHandler.ExecuteWhenFinished(LatePatch);

            #region RNG Patching

            // RNG
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
                };

                PatchingUtilities.PatchSystemRand(systemRngMethods, false);

                var genViewMoteMethods = new[]
                {
                    "ThrowGenericMote",
                    "ThrowGenericFleck",
                    "ThrowManaPuff",
                    "ThrowBarrierMote",
                    "ThrowNoteMote",
                    "ThrowTextMote",
                    "ThrowDeceptionMaskMote",
                    "ThrowPossessMote",
                    "ThrowExclamationMote",
                    "ThrowSparkFlashMote",
                    "ThrowEnchantingMote",
                    "ThrowCastingMote",
                    "ThrowCastingMote_Anti",
                    "ThrowCastingMote_Spirit",
                    "ThrowSiphonMote",
                    "ThrowBoltMote",
                    "ThrowPoisonMote",
                    "ThrowShadowCleaveMote",
                    "ThrowArcaneWaveMote",
                    "ThrowRegenMote",
                    "ThrowCrossStrike",
                    "ThrowBloodSquirt",
                    "ThrowFlames",
                    "ThrowMultiStrike",
                    "ThrowScreamMote",
                    "ThrowArcaneDaggers",
                };

                var type = AccessTools.TypeByName("TorannMagic.TM_MoteMaker");
                foreach (var methodName in genViewMoteMethods)
                    PatchingUtilities.PatchPushPopRand(AccessTools.DeclaredMethod(type, methodName));
                // Ambiguous match, need to specify argument types
                PatchingUtilities.PatchPushPopRand(AccessTools.DeclaredMethod(type, "ThrowDiseaseMote",
                    new[] { typeof(Vector3), typeof(Map), typeof(float), typeof(float), typeof(float), typeof(float), }));
                PatchingUtilities.PatchPushPopRand(AccessTools.DeclaredMethod(type, "ThrowArcaneMote",
                    new[] { typeof(Vector3), typeof(Map), typeof(float), typeof(float), typeof(float), typeof(float), typeof(int), typeof(float), }));
                PatchingUtilities.PatchPushPopRand(AccessTools.DeclaredMethod(type, "ThrowShadowMote",
                    new[] { typeof(Vector3), typeof(Map), typeof(float), typeof(int), typeof(float), typeof(float), }));
                PatchingUtilities.PatchPushPopRand(AccessTools.DeclaredMethod(type, "ThrowTwinkle",
                    new[] { typeof(Vector3), typeof(Map), typeof(float), }));
                PatchingUtilities.PatchPushPopRand(AccessTools.DeclaredMethod(type, "ThrowTwinkle",
                    new[] { typeof(Vector3), typeof(Map), typeof(float), typeof(float), typeof(float), typeof(float), typeof(float), typeof(float), }));
            }

            #endregion

            #region Gizmos

            // Gizmos
            {
                //// Magic ////
                // MagicPower
                var magicPowerType = AccessTools.TypeByName("TorannMagic.MagicPower");
                magicPowerAutoCastSetter = MethodInvoker.GetHandler(AccessTools.PropertySetter(magicPowerType, "AutoCast"));
                magicPowerInteractionTickField = AccessTools.FieldRefAccess<int>(magicPowerType, "interactionTick");
                // MagicData
                magicDataAllMagicPowersGetter = MethodInvoker.GetHandler(AccessTools.PropertyGetter("TorannMagic.MagicData:AllMagicPowers"));
                // CompAbilityUserMagic
                compMagicUserMagicDataGetter = MethodInvoker.GetHandler(AccessTools.PropertyGetter("TorannMagic.CompAbilityUserMagic:MagicData"));

                //// Might ////
                // MightPower
                var mightPowerType = AccessTools.TypeByName("TorannMagic.MightPower");
                mightPowerAutoCastSetter = MethodInvoker.GetHandler(AccessTools.PropertySetter(mightPowerType, "AutoCast"));
                mightPowerInteractionTickField = AccessTools.FieldRefAccess<int>(mightPowerType, "interactionTick");
                // MightData
                mightDataAllMightPowersGetter = MethodInvoker.GetHandler(AccessTools.PropertyGetter("TorannMagic.MightData:AllMightPowers"));
                // CompAbilityUserMight
                compMightUserMightDataGetter = MethodInvoker.GetHandler(AccessTools.PropertyGetter("TorannMagic.CompAbilityUserMight:MightData"));

                //// Shared ////
                MP.RegisterSyncMethod(typeof(ARimWorldOfMagic), nameof(SyncedSetMagicAutoCast));
                MP.RegisterSyncMethod(typeof(ARimWorldOfMagic), nameof(SyncedSetMightAutoCast));
                // TM_Calc
                var tmCalcType = AccessTools.TypeByName("TorannMagic.TM_Calc");

                getMagicUserCompMethod = MethodInvoker.GetHandler(AccessTools.Method(tmCalcType, "GetCompAbilityUserMagic"));
                getMightUserCompMethod = MethodInvoker.GetHandler(AccessTools.Method(tmCalcType, "GetCompAbilityUserMight"));
            }

            #endregion

            // Multithreading
            {
                // It'll break the thingy from working properly, but for now let's keep it off to prevent desyncs
                MpCompat.harmony.Patch(AccessTools.Method("TorannMagic.FlyingObject_LivingWall:DoThreadedActions"),
                    prefix: new HarmonyMethod(typeof(ARimWorldOfMagic), nameof(CancelCall)));
            }

            // TODO:
            // TorannMagic.TM_Calc:GetSpriteArea
            // Maybe TorannMagic.ModOptions.TM_DebugTools:SpawnSpirit
        }

        private static void LatePatch()
        {
            #region LatePatch RNG

            // RNG
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

            #region LatePatch Gizmos

            // Gizmos
            {
                // TM_Action
                MpCompat.harmony.Patch(AccessTools.Method("TorannMagic.TM_Action:DrawAutoCastForGizmo"),
                    transpiler: new HarmonyMethod(typeof(ARimWorldOfMagic), nameof(Transpiler)));
            }

            #endregion
        }

        private static bool CancelCall() => !MP.IsInMultiplayer;

        private static void SyncMagicPowerAutoCast(object magicPower, bool target, Pawn pawn)
        {
            if (!MP.IsInMultiplayer)
            {
                magicPowerAutoCastSetter(magicPower, target);
                return;
            }

            // Prevent spam
            // The gizmo normally has a 5 tick cooldown before being able to change the auto cast state
            if (magicPowerInteractionTickField(magicPower) >= Find.TickManager.TicksGame)
                return;
            magicPowerInteractionTickField(magicPower) = Find.TickManager.TicksGame + 5;

            var magicUser = getMagicUserCompMethod(null, pawn);
            var magicData = compMagicUserMagicDataGetter(magicUser);
            var allPowers = (IList)magicDataAllMagicPowersGetter(magicData);
            var index = allPowers.IndexOf(magicPower);

            SyncedSetMagicAutoCast(pawn, index, target);
        }

        private static void SyncMightPowerAutoCast(object mightPower, bool target, Pawn pawn)
        {
            if (!MP.IsInMultiplayer)
            {
                mightPowerAutoCastSetter(mightPower, target);
                return;
            }

            // Prevent spam
            // The gizmo normally has a 5 tick cooldown before being able to change the auto cast state
            if (mightPowerInteractionTickField(mightPower) >= Find.TickManager.TicksGame)
                return;
            mightPowerInteractionTickField(mightPower) = Find.TickManager.TicksGame + 5;

            var mightUser = getMightUserCompMethod(null, pawn);
            var mightData = compMightUserMightDataGetter(mightUser);
            var allPowers = (IList)mightDataAllMightPowersGetter(mightData);
            var index = allPowers.IndexOf(mightPower);

            SyncedSetMightAutoCast(pawn, index, target);
        }

        private static void SyncedSetMagicAutoCast(Pawn pawn, int powerIndex, bool target)
        {
            var magicUser = getMagicUserCompMethod(null, pawn);
            var magicData = compMagicUserMagicDataGetter(magicUser);
            var allPowers = (IList)magicDataAllMagicPowersGetter(magicData);
            var power = allPowers[powerIndex];
            // Reset the interaction tick, otherwise the setter may not change the value as it was "interacted" with too recently
            magicPowerInteractionTickField(power) = 0;
            magicPowerAutoCastSetter(power, target);
        }

        private static void SyncedSetMightAutoCast(Pawn pawn, int powerIndex, bool target)
        {
            var mightUser = getMightUserCompMethod(null, pawn);
            var mightData = compMightUserMightDataGetter(mightUser);
            var allPowers = (IList)mightDataAllMightPowersGetter(mightData);
            var power = allPowers[powerIndex];
            // Reset the interaction tick, otherwise the setter may not change the value as it was "interacted" with too recently
            mightPowerInteractionTickField(power) = 0;
            mightPowerAutoCastSetter(power, target);
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instr)
        {
            var magicAutoCastTarget = AccessTools.DeclaredPropertySetter("TorannMagic.MagicPower:AutoCast");
            var mightAutoCastTarget = AccessTools.DeclaredPropertySetter("TorannMagic.MightPower:AutoCast");
            var magicAutoCastReplacement = AccessTools.Method(typeof(ARimWorldOfMagic), nameof(SyncMagicPowerAutoCast));
            var mightAutoCastReplacement = AccessTools.Method(typeof(ARimWorldOfMagic), nameof(SyncMightPowerAutoCast));

            var pawnAbilityField = AccessTools.Field("AbilityUser.Command_PawnAbility:pawnAbility");
            var pawnGetter = AccessTools.PropertyGetter("AbilityUser.PawnAbility:Pawn");

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
    }
}