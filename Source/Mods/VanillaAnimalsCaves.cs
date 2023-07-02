using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Vanilla Animals Expanded - Caves by Oskar Potocki, Sarg Bjornson, Aquiles</summary>
    /// <see href="https://github.com/Vanilla-Expanded/VanillaAnimalsExpanded-Caves"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2576512001"/>
    [MpCompatFor("VanillaExpanded.VAECaves")]
    internal class VanillaAnimalsCaves
    {
        public VanillaAnimalsCaves(ModContentPack mod)
        {
            // RNG
            {
                var methods = new[]
                {
                    "VAECaves.Building_SpiderEggs:Tick",
                    "VAECaves.DamageWorker_TwoArmSlam:Apply",
                    "VAECaves.IncidentWorker_Hulk:CanFireNowSub",
                    "VAECaves.JobDriver_CreateEggs:<MakeNewToils>b__1_0",
                };

                PatchingUtilities.PatchSystemRand(methods, false);
                PatchingUtilities.PatchSystemRandCtor("VAECaves.Hediff_WallBreaker", false);
            }

            // Gizmos
            {
                MpCompat.RegisterLambdaMethod("VAECaves.Building_Cocoon", "GetGizmos", 0);
                MpCompat.RegisterLambdaMethod("VAECaves.CompConditionalSpawner", "CompGetGizmosExtra", 0).SetDebugOnly();
            }
        }
    }
}
