using System;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Reunion by Kyrun</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=1985186461"/>
    /// <see href="https://github.com/kyrun/rimworld-reunion"/>
    [MpCompatFor("Kyrun.Reunion")]
    public class Reunion
    {
        public Reunion(ModContentPack mod)
        {
            var methods = new[]
            {
                "Kyrun.Reunion.GameComponent:GetRandomAllyForSpawning",
                "Kyrun.Reunion.GameComponent:TryScheduleNextEvent",
                "Kyrun.Reunion.GameComponent:DecideAndDoEvent",
            };
            PatchingUtilities.PatchUnityRand(methods);
        }
    }
}