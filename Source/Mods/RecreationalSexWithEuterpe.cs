using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Intimacy - Socio Butterfly by turkler</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3630896210"/>
    [MpCompatFor("lovelydovey.recreation.witheuterpe")]
    public class RecreationalSexWithEuterpe
    {
        public RecreationalSexWithEuterpe(ModContentPack mod)
        {
            var type = AccessTools.TypeByName("RecreationalSexWithEuterpe.PawnColumnWorker_MealTime");
            MpCompat.RegisterLambdaDelegate(type, "GenerateDropdownElements", 0);

            type = AccessTools.TypeByName("RecreationalSexWithEuterpe.HediffComp_Birthday");
            MpCompat.RegisterLambdaMethod(type, "CompGetGizmos", 1);
        }
    }
}