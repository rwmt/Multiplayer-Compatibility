using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Prison Commons by Ben Lubar</summary>
    /// <see href="https://git.lubar.me/rimworld-mods/prison-commons"/>
    /// <see href="https://steamcommunity.com/workshop/filedetails/?id=2630896782"/>
    [MpCompatFor("me.lubar.PrisonCommons")]
    [MpCompatFor("me.lubar.PrisonCommons.temp")]
    internal class PrisonCommons
    {
        public PrisonCommons(ModContentPack mod) 
            => LongEventHandler.ExecuteWhenFinished(LatePatch);

        private static void LatePatch()
            => MpCompat.RegisterLambdaMethod("RimWorld.CompPrisonCommons", "CompGetGizmosExtra", 1);
    }
}
