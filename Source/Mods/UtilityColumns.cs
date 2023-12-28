using System.Collections;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Utility Columns by Nephlite, Maxim</summary>
    /// <see href="https://github.com/RealTelefonmast/UtilityColumns"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2013476665"/>
    [MpCompatFor("nephlite.orbitaltradecolumn")]
    public class UtilityColumns
    {
        // Building_ClaymoreColumn
        private static AccessTools.FieldRef<object, IList> claymoreColumnChargesField;

        // ClaymoreCharge
        private static AccessTools.FieldRef<object, bool[]> claymoreChargeSettingsField;

        // Gizmo_ClaymoreSafetySettings
        private static AccessTools.FieldRef<object, Building> gizmoClaymoreReferenceField;
        private static AccessTools.FieldRef<object, Gizmo> gizmoInnerClassParentField;

        public UtilityColumns(ModContentPack mod)
        {
            // Building_ClaymoreColumn
            var type = AccessTools.TypeByName("RimWorldColumns.Building_ClaymoreColumn");

            claymoreColumnChargesField = AccessTools.FieldRefAccess<IList>(type, "Charges");
            MpCompat.RegisterLambdaMethod(type, "GetGizmos", 0, 1).SetDebugOnly();

            // ClaymoreCharge
            claymoreChargeSettingsField = AccessTools.FieldRefAccess<bool[]>("RimWorldColumns.ClaymoreCharge:settings");

            // Other
            MP.RegisterSyncMethod(typeof(UtilityColumns), nameof(SyncSettings));

            LongEventHandler.ExecuteWhenFinished(LatePatch);
        }

        private static void LatePatch()
        {
            // Gizmo_ClaymoreSafetySettings
            var type = AccessTools.TypeByName("RimWorldColumns.Gizmo_ClaymoreSafetySettings");
            var innerMethod = MpMethodUtil.GetLambda(type, "GizmoOnGUI", lambdaOrdinal: 0);

            gizmoClaymoreReferenceField = AccessTools.FieldRefAccess<Building>(type, "claymoreReference");
            gizmoInnerClassParentField = AccessTools.FieldRefAccess<Gizmo>(innerMethod.DeclaringType, "<>4__this");

            MpCompat.harmony.Patch(innerMethod,
                prefix: new HarmonyMethod(typeof(UtilityColumns), nameof(PreDraw)),
                postfix: new HarmonyMethod(typeof(UtilityColumns), nameof(PostDraw)));
        }

        private static void PreDraw(object __instance, ref bool[][] __state)
        {
            if (!MP.IsInMultiplayer)
                return;

            var gizmo = gizmoInnerClassParentField(__instance);
            var claymoreReference = gizmoClaymoreReferenceField(gizmo);
            var charges = claymoreColumnChargesField(claymoreReference);
            __state = new bool[4][];

            for (var i = 0; i < 4; i++)
            {
                var current = claymoreChargeSettingsField(charges[i]);
                __state[i] = new[] { current[0], current[1], current[2] };
            }
        }

        private static void PostDraw(object __instance, ref bool[][] __state)
        {
            if (!MP.IsInMultiplayer)
                return;

            var gizmo = gizmoInnerClassParentField(__instance);
            var claymoreReference = gizmoClaymoreReferenceField(gizmo);
            var charges = claymoreColumnChargesField(claymoreReference);
            var isEqual = true;

            for (var i = 0; i < 4; i++)
            {
                var current = claymoreChargeSettingsField(charges[i]);
                var previous = __state[i];

                if (current[0] != previous[0] || current[1] != previous[1] || current[2] != previous[2])
                {
                    isEqual = false;
                    break;
                }
            }

            if (isEqual)
                return;

            var changed = new bool[4][];

            for (var i = 0; i < 4; i++)
            {
                changed[i] = claymoreChargeSettingsField(charges[i]);
                claymoreChargeSettingsField(charges[i]) = __state[i];
            }

            SyncSettings(claymoreReference, changed);
        }

        private static void SyncSettings(Building column, bool[][] settings)
        {
            var charges = claymoreColumnChargesField(column);

            for (var i = 0; i < 4; i++)
                claymoreChargeSettingsField(charges[i]) = settings[i];
        }
    }
}