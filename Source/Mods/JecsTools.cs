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
    [MpCompatFor("zal.jecslitels")]
    [MpCompatFor("zal.jecsliterwom")]
    public class JecsTools
    {
        #region Main patch

        private static bool patched = false;

        public JecsTools(ModContentPack mod)
        {
            if (patched)
                return;
            patched = true;

            const int totalPatchCount = 5;
            var patchedElementsCount = 0;

            // AbilityUser
            if (PatchAbilities())
                patchedElementsCount++;

            // CompActivatableEffect
            {
                // Deactivate/Activate gizmo
                var type = AccessTools.TypeByName("CompActivatableEffect.CompActivatableEffect");
                if (type != null)
                {
                    MpCompat.RegisterLambdaMethod(type, "EquippedGizmos", 0, 1);
                    patchedElementsCount++;
                }
            }

            // CompInstalledPart
            {
                var type = AccessTools.TypeByName("CompInstalledPart.InstalledPartFloatMenuPatch");
                if (type != null)
                {
                    MP.RegisterSyncMethodLambda(type, "GetFloatMenus", 3);
                    patchedElementsCount++;
                }
                // Looking at the code, it seems there's no built-in way to uninstall those?
            }

            // CompSlotLoadable
            {
                var type = AccessTools.TypeByName("CompSlotLoadable.CompSlotLoadable");
                if (type != null)
                {
                    MP.RegisterSyncMethod(type, "TryCancel"); // Gizmo -> cancel
                    MP.RegisterSyncMethod(type, "TryGiveLoadSlotJob"); // Gizmo -> float menu -> load option
                    MP.RegisterSyncMethod(type, "TryEmptySlot"); // Gizmo -> float menu -> empty option

                    MpCompat.RegisterLambdaMethod("CompSlotLoadable.SloatLoadbleFloatMenuPatch", "GetFloatMenus", 1);

                    patchedElementsCount++;
                }
            }

            // CompToggleDef
            {
                var type = AccessTools.TypeByName("CompToggleDef.ToggleDefCardUtility");
                if (type != null)
                {
                    MP.RegisterSyncMethod(type, "SwapThing").SetContext(SyncContext.MapSelected);

                    patchedElementsCount++;
                }
            }

            if (Prefs.DevMode)
                Log.Message($"Patched {patchedElementsCount} out of {totalPatchCount} elements of JecsTools.");
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

        private static bool PatchAbilities()
        {
            // PawnAbility
            var type = AccessTools.TypeByName("AbilityUser.PawnAbility");
            if (type == null)
                return false;

            pawnAbilityUserField = AccessTools.FieldRefAccess<ThingComp>(type, "abilityUser");

            MP.RegisterSyncMethod(type, "UseAbility").SetPostInvoke(StopTargeting);
            MP.RegisterSyncWorker<object>(SyncPawnAbility, type);

            // CompAbilityUser
            compAbilityUserAbilityDataField = AccessTools.FieldRefAccess<object>("AbilityUser.CompAbilityUser:abilityData");

            // AbilityData
            abilityDataAllPowersGetter = MethodInvoker.GetHandler(AccessTools.PropertyGetter("AbilityUser.AbilityData:AllPowers"));

            // AbilityUserUtility
            abilityUserUtilityGetCompsMethod = MethodInvoker.GetHandler(AccessTools.Method("AbilityUser.AbilityUserUtility:GetCompAbilityUsers"));

            return true;
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