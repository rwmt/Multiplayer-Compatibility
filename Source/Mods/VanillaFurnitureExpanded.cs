using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Vanilla Furniture Expanded by OskarPotocki, Atlas, Kikohi</summary>
    /// <see href="https://github.com/Vanilla-Expanded/VanillaFurnitureExpanded"/>
    /// <see href="https://steamcommunity.com/workshop/filedetails/?id=1718190143"/>
    [MpCompatFor("VanillaExpanded.VFECore")]
    internal class VanillaFurnitureExpanded
    {
        [MpCompatSyncField("VanillaFurnitureEC.CompBinClean", "cleanupTarget")]
        private static ISyncField cleanupTargetField = null;

        public VanillaFurnitureExpanded(ModContentPack mod)
        {
            MpCompatPatchLoader.LoadPatch(this);

            // Uses ShouldSpawnMotesAt, so could potentially cause issues somewhere. Seems like no RNG though... Let's better be safe here.
            PatchingUtilities.PatchPushPopRand("VanillaFurnitureEC.JobDriver_PlayDarts:ThrowDart");
        }

        [MpCompatPrefix("VanillaFurnitureEC.Command_CleanupTarget", "GizmoOnGUI")]
        private static void PreGizmoGui(ThingComp ___comp, out bool __state)
        {
            if (!MP.IsInMultiplayer || ___comp == null)
            {
                __state = false;
                return;
            }

            __state = true;
            MP.WatchBegin();
            cleanupTargetField.Watch(___comp);
        }

        [MpCompatPostfix("VanillaFurnitureEC.Command_CleanupTarget", "GizmoOnGUI")]
        private static void PostGizmoGui(bool __state)
        {
            if (__state)
                MP.WatchEnd();
        }
    }
}