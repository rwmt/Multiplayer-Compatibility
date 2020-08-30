using System;
using HarmonyLib;
using Multiplayer.API;
using Verse;
using RimWorld;

namespace Multiplayer.Compat
{
    /// <summary>This is a reupload of RPG Style Inventory by Nykot, the original RPG Style Inventory is made by Sandy</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2204419561"/>
    [MpCompatFor("Nykot.RPGStyleInventory")]
    class RPGStyleInventory_12
    {
        public RPGStyleInventory_12(ModContentPack mod)
        {
            Type type = AccessTools.TypeByName("Sandy_Detailed_RPG_Inventory.Sandy_Detailed_RPG_GearTab");
            
            MP.RegisterSyncWorker<ITab_Pawn_Gear>(SyncITab, type);
            MP.RegisterSyncMethod(type, "InterfaceDrop").SetContext(SyncContext.MapSelected);
            MP.RegisterSyncMethod(type, "InterfaceIngest").SetContext(SyncContext.MapSelected);
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
