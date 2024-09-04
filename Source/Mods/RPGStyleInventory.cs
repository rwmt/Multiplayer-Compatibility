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
        private static Type tabType;

        public RPGStyleInventory(ModContentPack mod)
        {
            tabType = AccessTools.TypeByName("Sandy_Detailed_RPG_Inventory.Sandy_Detailed_RPG_GearTab");

            MP.RegisterSyncWorker<ITab_Pawn_Gear>(SyncITab, tabType);

            // Original re-implemented InterfaceDrop/InterfaceIngest, but some stopped doing that.
            // Sync those methods if they're declared, but skip if they aren't since we don't want
            // to sync vanilla methods (which are already synced). On top of that, vanilla marked
            // InterfaceIngest as obsolete (use FoodUtility.IngestFromInventoryNow instead),
            // so avoid HugsLib warnings about patching obsolete methods.
            foreach (var methodName in new[] { "InterfaceDrop", "InterfaceIngest" })
            {
                var method = AccessTools.DeclaredMethod(tabType, methodName);
                if (method != null)
                    MP.RegisterSyncMethod(method).SetContext(SyncContext.MapSelected);
            }

            // Remove/add forced apparel
            if (mod.PackageId == "Sandy.RPGStyleInventory.avilmask.Revamped".ToLower()) {
                MpCompat.RegisterLambdaDelegate(tabType, "PopupMenu", 0, 1);
            }
        }

        private static void SyncITab(SyncWorker sync, ref ITab_Pawn_Gear gearITab)
        {
            if (!sync.isWriting)
                gearITab = Activator.CreateInstance(tabType) as ITab_Pawn_Gear;
        }
    }
}