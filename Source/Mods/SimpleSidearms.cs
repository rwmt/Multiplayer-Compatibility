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
        static ISyncField primaryWeaponModeSyncField;

        public SimpleSidearmsCompat(ModContentPack mod)
        {
            Type type;

            // Gizmo interactions
            {
                type = AccessTools.TypeByName("SimpleSidearms.utilities.WeaponAssingment");

                var methods = new[] {
                    "equipSpecificWeaponTypeFromInventory",
                    "equipSpecificWeapon",
                    "dropSidearm",
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
                    "UnsetMeleeWeaponPreference",
                    "ForgetSidearmMemory",
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
            {
                type = AccessTools.TypeByName("SimpleSidearms.rimworld.CompSidearmMemory");

                MpCompat.harmony.Patch(AccessTools.Method(type, "GetMemoryCompForPawn"),
                    postfix: new HarmonyMethod(typeof(SimpleSidearmsCompat), nameof(CaptureTheMemory)));
            }
            // Used often in the Set* methods for CompSidearmMemory
            {
                type = AccessTools.TypeByName("SimpleSidearms.rimworld.ThingDefStuffDefPair");

                MP.RegisterSyncWorker<object>(SyncWorkerForThingDefStuffDefPair, type);
            }

        }

        #region ThingDefStuffDefPair

        static void SyncWorkerForThingDefStuffDefPair(SyncWorker sync, ref object obj)
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
        static bool watching;

        // UGLY UGLY this is so UGLY! Yet... it works.
        static void CaptureTheMemory(object __result)
        {
            if (watching) {
                primaryWeaponModeSyncField.Watch(__result);
            }
        }

        static void HandleInteractionPrefix()
        {
            if (MP.IsInMultiplayer) {
                MP.WatchBegin();

                watching = true;
            }
        }
        static void HandleInteractionPostfix()
        {
            if (MP.IsInMultiplayer) {
                MP.WatchEnd();

                watching = false;
            }
        }

        #endregion
    }
}
