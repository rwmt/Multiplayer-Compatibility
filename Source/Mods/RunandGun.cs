using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using UnityEngine;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>RunAndGun by roolo</summary>
    /// <see href="https://github.com/rheirman/RunAndGun"/>
    /// <see href="https://github.com/MemeGoddess/RunAndGun"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=1204108550"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3523879860"/>
    [MpCompatFor("roolo.RunAndGun")]
    [MpCompatFor("roolo.RunAndGun.kotobike")]
    [MpCompatFor("memegoddess.RunAndGun")]
    class RunandGun
    {
        private const string GizmoPatch = "RunAndGun.Harmony.Pawn_DraftController_GetGizmos_Patch";
        private const string RunAndGunIconPath = "UI/Buttons/enable_RG";

        private static FieldInfo compIsEnabledField;
        private static FieldInfo weaponForbidderField;
        private static Texture2D runAndGunIcon;

        public RunandGun(ModContentPack mod)
        {
            RegisterToggleSync();
            PatchPawnGetGizmos();
            PatchMentalStateRand();
        }

        private static void PatchMentalStateRand()
        {
            var patchType = AccessTools.TypeByName("RunAndGun.Harmony.MentalStateHandler_TryStartMentalState");
            if (patchType == null)
                return;

            MethodInfo target = null;
            try
            {
                target = MpMethodUtil.GetLocalFunc(patchType, "Postfix", localFunc: "shouldRunAndGun");
            }
            catch (Exception)
            {
                // Roslyn local-function name may differ between builds
            }

            target ??= AccessTools.GetDeclaredMethods(patchType)
                .FirstOrDefault(m => m.Name.Contains("shouldRunAndGun", StringComparison.Ordinal));

            if (target != null)
                PatchingUtilities.PatchSystemRand(target, false);
        }

        /// <summary>Fallback gizmo only — no MP.WatchBegin here (runs every GUI frame and breaks combat sync).</summary>
        private static void PatchPawnGetGizmos()
        {
            var getGizmos = AccessTools.Method(typeof(Pawn), nameof(Pawn.GetGizmos));
            MpCompat.harmony.Patch(getGizmos,
                postfix: new HarmonyMethod(typeof(RunandGun), nameof(PawnGetGizmosPostfix)));
        }

        /// <summary>
        /// Sync delegate must include the captured CompRunAndGun ("data") or the host cannot apply OFF.
        /// kotobike/memegoddess: toggle is &lt;Postfix&gt;b__1; roolo 1.4 uses lambda ordinal 2.
        /// </summary>
        private static void RegisterToggleSync()
        {
            var patchType = AccessTools.TypeByName(GizmoPatch);
            if (patchType == null)
                return;

            var compType = AccessTools.TypeByName("RunAndGun.CompRunAndGun");
            if (compType != null)
                MP.RegisterSyncField(compType, "isEnabled");

            MP.RegisterSyncMethod(typeof(RunandGun), nameof(SyncSetRunAndGunEnabled));

            string[] closureFields = ["data"];
            var registered = false;

            try
            {
                MP.RegisterSyncDelegate(patchType, "<>c__DisplayClass0_0", "<Postfix>b__1", closureFields);
                registered = true;
            }
            catch (Exception)
            {
                // display class name differs between builds
            }

            foreach (var ord in new[] { 1, 2 })
            {
                try
                {
                    MpCompat.RegisterLambdaDelegate(GizmoPatch, "Postfix", closureFields, ord);
                    registered = true;
                }
                catch (Exception)
                {
                    // try next ordinal
                }
            }

            if (!registered)
                Log.Warning("MPCompat :: RunAndGun toggle lambda not found (tried direct delegate and ordinals 1, 2)");
        }

        public static void SyncSetRunAndGunEnabled(Pawn pawn, bool enabled)
        {
            if (pawn == null)
                return;

            foreach (var comp in pawn.AllComps)
            {
                if (comp.GetType().Name != "CompRunAndGun")
                    continue;

                compIsEnabledField ??= AccessTools.Field(comp.GetType(), "isEnabled");
                compIsEnabledField?.SetValue(comp, enabled);
                return;
            }
        }

        private static void PawnGetGizmosPostfix(Pawn __instance, ref IEnumerable<Gizmo> __result)
        {
            if (!MP.IsInMultiplayer || __instance == null || !__instance.Drafted)
                return;

            try
            {
                if (__instance.Faction != Faction.OfPlayer || !PawnHasRangedWeapon(__instance))
                    return;

                var comp = GetRunAndGunComp(__instance);
                if (comp == null || IsWeaponForbidden(__instance))
                    return;

                var list = __result?.ToList() ?? new List<Gizmo>();
                if (HasRunAndGunGizmo(list))
                {
                    __result = list;
                    return;
                }

                runAndGunIcon ??= ContentFinder<Texture2D>.Get(RunAndGunIconPath, true);
                var isEnabled = GetRunAndGunEnabled(comp);
                list.Add(new Command_Toggle
                {
                    defaultLabel = "RG_Action_Enable_Label".Translate(),
                    defaultDesc = (isEnabled ? "RG_Action_Disable_Description" : "RG_Action_Enable_Description").Translate(),
                    icon = runAndGunIcon,
                    isActive = () => GetRunAndGunEnabled(comp),
                    toggleAction = () => SyncSetRunAndGunEnabled(__instance, !GetRunAndGunEnabled(comp)),
                });
                __result = list;
            }
            catch (Exception ex)
            {
                Log.Warning($"MPCompat :: RunAndGun pawn GetGizmos compat skipped: {ex.Message}");
            }
        }

        private static ThingComp GetRunAndGunComp(Pawn pawn)
        {
            foreach (var comp in pawn.AllComps)
            {
                if (comp.GetType().Name == "CompRunAndGun")
                    return comp;
            }

            return null;
        }

        private static bool GetRunAndGunEnabled(ThingComp comp)
        {
            compIsEnabledField ??= AccessTools.Field(comp.GetType(), "isEnabled");
            return compIsEnabledField != null && (bool)compIsEnabledField.GetValue(comp);
        }

        private static bool HasRunAndGunGizmo(List<Gizmo> gizmos)
        {
            runAndGunIcon ??= ContentFinder<Texture2D>.Get(RunAndGunIconPath, true);
            if (runAndGunIcon == null)
                return false;

            var iconName = runAndGunIcon.name;
            return gizmos.OfType<Command_Toggle>().Any(g => g.icon != null && g.icon.name == iconName);
        }

        private static bool PawnHasRangedWeapon(Pawn pawn)
        {
            var primary = pawn.equipment?.Primary;
            return primary?.def?.IsRangedWeapon == true;
        }

        private static bool IsWeaponForbidden(Pawn pawn)
        {
            try
            {
                if (pawn.equipment?.Primary == null)
                    return false;

                var baseType = AccessTools.TypeByName("RunAndGun.Base");
                if (baseType == null)
                    return false;

                weaponForbidderField ??= AccessTools.Field(baseType, "weaponForbidder");
                var forbidder = weaponForbidderField?.GetValue(null);
                if (forbidder == null)
                    return false;

                var innerListField = AccessTools.Field(forbidder.GetType(), "InnerList");
                var innerList = innerListField?.GetValue(forbidder) as System.Collections.IDictionary;
                if (innerList == null)
                    return false;

                if (!innerList.Contains(pawn.equipment.Primary.def.defName))
                    return false;

                var record = innerList[pawn.equipment.Primary.def.defName];
                var isSelectedField = AccessTools.Field(record.GetType(), "isSelected");
                return isSelectedField != null && (bool)isSelectedField.GetValue(record);
            }
            catch
            {
                return false;
            }
        }
    }
}
