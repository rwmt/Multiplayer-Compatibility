using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using Verse;
using RimWorld;
using Multiplayer.API;


namespace Multiplayer.Compat
{
    /// <summary>TD Enhancement Pack - Continued</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3525414162/"/>
    /// <see href="https://github.com/MemeGoddess/RimWorld-EnhancementPack"/>
    [MpCompatFor("memegoddess.tdpack")]
    class TD_Enhancement_Pack
    {
        public TD_Enhancement_Pack(ModContentPack mod)
        {
            /// Attempt to sync "Allow Harvest" Gizmo
            /// really just copied from other patches until it works X.x
            Type allowHarvest = AccessTools.TypeByName("TD_Enhancement_Pack.Zone_Growing_Extensions");
            MP.RegisterSyncMethod(allowHarvest, "ToggleHarvest");
            /// Add Building Gizmo sync
            Type allowHarvestBuilding = AccessTools.TypeByName("TD_Enhancement_Pack.Building_PlantGrower_Extensions");
            MP.RegisterSyncMethod(allowHarvestBuilding, "ToggleHarvest");
        }
    }
}
