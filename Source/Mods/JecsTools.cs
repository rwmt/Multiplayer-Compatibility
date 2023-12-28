using System.Collections;
using System.Linq;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>JecsTools by jecrell and contributors</summary>
    /// <see href="https://github.com/jecrell/JecsTools"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=932008009"/>
    [MpCompatFor("jecrell.jecstools")]
    public class JecsTools
    {
        #region Main patch

        public JecsTools(ModContentPack mod)
        {
            // AbilityUser
            PatchAbilities();

            // CompActivatableEffect
            {
                // Deactivate/Activate gizmo
                MpCompat.RegisterLambdaMethod("CompActivatableEffect.CompActivatableEffect", "EquippedGizmos", 0, 1);
            }

            // CompInstalledPart
            {
                MpCompat.RegisterLambdaDelegate("CompInstalledPart.InstalledPartFloatMenuPatch", "GetFloatMenus", 3);
                // Looking at the code, it seems there's no built-in way to uninstall those?
            }

            // CompSlotLoadable
            {
                var type = AccessTools.TypeByName("CompSlotLoadable.CompSlotLoadable");
                MP.RegisterSyncMethod(type, "TryCancel"); // Gizmo -> cancel
                MP.RegisterSyncMethod(type, "TryGiveLoadSlotJob"); // Gizmo -> float menu -> load option
                MP.RegisterSyncMethod(type, "TryEmptySlot"); // Gizmo -> float menu -> empty option
                MpCompat.RegisterLambdaDelegate("CompSlotLoadable.SloatLoadbleFloatMenuPatch", "GetFloatMenus", 1);
            }

            // CompToggleDef
            {
                MP.RegisterSyncMethod(AccessTools.Method("CompToggleDef.ToggleDefCardUtility:SwapThing")).SetContext(SyncContext.MapSelected);
            }
        }

        #endregion

        #region Abilities

        // PawnAbility
        private static AccessTools.FieldRef<object, ThingComp> pawnAbilityUserField;

        // CompAbilityUser
        private static AccessTools.FieldRef<object, object> compAbilityUserAbilityDataField;

        // AbilityData
        private static FastInvokeHandler abilityDataAllPowersGetter;

        // AbilityUserUtility
        private static FastInvokeHandler abilityUserUtilityGetCompsMethod;

        private static void PatchAbilities()
        {
            // PawnAbility
            var type = AccessTools.TypeByName("AbilityUser.PawnAbility");
            pawnAbilityUserField = AccessTools.FieldRefAccess<ThingComp>(type, "abilityUser");

            MP.RegisterSyncMethod(type, "UseAbility").SetPostInvoke(StopTargeting);
            MP.RegisterSyncWorker<object>(SyncPawnAbility, type);

            // CompAbilityUser
            compAbilityUserAbilityDataField = AccessTools.FieldRefAccess<object>("AbilityUser.CompAbilityUser:abilityData");

            // AbilityData
            abilityDataAllPowersGetter = MethodInvoker.GetHandler(AccessTools.PropertyGetter("AbilityUser.AbilityData:AllPowers"));

            // AbilityUserUtility
            abilityUserUtilityGetCompsMethod = MethodInvoker.GetHandler(AccessTools.Method("AbilityUser.AbilityUserUtility:GetCompAbilityUsers"));
        }

        private static void StopTargeting(object instance, object[] args)
        {
            // The job driver is assigning Find.Targeter.targetingSource, starting targeting again.
            // We need to stop targeting after casting or we'll start targeting again after casting.
            if (MP.IsExecutingSyncCommandIssuedBySelf)
                Find.Targeter.StopTargeting();
        }

        private static void SyncPawnAbility(SyncWorker sync, ref object ability)
        {
            if (sync.isWriting)
            {
                // The comp.props seems null, at least in some cases - which is what MP sync worker uses for syncing.
                // We need to sync it differently.
                var abilityUserComp = pawnAbilityUserField(ability);
                var comps = ((IEnumerable)abilityUserUtilityGetCompsMethod(null, abilityUserComp.parent)).Cast<ThingComp>().ToArray();

                var foundMatch = false;
                for (var index = 0; index < comps.Length; index++)
                {
                    var comp = comps[index];
                    var data = compAbilityUserAbilityDataField(comp);
                    var allPowers = (IList)abilityDataAllPowersGetter(data);
                    var innerIndex = allPowers.IndexOf(ability);

                    if (innerIndex >= 0)
                    {
                        foundMatch = true;
                        sync.Write(index);
                        sync.Write(innerIndex);
                        sync.Write(abilityUserComp.parent); // Parent pawn
                        break;
                    }
                }

                if (!foundMatch)
                    sync.Write(-1);
            }
            else
            {
                var index = sync.Read<int>();

                if (index < 0)
                    return;

                var innerIndex = sync.Read<int>();
                var pawn = sync.Read<Pawn>();

                var allComps = ((IEnumerable)abilityUserUtilityGetCompsMethod(null, pawn)).Cast<ThingComp>().ToArray();
                var comp = allComps[index];
                var data = compAbilityUserAbilityDataField(comp);
                var all = (IList)abilityDataAllPowersGetter(data);

                ability = all[innerIndex];
            }
        }

        #endregion
    }
}