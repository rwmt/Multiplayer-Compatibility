using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Dubs Paint Shop by Dubwise</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=1579516669"/>
    [MpCompatFor("dubwise.dubspaintshop")]
    internal class DubsPaintShop
    {
        public DubsPaintShop(ModContentPack mod) => LongEventHandler.ExecuteWhenFinished(() => DubsMintMenu.Patch("DubRoss.MP_Util"));  
    }
}
