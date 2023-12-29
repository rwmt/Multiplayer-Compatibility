using System;

using HarmonyLib;
using JetBrains.Annotations;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Simple Sidearms by PeteTimesSix</summary>
    /// <see href="https://github.com/PeteTimesSix/SimpleSidearms"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=927155256"/>
    /// <remarks>This would be so simple as a PR for SimpleSidearms</remarks>
    [MpCompatFor("PeteTimesSix.SimpleSidearms")]
    public class SimpleSidearmsCompat
    {
        // TODO: Suggest the author to encapsulate this, would simplify things so much
        [MpCompatSyncField("SimpleSidearms.rimworld.CompSidearmMemory", "primaryWeaponMode")]
        private static ISyncField primaryWeaponModeSyncField;

        public SimpleSidearmsCompat(ModContentPack mod)
        {
            MpCompatPatchLoader.LoadPatch(this);

            Type type;

            // Gizmo interactions
            {
                type = AccessTools.TypeByName("PeteTimesSix.SimpleSidearms.Utilities.WeaponAssingment");

                var methods = new[] {
                    "equipSpecificWeapon", // Called by equipSpecificWeaponFromInventory and equipSpecificWeaponTypeFromInventory
                    "DropSidearm",
                    // Tacticowl dual-wield support
                    "EquipSpecificWeaponFromInventoryAsOffhand",
                    "UnequipOffhand",
                };
                foreach (string method in methods) {
                    MP.RegisterSyncMethod(AccessTools.Method(type, method));
                }
            }

            {
                type = AccessTools.TypeByName("SimpleSidearms.rimworld.CompSidearmMemory");

                var methods = new[] {
                    "SetWeaponAsForced",
                    "SetRangedWeaponTypeAsDefault",
                    "SetMeleeWeaponTypeAsPreferred",
                    "SetUnarmedAsForced",
                    "SetUnarmedAsPreferredMelee",
                    "UnsetForcedWeapon",
                    "UnsetRangedWeaponDefault",
                    "UnsetMeleeWeaponPreference",
                    "UnsetUnarmedAsForced",
                    "ForgetSidearmMemory",
                    // Tacticowl dual wield support
                    "InformOfAddedSidearm", // As opposed to InformOfAddedPrimary, this one can be called from a place we need to sync
                };
                foreach (string method in methods) {
                    MP.RegisterSyncMethod(AccessTools.Method(type, method));
                }
            }

            // Patched sync methods
            {
                // When undrafted, the pawns will remove their temporary forced weapon.
                // Could cause issues when drafting pawns, as they'll be considered undrafted when the postfix runs.
                PatchingUtilities.PatchCancelInInterface("PeteTimesSix.SimpleSidearms.Intercepts.Pawn_DraftController_Drafted_Setter_Postfix:DraftedSetter");
                // When dropping a weapon, it'll cause the pawn to about preferences towards them.
                PatchingUtilities.PatchCancelInInterface("PeteTimesSix.SimpleSidearms.Intercepts.ITab_Pawn_Gear_InterfaceDrop_Prefix:InterfaceDrop");
            }
        }

        #region ThingDefStuffDefPair

        // Used often in the Set* methods for CompSidearmMemory
        [MpCompatSyncWorker("SimpleSidearms.rimworld.ThingDefStuffDefPair")]
        private static void SyncWorkerForThingDefStuffDefPair(SyncWorker sync, ref object obj)
        {
            var traverse = Traverse.Create(obj);

            var thingField = traverse.Field("thing");
            var stuffField = traverse.Field("stuff");

            if (sync.isWriting) {
                sync.Write(thingField.GetValue<ThingDef>());
                sync.Write(stuffField.GetValue<ThingDef>());
            } else {
                thingField.SetValue(sync.Read<ThingDef>());
                stuffField.SetValue(sync.Read<ThingDef>());
            }
        }

        #endregion

        #region primaryWeaponMode field watch

        // Required for primaryWeaponMode
        [MpCompatPrefix("SimpleSidearms.rimworld.Gizmo_SidearmsList", "handleInteraction")]
        private static void HandleInteractionPrefix(ThingComp ___pawnMemory)
        {
            if (MP.IsInMultiplayer) {
                MP.WatchBegin();
                primaryWeaponModeSyncField.Watch(___pawnMemory);
            }
        }

        [MpCompatPostfix("SimpleSidearms.rimworld.Gizmo_SidearmsList", "handleInteraction")]
        private static void HandleInteractionPostfix()
        {
            if (MP.IsInMultiplayer)
                MP.WatchEnd();
        }

        #endregion

        #region Stop verb init in interface

        [MpCompatPrefix("PeteTimesSix.SimpleSidearms.Utilities.GettersFilters", "isManualUse")]
        [MpCompatPrefix("PeteTimesSix.SimpleSidearms.Utilities.GettersFilters", "isDangerousWeapon")]
        [MpCompatPrefix("PeteTimesSix.SimpleSidearms.Utilities.GettersFilters", "isEMPWeapon")]
        [MpCompatPrefix("PeteTimesSix.SimpleSidearms.Utilities.GettersFilters", "findBestRangedWeapon", 8)]
        private static bool PrePrimaryVerbMethodCall(ThingWithComps __0, ref bool __result)
        {
            if (!MP.InInterface)
                return true;

            var comp = __0.GetComp<CompEquippable>();
            // Let the mod handle non-existent CompEquippable
            if (comp == null)
                return true;

            // If verbs are initialized, let the mod handle it as it wants
            if (comp.verbTracker.verbs != null)
                return true;

            // If verbs are null, assume false (is EMP, is dangerous, etc.)
            __result = false;
            // Initialize the verb
            SyncInitVerbsForComp(comp);
            // Prevent the method from running
            return false;
        }

        [MpCompatSyncMethod]
        private static void SyncInitVerbsForComp(CompEquippable comp)
        {
            if (comp.verbTracker.verbs == null)
                comp.verbTracker.InitVerbsFromZero();
        }

        #endregion
    }
}
