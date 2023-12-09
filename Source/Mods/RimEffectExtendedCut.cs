using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Rim-Effect: Extended Cut by Vanilla Expanded Team and Co.</summary>
    /// <see href="https://github.com/Helixien/RimEffect-ExtendedCut"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2479492267"/>
    [MpCompatFor("RimEffect.ExtendedCut")]
    public class RimEffectExtendedCut
    {
        public RimEffectExtendedCut(ModContentPack mod)
        {
            MpCompat.RegisterLambdaDelegate("RimEffectExtendedCut.Building_WarzoneTable", "GetBattleSetOptions", 0);
            MpCompat.RegisterLambdaMethod("RimEffectExtendedCut.CompPowerOutDoorLamp", "CompGetGizmosExtra", 0, 1).SetDebugOnly();
        }
    }
}