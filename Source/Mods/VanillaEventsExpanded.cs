using RimWorld;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Vanilla Events Expanded by Oskar Potocki, Helixien, Kikohi</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=1938420742"/>
    /// <see href="https://github.com/Vanilla-Expanded/VanillaEventsExpanded"/>
    /// Contribution to Multiplayer Compatibility by Sokyran and Reshiram
    [MpCompatFor("VanillaExpanded.VEE")]
    class VEE
    {
        public VEE(ModContentPack mod)
        {
            MpSyncWorkers.Requires<GameCondition>();

            var methodsForAll = new[]
            {
                "VEE.RegularEvents.ApparelPod:TryExecuteWorker",
                "VEE.RegularEvents.CaravanAnimalWI:GenerateGroup",
                "VEE.RegularEvents.MeteoriteShower:TryExecuteWorker",
                "VEE.RegularEvents.WeaponPod:TryExecuteWorker",
            };

            PatchingUtilities.PatchSystemRand(methodsForAll, false);
            // This method only calls other methods that use RNG calls
            PatchingUtilities.PatchPushPopRand("VEE.RegularEvents.EarthQuake:TryExecuteWorker");
            // Only patch System.Random out, as this methods is only called by other ones
            PatchingUtilities.PatchSystemRand("VEE.RegularEvents.EarthQuake:DamageInRadius", false);

            // Unity RNG
            PatchingUtilities.PatchUnityRand("VEE.Shuttle:Tick");

            // Current map usage, picks between rain and snow based on current map temperature, instead of using map it affects
            PatchingUtilities.ReplaceCurrentMapUsage("VEE.PurpleEvents.PsychicRain:ForcedWeather");

            // Reset game conditions - technically does not require debug mode,
            // but lets you end (almost?) any game condition at any time
            // so I'd consider it close enough to justify `SetDebugOnly` on it.
            MpCompat.RegisterLambdaDelegate("VEE.Settings.VEESettings", "ResetWorldCondButton", 0).SetDebugOnly();
            MpCompat.RegisterLambdaDelegate("VEE.Settings.VEESettings", "ResetMapCondButton", 0).SetDebugOnly();

            LongEventHandler.ExecuteWhenFinished(LatePatch);
        }

        public static void LatePatch() => PatchingUtilities.PatchSystemRand("VEE.RegularEvents.SpaceBattle:GameConditionTick", false);
    }
}
