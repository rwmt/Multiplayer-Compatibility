using HarmonyLib;
using Multiplayer.API;
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

            PatchingUtilities.PatchSystemRand("VEE.RegularEvents.MeteoriteShower:TryExecuteWorker", false);
            PatchingUtilities.PatchPushPopRand("VEE.IncomingSmoker:ThrowBlackSmoke");

            // Current map usage, picks between rain and snow based on current map temperature, instead of using map it affects
            PatchingUtilities.ReplaceCurrentMapUsage("VEE.PurpleEvents.PsychicRain:ForcedWeather");

            // Reset game conditions - technically does not require debug mode,
            // but lets you end (almost?) any game condition at any time
            // so I'd consider it close enough to justify `SetDebugOnly` on it.
            MpCompat.RegisterLambdaDelegate("VEE.Settings.VEESettings", "ResetWorldCondButton", 0).SetDebugOnly();
            MpCompat.RegisterLambdaDelegate("VEE.Settings.VEESettings", "ResetMapCondButton", 0).SetDebugOnly();

            RegisterChoiceLetterLambdas("VEE.ChoiceLetter_AcceptCrashlanders");
            RegisterChoiceLetterLambdas("VEE.ChoiceLetter_WhiteoutRefugees");

            MpCompat.RegisterLambdaMethod("VEE.HediffComp_Traitor", "CompGetGizmos", 0).SetDebugOnly();
        }

        private static void RegisterChoiceLetterLambdas(string typeName)
        {
            var type = AccessTools.TypeByName(typeName);
            foreach (var method in MpMethodUtil.GetLambda(type, "Choices", MethodType.Getter, null, 0, 1))
                MP.RegisterSyncMethod(method);
        }
    }
}
