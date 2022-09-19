using System.Linq;
using HarmonyLib;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Anima Obelisk by DimonSever000</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2614248835"/>
    [MpCompatFor("DimonSever000.AnimaObelisk.Specific")]
    internal class AnimaObelisk
    {
        public AnimaObelisk(ModContentPack mod)
        {
            var type = AccessTools.TypeByName("PsyObelisk.Things.ThingComp_PsyObelisk");
            PatchingUtilities.PatchUnityRand(AccessTools.Method(type, "GlowAround"), false);
            var syncMethods = MpCompat.RegisterLambdaMethod(type, "CompGetGizmosExtra", Enumerable.Range(0, 8).ToArray()); // 0 to 7
            syncMethods.Skip(2).SetDebugOnly();
        }
    }
}
