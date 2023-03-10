using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Clean Pathfinding 2 by Owlchemist</summary>
    /// <see href="https://github.com/Owlchemist/clean-pathfinding"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2603765747"/>
    [MpCompatFor("Owlchemist.CleanPathfinding")]
    public class CleanPathfinding2
    {
        public CleanPathfinding2(ModContentPack mod)
            // Switch door type gizmo
            => MP.RegisterSyncMethod(AccessTools.DeclaredMethod("CleanPathfinding.MapComponent_DoorPathing:SwitchDoorType"));
    }
}