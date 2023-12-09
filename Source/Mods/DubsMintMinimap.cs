using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Dubs Mint Minimap by Dubwise</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=1662119905"/>
    [MpCompatFor("dubwise.dubsmintminimap")]
    internal class DubsMintMinimap
    {
        public DubsMintMinimap(ModContentPack mod) => LongEventHandler.ExecuteWhenFinished(() => DubsMintMenu.Patch("DubsMintMinimap.MP_Util"));
    }
}
