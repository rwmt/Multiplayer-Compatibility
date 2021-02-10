using Verse;

namespace Multiplayer.Compat
{
    /// <summary>ExplosiveTrailsEffect.dll is used (at least) by Vanilal Furniture Expanded - Security and Vanilla Weapons Expanded</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=1814383360"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=1845154007"/>
    /// <see href="https://github.com/AndroidQuazar/VanillaFurnitureExpanded-Security"/>
    [MpCompatFor("VanillaExpanded.VWE")]
    [MpCompatFor("VanillaExpanded.VFESecurity")]
    public class ExplosiveTrailsEffect
    {
        private static bool isApplied = false;

        public ExplosiveTrailsEffect(ModContentPack mod)
        {
            if (isApplied) return;

            isApplied = true;

            var methodNames = new[]
            {
                "ExplosiveTrailsEffect.ExhaustFlames:ThrowRocketExhaustFlame",
                "ExplosiveTrailsEffect.SmokeThrowher:ThrowSmokeTrail",
            };

            PatchingUtilities.PatchPushPopRand(methodNames);
        }
    }
}
