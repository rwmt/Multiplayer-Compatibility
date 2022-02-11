using System;
using HarmonyLib;
using Multiplayer.API;
using Verse;
using RimWorld;

namespace Multiplayer.Compat
{
    /// <summary>RPG Style Inventory by Sandy</summary>
    /// <see href="https://github.com/SandyTheGreat/RPG-Style-Inventory"/>
    /// <see href="https://github.com/catgirlfighter/-1.0-RPG-Style-Inventory-V3.0-"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=1561221991"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2478833213"/>
    [MpCompatFor("Sandy.RPGStyleInventory")]
    [MpCompatFor("Nykot.RPGStyleInventory")]
    [MpCompatFor("Sandy.RPGStyleInventory.avilmask.Revamped")]
    class RPGStyleInventory
    {
        public RPGStyleInventory(ModContentPack mod)
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
