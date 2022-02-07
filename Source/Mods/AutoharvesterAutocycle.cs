using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Autoharvester Auto-cycle by FluffyKittens</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2603143540"/>
    [MpCompatFor("FluffyKittens.Autoharvester")]
    internal class AutoharvesterAutocycle
    {
        public AutoharvesterAutocycle(ModContentPack mod) 
            => MpCompat.RegisterLambdaMethod("AutoHarvesterCycle.CycleComp", "CompGetGizmosExtra", 0, 1, 2, 3);
    }
}