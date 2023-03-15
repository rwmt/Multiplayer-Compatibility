using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Alpha Mythology by Sarg Bjornson</summary>
    /// <see href="https://github.com/juanosarg/AlphaMythology"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=1821617793"/>
    [MpCompatFor("sarg.magicalmenagerie")]
    public class AlphaMythology
    {
        public AlphaMythology(ModContentPack mod) 
            => PatchingUtilities.PatchSystemRandCtor("AnimalBehaviours.DeathActionWorker_ExplodeAndSpawnEggs");
    }
}