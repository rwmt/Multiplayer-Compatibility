using HarmonyLib;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>RimThunder - Core by RimThunder Teams</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3070495204"/>
    [MpCompatFor("rimthunder.core")]
    public class RimThunderCore
    {
        public RimThunderCore(ModContentPack mod)
        {
            // Of the 7 RimThunder mods, only Core ships compiled code for RimWorld 1.6
            // (Motorization.dll, GuidedMissile.dll) - the 6 addon packs (Breakthrough, Desert
            // Sabre, Flying Chariot, Liberty of Delivery, Red Dragon, Roaring Tiger) are pure
            // content packs with no 1.6 assembly, nothing to patch for those.

            // Motorization.CompAbilityEffect_ActiveProtectionSystem.CompTick rolls
            // Rand.Range(0f, 1f) against Props.chanceToFail every tick the ability is active, to
            // decide whether an incoming projectile gets intercepted - a gameplay-affecting roll
            // (DoIntercept, called from within the same method, can spawn a Thing via
            // GenSpawn.Spawn on a partial chance), with no Rand.PushState anywhere in the class.
            PatchIsolated("Motorization.CompAbilityEffect_ActiveProtectionSystem", "CompTick");
            // VehicleCompProjectileInterceptor.PostPostMake/PostExposeData (PostLoadInit branch)
            // both seed nextChargeTick via Find.TickManager.TicksGame + Rand.Range(0,
            // Props.chargeIntervalTicks) unprotected - fires whenever an interceptor-equipped
            // vehicle is generated or loaded (trade caravan, raid spawn, map gen, save load).
            PatchIsolated("Motorization.VehicleCompProjectileInterceptor", "PostPostMake");
            PatchIsolated("Motorization.VehicleCompProjectileInterceptor", "PostExposeData");

            // RimThunder ships its OWN deploy comp, Motorization.CompDeployable, separate from
            // Vehicle Framework's CompVehicleTurrets deploy: its CompGetGizmosExtra builds a
            // Command_Toggle whose toggleAction starts the DeployVehicle job and sets
            // deployTicks. Unsynced: only the clicking peer's vehicle deployed, and Deployed
            // drives movementStatus (mobileWhileDeployed) plus the Deployed/Undeployed vehicle
            // events every other peer then never saw.
            var deployable = AccessTools.TypeByName("Motorization.CompDeployable");
            if (deployable != null)
                MpCompat.RegisterLambdaMethod(deployable, "CompGetGizmosExtra", MethodType.Normal, 0);
            else
                Log.Warning("Multiplayer Compat :: RimThunder Core: could not find Motorization.CompDeployable - its deploy toggle will not be synced.");
        }

        private static void PatchIsolated(string typeName, string methodName)
        {
            var type = AccessTools.TypeByName(typeName);
            var method = type != null ? AccessTools.DeclaredMethod(type, methodName) : null;
            if (method != null)
            {
                MpCompat.harmony.Patch(method,
                    prefix: new HarmonyMethod(typeof(RimThunderCore), nameof(PreIsolateRand)),
                    finalizer: new HarmonyMethod(typeof(RimThunderCore), nameof(PostIsolateRand)));
            }
            else
            {
                Log.Warning($"Multiplayer Compat :: RimThunder Core: could not find {typeName}:{methodName} - mod may have updated, this fix is now stale for it.");
            }
        }

        // Isolate rather than reseed: neither roll needs to reproduce a particular result (unlike
        // e.g. a GenStep, which must reproduce the same map for a preview to match), it just must
        // not leak forward into the shared stream. A finalizer (not a postfix) so PopState still
        // runs if the patched method throws partway through.
        private static void PreIsolateRand() => Rand.PushState();
        private static void PostIsolateRand() => Rand.PopState();
    }
}
