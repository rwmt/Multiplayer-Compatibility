using System;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Pocket Sand by Reisen</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2226330302"/>
    [MpCompatFor("usagirei.pocketsand")]
    public class PocketSandCompat
    {
        public PocketSandCompat(ModContentPack mod)
        {
            LongEventHandler.ExecuteWhenFinished(LateLoad);
        }

        static void LateLoad()
        {
            Type type = AccessTools.TypeByName("PocketSand.PawnExtensions");

            MP.RegisterSyncMethod(type, "EquipFromInventory");
            MP.RegisterSyncMethod(type, "DropFromInventory");
        }
    }
}