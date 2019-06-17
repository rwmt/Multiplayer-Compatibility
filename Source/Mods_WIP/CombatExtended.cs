using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

using Harmony;

using Multiplayer.API;

using RimWorld;
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
            Type type;
            {
                type = AccessTools.TypeByName("CombatExtended.Harmony.FloatMenuMakerMap_Modify_AddHumanlikeOrders");

                MP.RegisterSyncDelegate(type, "<>c__DisplayClass1_1", "<AddMenuItems>b__1");

                MP.RegisterSyncDelegate(type, "<>c__DisplayClass1_2", "<AddMenuItems>b__4");
                MP.RegisterSyncDelegate(type, "<>c__DisplayClass1_2", "<AddMenuItems>b__5");
                MP.RegisterSyncDelegate(type, "<>c__DisplayClass1_2", "<AddMenuItems>b__7");

                MP.RegisterSyncDelegate(type, "<>c__DisplayClass1_3", "<AddMenuItems>b__6");
            }
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
                MP.RegisterSyncMethod(type, "RemoveSlot");
                MP.RegisterSyncMethod(type, "RemoveSlot", new SyncType[] { typeof(int) });
            }
            // iTab Inventory?


        }
    }
}
