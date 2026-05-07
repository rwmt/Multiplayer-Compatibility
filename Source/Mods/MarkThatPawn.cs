using HarmonyLib;
using Multiplayer.API;
using System.Reflection;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Mark That Pawn by Mile</summary>
    /// <see href="https://github.com/emipa606/MarkThatPawn"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3056996662"/>
    [MpCompatFor("Mlie.MarkThatPawn")]
    public class MarkThatPawn
    {
        public MarkThatPawn(ModContentPack mod)
        {
            // Gizmos
            var type = AccessTools.TypeByName("MarkThatPawn.MarkThatPawn");
            MP.RegisterSyncDelegateLocalFunc(type, "GetMarkingOptions", "Action").SetContext(SyncContext.MapSelected);
            MP.RegisterSyncDelegateLocalFunc(type, "GetMarkingOptions", "action").SetContext(SyncContext.MapSelected);

        }
    }
}