using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Vanilla Plants Expanded - Auto Plow Patch by Archoran</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2497914485"/>
    [MpCompatFor("Archoran.Utils.VPEAutoPlow")]
    class VanillaPlantsAutoPlowPatch
    {
        public VanillaPlantsAutoPlowPatch(ModContentPack mod)
            => MP.RegisterSyncDelegateLambda(AccessTools.TypeByName("VPEAutoPlow.Patch_Zone_Growing_GetGizmos"), "Add_AllowAutoPlow_Gizmo", 1);
    }
}
