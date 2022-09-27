using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Rim-Effect: Drell by Vanilla Expanded Team and Co.</summary>
    /// <see href="https://github.com/AndroidQuazar/RimEffect-Drell"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2651150217"/>
    [MpCompatFor("RimEffect.Drell")]
    public class RimEffectDrell
    {
        public RimEffectDrell(ModContentPack mod) 
            => MpCompat.RegisterLambdaMethod("REDrell.Comp_Drell", "CompGetGizmosExtra", 0, 1);
    }
}