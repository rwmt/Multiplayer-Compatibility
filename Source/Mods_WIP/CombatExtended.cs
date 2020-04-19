using System;

using HarmonyLib;

using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Combat Extended by NoImageAvailable</summary>
    /// <remarks>Incomplete</remarks>
    /// <see href="https://steamcommunity.com/workshop/filedetails/?id=1631756268"/>
    /// <see href="https://github.com/NoImageAvailable/CombatExtended"/>
    //[MpCompatFor("Combat Extended")]
    public class CombatExtendedCompat
    {
        public CombatExtendedCompat(ModContentPack mod)
        {
            LongEventHandler.ExecuteWhenFinished(LateLoad);
        }

        static void LateLoad()
        {
            Type type;

            // Float Menus
            {
                type = AccessTools.TypeByName("CombatExtended.Harmony.FloatMenuMakerMap_Modify_AddHumanlikeOrders");

                // CE_Stabilizing
                MP.RegisterSyncDelegate(type, "<>c__DisplayClass1_1", "<AddMenuItems>b__1");

                // PickUp
                MP.RegisterSyncDelegate(type, "<>c__DisplayClass1_2", "<AddMenuItems>b__4");

                // PickUpAll
                MP.RegisterSyncDelegate(type, "<>c__DisplayClass1_2", "<AddMenuItems>b__5");

                // PickUpSome
                MP.RegisterSyncDelegate(type, "<>c__DisplayClass1_2", "<AddMenuItems>b__7");

                // Dialog_Slider
                MP.RegisterSyncDelegate(type, "<>c__DisplayClass1_3", "<AddMenuItems>b__6");
            }

            // Pawn Gizmos
            {
                // Reload
                type = AccessTools.TypeByName("CombatExtended.CompAmmoUser");

                MP.RegisterSyncMethod(type, "TryStartReload");
            }
            {
                // Toggles
                type = AccessTools.TypeByName("CombatExtended.CompFireModes");

                MP.RegisterSyncMethod(type, "ResetModes");
                MP.RegisterSyncMethod(type, "ToggleAimMode");
                MP.RegisterSyncMethod(type, "ToggleFireMode");
            }

            // Turret Gizmos
            {
                type = AccessTools.TypeByName("CombatExtended.Building_TurretGunCE");

                MP.RegisterSyncMethod(type, "<GetGizmos>b__64_0");
                MP.RegisterSyncMethod(type, "<GetGizmos>b__64_1");

                MP.RegisterSyncMethod(type, "TryOrderReload");
            }

            // Loadouts
            {
                type = AccessTools.TypeByName("CombatExtended.Utility_Loadouts");

                MP.RegisterSyncMethod(type, "SetLoadout");
                MP.RegisterSyncMethod(type, "SetLoadoutById");
                MP.RegisterSyncMethod(type, "GenerateLoadoutFromPawn");
            }
            {
                type = AccessTools.TypeByName("CombatExtended.Loadout");

                MP.RegisterSyncMethod(type, "Copy", new SyncType[] { type });
                MP.RegisterSyncMethod(type, "Copy");
                MP.RegisterSyncMethod(type, "AddSlot");
                MP.RegisterSyncMethod(type, "MoveSlot");
                MP.RegisterSyncMethod(type, "RemoveSlot", new SyncType[] { typeof(int) });
                MP.RegisterSyncMethod(type, "RemoveSlot", new SyncType[] { AccessTools.TypeByName("CombatExtended.LoadoutSlot") });
            }

            // iTab Inventory?
            {
                // TODO
            }

            // RNG prevention
            {
                var methods = new[] {
                    AccessTools.Method("CombatExtended.CompSuppressable:AddSuppression"),
                    AccessTools.Method("CombatExtended.CompSuppressable:CompTick"),    
                    // oh boy shooting doesn't work...
                    // TODO
                };

                foreach(var method in methods) {
                    MpCompat.harmony.Patch(method,
                        prefix: new HarmonyMethod(typeof(CombatExtendedCompat), nameof(FixRNGPre)),
                        postfix: new HarmonyMethod(typeof(CombatExtendedCompat), nameof(FixRNGPos))
                    );
                }
            }
        }

        static void FixRNGPre() => Rand.PushState();
        static void FixRNGPos() => Rand.PopState();
    }
}
