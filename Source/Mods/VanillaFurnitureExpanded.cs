using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Vanilla Furniture Expanded by OskarPotocki, Atlas, Kikohi</summary>
    /// <see href="https://github.com/Atla55/More-Furniture-A-O"/>
    /// <see href="https://steamcommunity.com/workshop/filedetails/?id=1718190143"/>
    [MpCompatFor("VanillaExpanded.VFECore")]
    internal class VanillaFurnitureExpanded
    {
        public VanillaFurnitureExpanded(ModContentPack mod)
        {
            // Uses ShouldSpawnMotesAt, so could potentially cause issues somewhere. Seems like no RNG though... Let's better be safe here.
            PatchingUtilities.PatchPushPopRand("VanillaFurnitureEC.JobDriver_PlayDarts:ThrowDart");
        }
    }
}