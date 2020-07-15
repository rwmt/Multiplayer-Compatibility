using System;
using HarmonyLib;
using Multiplayer.API;
using Verse;
using RimWorld;

namespace Multiplayer.Compat
{
    /// <summary>RPG Style Inventory by Sandy</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=1561221991"/>
    [MpCompatFor("Sandy.RPGStyleInventory")]
    class RPGStyleInventory
    {


        public RPGStyleInventory(ModContentPack mod)
        {
            MP.RegisterSyncWorker<ITab_Pawn_Gear> (SyncITab, AccessTools.TypeByName("Sandy_Detailed_RPG_Inventory.Sandy_Detailed_RPG_GearTab"));
            MP.RegisterSyncMethod(AccessTools.TypeByName("Sandy_Detailed_RPG_Inventory.Sandy_Detailed_RPG_GearTab"), "InterfaceDrop").SetContext(SyncContext.MapSelected);
            MP.RegisterSyncMethod(AccessTools.TypeByName("Sandy_Detailed_RPG_Inventory.Sandy_Detailed_RPG_GearTab"), "InterfaceIngest").SetContext(SyncContext.MapSelected);

        }

        private static void SyncITab(SyncWorker sync, ref ITab_Pawn_Gear gearITab)
        {
            if (sync.isWriting)
            {
                sync.Write(gearITab.GetType());
            }
            else
            {
                gearITab = (ITab_Pawn_Gear)InspectTabManager.GetSharedInstance(sync.Read<Type>());
            }

        }

        
    }
}
