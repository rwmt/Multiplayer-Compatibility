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
            var methodsForAll = new[]
            {
                "VEE.HediffComp_MightJoin:CompPostTick",

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

            LongEventHandler.ExecuteWhenFinished(LatePatch);
        }

        public static void LatePatch() => PatchingUtilities.PatchSystemRand("VEE.RegularEvents.SpaceBattle:GameConditionTick", false);
    }
}
