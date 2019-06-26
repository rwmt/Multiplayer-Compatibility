using System;
using System.Linq;
using System.Reflection;
using Harmony;
using Multiplayer.API;
using Multiplayer.Client;
using RimWorld;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Giddy Up! Ride and Roll by Roolo</summary>
    /// <remarks>Designated areas are trouble</remarks>
    /// <see href="https://github.com/rheirman/GiddyUpRideAndRoll"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=1331961995"/>
    [MpCompatFor("Giddy-up! Ride and Roll")]
    public class GiddyUpRideAndRollCompat
    {
        static Type AreaGUType;

        static FieldInfo AreaLabelField;
        static MethodInfo SetSelectedAreaMethod;

        static Type DesignatorDropAnimalExpandType;
        static Type DesignatorDropAnimalClearType;
        static Type DesignatorNoMountExpandType;
        static Type DesignatorNoMountClearType;

        public GiddyUpRideAndRollCompat(ModContentPack mod)
        {
            // Stop waiting
            MP.RegisterSyncDelegate(
                AccessTools.TypeByName("GiddyUpRideAndRoll.Harmony.Pawn_GetGizmos"),
                "<>c__DisplayClass1_0",
                "<CreateGizmo_LeaveRider>b__0"
            );

            // Unsure... May not be needed. Harmless to have it.
            MP.RegisterSyncMethod(
                AccessTools.Method("GiddyUpRideAndRoll.Harmony.CaravanEnterMapUtility_Enter:MountCaravanMounts")
            );

            // AreaGU Randomness
            {
                AreaGUType = AccessTools.TypeByName("GiddyUpCore.Zones.Area_GU");

                MpCompat.harmony.Patch(AccessTools.Method("Verse.AreaManager:AddStartingAreas"),
                    postfix: new HarmonyMethod(typeof(GiddyUpRideAndRollCompat), nameof(AddStartingAreasPostfix))
                    );

                MpCompat.harmony.Patch(AccessTools.Method(AreaGUType, "GetUniqueLoadID"),
                    postfix: new HarmonyMethod(typeof(GiddyUpRideAndRollCompat), nameof(GetUniqueLoadIDPostfix))
                    );
            }

            // Designators
            {
                Type type = AccessTools.TypeByName("GiddyUpCore.Zones.Designator_GU");

                AreaLabelField = AccessTools.Field(type, "areaLabel");
                SetSelectedAreaMethod = AccessTools.Method(type, "setSelectedArea");

                MP.RegisterSyncWorker<Designator>(DesignatorGU, type, true);

                DesignatorDropAnimalExpandType = AccessTools.TypeByName("GiddyUpRideAndRoll.Zones.Designator_GU_DropAnimal_Expand");
                DesignatorDropAnimalClearType = AccessTools.TypeByName("GiddyUpRideAndRoll.Zones.Designator_GU_DropAnimal_Clear");
                DesignatorNoMountExpandType = AccessTools.TypeByName("GiddyUpRideAndRoll.Zones.Designator_GU_NoMount_Expand");
                DesignatorNoMountClearType = AccessTools.TypeByName("GiddyUpRideAndRoll.Zones.Designator_GU_NoMount_Clear");
            }
        }


        #region Designators

        static void DesignatorGU(SyncWorker sync, ref Designator designator)
        {
            if (sync.isWriting) {
                Type t = designator.GetType();
                byte tByte = ByteFromType(t);

                sync.Write(tByte);

            } else {
                byte tByte = sync.Read<byte>();
                Type t = TypeFromByte(tByte);

                designator = (Designator) Activator.CreateInstance(t);

                string label = (string) AreaLabelField.GetValue(designator);

                SetSelectedAreaMethod.Invoke(designator, new object[] { label });
            }
        }
        static Type TypeFromByte(byte tByte)
        {
            switch (tByte) {
            case 1: return DesignatorDropAnimalExpandType;
            case 2: return DesignatorDropAnimalClearType;
            case 3: return DesignatorNoMountExpandType;
            case 4: return DesignatorNoMountClearType;
            default: throw new Exception("Unknown Designator for GiddyUP");
            }
        }
        static byte ByteFromType(Type type)
        {
            if (type == DesignatorDropAnimalExpandType) {
                return 1;
            }
            if (type == DesignatorDropAnimalClearType) {
                return 2;
            }
            if (type == DesignatorNoMountExpandType) {
                return 3;
            }
            if (type == DesignatorNoMountClearType) {
                return 4;
            }
            throw new Exception("Unknown Designator for GiddyUP");
        }

        #endregion

        #region AreaGU
        // This prevents GiddyUP from creating the areas while the game is already going
        static void AddStartingAreasPostfix(AreaManager __instance)
        {
            string[] zoneNames = {
                "Gu_Area_DropMount",
                "Gu_Area_NoMount",
            };
            foreach(string name in zoneNames) {
                var areaGU = (Area) Activator.CreateInstance(AreaGUType, new object[] { __instance, name });

                __instance.areas.Add(areaGU);
            }
        }
        // Multiplayer supports many player factions, GiddyUP assumes their areas are unique
        static void GetUniqueLoadIDPostfix(Area __instance, ref string __result)
        {
            // This seems to do the job just fine with no side effects
            __result = __result + "_" + __instance.ID;
        }
        #endregion
    }
}
