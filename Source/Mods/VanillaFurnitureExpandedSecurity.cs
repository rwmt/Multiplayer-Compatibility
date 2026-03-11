using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using Verse;
using Verse.Sound;

namespace Multiplayer.Compat
{
    /// <summary>Vanilla Furniture Expanded - Security by Oskar Potocki, Sokyran, Taranchuk</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=1845154007"/>
    /// <see href="https://github.com/Vanilla-Expanded/VanillaFurnitureExpanded-Security"/>
    /// Contribution to Multiplayer Compatibility by Sokyran and Reshiram (original)
    /// Updated for reworked VFE-Security (1.6) by sviyh
    [MpCompatFor("VanillaExpanded.VFESecurity")]
    class VFESecurity
    {
        private static AccessTools.FieldRef<object, int> concealedTransitionDurationField;
        private static AccessTools.FieldRef<object, int> concealedTransitionTicksField;
        private static AccessTools.FieldRef<object, Effecter> concealedProgressBarField;
        private static FastInvokeHandler concealedIsPowerOff;
        private static FastInvokeHandler concealedGetProps;
        private static AccessTools.FieldRef<object, int> propsSubmergeSeconds;
        private static AccessTools.FieldRef<object, int> propsDeploySeconds;

        public VFESecurity(ModContentPack mod)
        {
            // Most patches are deferred to LatePatch because accessing VFESecurity types
            // (e.g. CompConcealed) triggers their static constructors which load textures.
            // Texture loading must happen on the main thread, so we use ExecuteWhenFinished.
            LongEventHandler.ExecuteWhenFinished(LatePatch);

            // RNG fixes - these use string-based lookups and don't trigger static ctors
            {
                PatchingUtilities.PatchPushPopRand("VFESecurity.Verb_BurnerFireSpew:TryCastShot");
                PatchingUtilities.PatchPushPopRand("VFESecurity.Verb_Dazzle:SearchlightEffect");
            }
        }

        private static void LatePatch()
        {
            // Concealed turrets/barriers - deploy/submerge toggle
            {
                var type = AccessTools.TypeByName("VFESecurity.CompConcealed");
                var propsType = AccessTools.TypeByName("VFESecurity.CompProperties_Concealed");

                // Cache field refs for the prefix
                concealedTransitionDurationField = AccessTools.FieldRefAccess<int>(type, "transitionDuration");
                concealedTransitionTicksField = AccessTools.FieldRefAccess<int>(type, "transitionTicks");
                concealedProgressBarField = AccessTools.FieldRefAccess<Effecter>(type, "progressBar");
                concealedIsPowerOff = MethodInvoker.GetHandler(AccessTools.DeclaredMethod(type, "IsPowerOff"));
                concealedGetProps = MethodInvoker.GetHandler(AccessTools.DeclaredPropertyGetter(type, "Props"));
                propsSubmergeSeconds = AccessTools.FieldRefAccess<int>(propsType, "submergeSeconds");
                propsDeploySeconds = AccessTools.FieldRefAccess<int>(propsType, "deploySeconds");

                // Sync StartTransition directly. The gizmo actions call it as an
                // instance method on CompConcealed (a ThingComp), which the MP
                // framework can serialize. No need to sync the lambdas themselves.
                MP.RegisterSyncMethod(type, "StartTransition");

                // StartTransition iterates Find.Selector.SelectedObjects which won't
                // contain the right objects on the remote client. In MP we replace it
                // to only affect `this` comp (the synced instance).
                MpCompat.harmony.Patch(
                    AccessTools.DeclaredMethod(type, "StartTransition"),
                    prefix: new HarmonyMethod(typeof(VFESecurity), nameof(PreStartTransition)));
            }

            // World artillery
            {
                var type = AccessTools.TypeByName("VFESecurity.CompWorldArtillery");

                // StartAttack sets worldTarget, target, and calls OrderAttack.
                var startAttack = AccessTools.DeclaredMethod(type, "StartAttack");
                MP.RegisterSyncMethod(startAttack).SetContext(SyncContext.MapSelected);
                MpCompat.harmony.Patch(startAttack,
                    prefix: new HarmonyMethod(typeof(VFESecurity), nameof(PreStartAttack)));

                // Reset clears the artillery targeting state
                MP.RegisterSyncMethod(type, "Reset");
            }

            // The mod's Harmony patches on Building_TurretGun call comp.Reset()
            // on the UI side. Cancel them in interface - they'll run via sync.
            {
                PatchingUtilities.PatchCancelInInterface(
                    "VFESecurity.Building_TurretGun_OrderAttack_Patch:Prefix");
                PatchingUtilities.PatchCancelInInterface(
                    "VFESecurity.Building_TurretGun_ResetForcedTarget_Patch:Prefix");
            }

            // RNG fix for artillery miss radius calculation
            PatchingUtilities.PatchPushPopRand("VFESecurity.ArtilleryUtils:SpawnArtilleryProjectile");
        }

        /// <summary>
        /// In multiplayer, StartTransition iterates Find.Selector.SelectedObjects which
        /// won't have the right selection on the remote client. Replace it to apply the
        /// transition only to `this` comp. The MP framework handles multi-select by
        /// calling each synced gizmo lambda on its respective comp instance.
        /// </summary>
        private static bool PreStartTransition(object __instance, bool submerge)
        {
            if (!MP.IsInMultiplayer)
                return true;

            var comp = __instance;
            if ((bool)concealedIsPowerOff(comp))
                return false;

            var props = concealedGetProps(comp);
            var duration = submerge ? propsSubmergeSeconds(props) * 60 : propsDeploySeconds(props) * 60;
            concealedTransitionDurationField(comp) = duration;
            concealedTransitionTicksField(comp) = duration;
            concealedProgressBarField(comp) = EffecterDefOf.ProgressBar.Spawn();

            if (submerge)
            {
                var parent = ((ThingComp)comp).parent;
                SoundDefOf.Door_OpenPowered.PlayOneShot(new TargetInfo(parent.Position, parent.Map));
            }

            return false;
        }

        /// <summary>
        /// Hide world view immediately on the calling client when starting
        /// an artillery attack, since waiting for sync may take a while.
        /// </summary>
        private static void PreStartAttack()
        {
            if (!MP.IsExecutingSyncCommand)
                CameraJumper.TryHideWorld();
        }
    }
}
