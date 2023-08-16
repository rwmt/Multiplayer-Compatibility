using System;

using HarmonyLib;
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
        private static ISyncField primaryWeaponModeSyncField;

        public SimpleSidearmsCompat(ModContentPack mod)
        {
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

                // TODO: Suggest the author to encapsulate this, would simplify things so much
                primaryWeaponModeSyncField = MP.RegisterSyncField(AccessTools.Field(type, "primaryWeaponMode"));
            }

            // Required for primaryWeaponMode
            {
                type = AccessTools.TypeByName("SimpleSidearms.rimworld.Gizmo_SidearmsList");

                MpCompat.harmony.Patch(AccessTools.Method(type, "handleInteraction"),
                    prefix: new HarmonyMethod(typeof(SimpleSidearmsCompat), nameof(HandleInteractionPrefix)),
                    postfix: new HarmonyMethod(typeof(SimpleSidearmsCompat), nameof(HandleInteractionPostfix)));
            }

            // Used often in the Set* methods for CompSidearmMemory
            {
                type = AccessTools.TypeByName("SimpleSidearms.rimworld.ThingDefStuffDefPair");

                MP.RegisterSyncWorker<object>(SyncWorkerForThingDefStuffDefPair, type);
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

        private static void HandleInteractionPrefix(ThingComp ___pawnMemory)
        {
            if (MP.IsInMultiplayer) {
                MP.WatchBegin();
                primaryWeaponModeSyncField.Watch(___pawnMemory);
            }
        }

        private static void HandleInteractionPostfix()
        {
            if (MP.IsInMultiplayer)
                MP.WatchEnd();
        }

        #endregion
    }
}
