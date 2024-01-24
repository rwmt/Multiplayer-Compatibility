using System;
using System.Collections;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Vanilla Factions Expanded - Tribals by Oskar Potocki, xrushha, Taranchuk, Sarg Bjornson</summary>
    /// <see href="https://github.com/Vanilla-Expanded/VanillaFactionsExpanded-Tribals"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3079786283"/>
    [MpCompatFor("OskarPotocki.VFE.Tribals")]
    public class VanillaFactionsTribal
    {
        #region Main patch

        public VanillaFactionsTribal(ModContentPack mod)
        {
            // Needs to be done late or will cause issues with resource loading from non-main thread.
            LongEventHandler.ExecuteWhenFinished(() => MpCompatPatchLoader.LoadPatch(this));

            #region RNG

            {
                PatchingUtilities.PatchSystemRand("VFETribals.RitualOutcomeEffectWorker_TribalGathering:Apply", false);
            }

            #endregion

            #region Gizmos

            {
                // Light (0)/extinguish (2) large fire, cancel: lighting(1)/extinguishing(3)
                MpCompat.RegisterLambdaDelegate("VFETribals.LargeFire", "GetGizmos", 0, 1, 2, 3);
            }

            #endregion

            #region Choice Letters

            {
                // Neither of the choice letters has a timeout, so we don't need to deal with that.
                // Accept (0), reject with a chance of raid (1)
                var type = AccessTools.TypeByName("VFETribals.ChoiceLetter_WildMenJoin");
                foreach (var method in MpMethodUtil.GetLambda(type, "Choices", MethodType.Getter, null, 0, 1))
                    MP.RegisterSyncMethod(method);
            }

            #endregion

            #region Cornerstones

            {
                var type = customizeCornerstonesWindowType = AccessTools.TypeByName("VFETribals.Window_CustomizeCornerstones");
                fillCornerstoneDefsMethod = MethodInvoker.GetHandler(
                    AccessTools.DeclaredMethod(type, "FillCornerstoneDefs"));

                type = AccessTools.TypeByName("VFETribals.GameComponent_Tribals");
                tribalsGameCompInstance = AccessTools.StaticFieldRefAccess<GameComponent>(AccessTools.DeclaredField(type, "Instance"));
                MP.RegisterSyncMethod(type, "AddCornerstone")
                    .SetPostInvoke(PostAddCornerstone);
            }

            #endregion
        }

        #endregion

        #region Cornerstones

        // GameComponent_Tribals
        private static AccessTools.FieldRef<GameComponent> tribalsGameCompInstance;
        [MpCompatSyncField("VFETribals.GameComponent_Tribals", "ethos")]
        private static ISyncField tribalsGameCompEthosField;
        [MpCompatSyncField("VFETribals.GameComponent_Tribals", "ethosLocked")]
        private static ISyncField tribalsGameCompEthosLockedField;

        // Window_CustomizeCornerstones
        private static Type customizeCornerstonesWindowType;
        private static FastInvokeHandler fillCornerstoneDefsMethod;

        [MpCompatPrefix("VFETribals.GameComponent_Tribals", "AddCornerstone")]
        private static bool PreAddCornerstone(Def def, int ___availableCornerstonePoints, IList ___cornerstones)
        {
            if (MP.IsInMultiplayer || !MP.IsExecutingSyncCommand)
                return true;

            // Make sure that after syncing the cornerstone selection we're not in a situation where
            // either two players pick a cornerstone at a similar time, or a repeated sync happened,
            // by ensuring that there's still free points to select a cornerstone, and that it
            // wasn't selected by the player yet.
            return ___availableCornerstonePoints > 0 && !___cornerstones.Contains(def);
        }

        private static void PostAddCornerstone(object instance, object[] args)
        {
            // Ensure the list of cornerstones the player sees is up-to-date after picking one
            foreach (var window in Find.WindowStack.Windows)
            {
                if (customizeCornerstonesWindowType.IsInstanceOfType(window))
                    fillCornerstoneDefsMethod(window);
            }
        }

        [MpCompatPrefix("VFETribals.Window_CustomizeCornerstones", "DoWindowContents")]
        [MpCompatPrefix("VFETribals.Dialog_EditEthos", "ApplyChanges")]
        private static void PreEthosFieldWatch()
        {
            if (!MP.IsInMultiplayer)
                return;

            var gameComp = tribalsGameCompInstance();
            MP.WatchBegin();
            tribalsGameCompEthosField.Watch(gameComp);
            tribalsGameCompEthosLockedField.Watch(gameComp);
        }

        [MpCompatPostfix("VFETribals.Window_CustomizeCornerstones", "DoWindowContents")]
        [MpCompatPostfix("VFETribals.Dialog_EditEthos", "ApplyChanges")]
        private static void PostEthosFieldWatch()
        {
            if (MP.IsInMultiplayer)
                MP.WatchEnd();
        }

        #endregion

        #region Configure ideo

        // After several days of work on this, I realized that trying to get this to work
        // would most likely require patching a bunch of stuff in vanilla classes like
        // Page_ConfigureIdeo, Page_ConfigureFluidIdeo, as well as their related helper
        // classes. Leaving this as-is due to having issues implementing an MP-safe way
        // to handle this. The host can use the menu and then let create a joinpoint.
        // Shouldn't be that bad, especially since it won't happen more than once per game*.
        // *assuming no multifaction - in multifaction it would need extra patches to work,
        // which would raise the complexity even further.

        #endregion

        #region Game Components

        // This class will cause quite a bit of issues with multifaction, basically
        // everything it does assumes one player faction - stuff like keeping track
        // of player tech level, researched technologies, unlocked cornerstones...
        // Will need a lot of patching to be fully compatible, especially introducing
        // data like that for every single faction and properly handling current faction.

        #endregion
    }
}