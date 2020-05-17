using HarmonyLib;
using Verse;

namespace Multiplayer.Compat.Mods
{
    /// <summary>Vanilla Events Expanded by Oskar Potocki, Helixien, Kikohi</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=1938420742"/>
    /// Contribution to Multiplayer Compatibility by Sokyran and Reshiram
    [MpCompatFor("VanillaExpanded.VEE")]
    class VEE
    {
        public VEE(ModContentPack mod)
        {
            // Methods with no System.Random calls, but calling methods that use it, or just using Verse.Rand
            var methodsForRngSync = new[]
            {
                "VEE.DummySpaceBattle:CreateRandomExplosion",
                "VEE.DummySpaceBattle:StartRandomFire",

                "VEE.RegularEvents.EarthQuake:TryExecuteWorker",
            };

            var methodsForAll = new[]
            {
                "VEE.DummySpaceBattle:Tick",
                "VEE.HeddifComp_MightJoin:CompPostTick",
                "VEE.Shuttle:Tick",
                
                // These 4 methods initialize System.Random, but don't use them in any way whatsoever.
                //"VEE.PurpleEvents.GlobalWarming:ChangeBiomes",
                //"VEE.PurpleEvents.GlobalWarming:ChangeTileTemp",
                //"VEE.PurpleEvents.IceAge:ChangeBiomes",
                //"VEE.PurpleEvents.IceAge:ChangeTileTemp",
                "VEE.PurpleEvents.PsychicBloom:Init",

                "VEE.RegularEvents.ApparelPod:TryExecuteWorker",
                "VEE.RegularEvents.CaravanAnimalWI:GenerateGroup",
                "VEE.RegularEvents.Drought:HarmPlant",
                "VEE.RegularEvents.HuntingParty:TryExecuteWorker",
                "VEE.RegularEvents.MeteoriteShower:TryExecuteWorker",
                //"VEE.RegularEvents.SpaceBattle:GameConditionTick", // This method was having issues with transpiling no matter what I did, skipping it until it can be fixed
                "VEE.RegularEvents.WeaponPod:TryExecuteWorker",
            };

            PatchingUtilities.PatchPushPopRand(methodsForRngSync);
            PatchingUtilities.PatchSystemRand(methodsForAll);
            // Only patch System.Random out, as this methods is only called by other ones
            MpCompat.harmony.Patch(AccessTools.Method("VEE.RegularEvents.EarthQuake:DamageInRadius"),
                transpiler: new HarmonyMethod(typeof(PatchingUtilities), nameof(PatchingUtilities.FixRNG)));
        }
    }
}
