using HarmonyLib;
using Verse;

namespace Multiplayer.Compat
{
    [MpCompatFor("roolo.whatthehack")]
    class WhatTheHack
    {
        public WhatTheHack(ModContentPack mod)
        {
            var methods = new[]
            {
                AccessTools.Method(AccessTools.TypeByName("WhatTheHack.Harmony.IncidentWorker_Raid_TryExecuteWorker"), "SpawnHackedMechanoids"),
                AccessTools.Method(AccessTools.TypeByName("WhatTheHack.Harmony.Pawn_JobTracker_DetermineNextJob"), "HackedPoorlyEvent"),
                AccessTools.Method(AccessTools.TypeByName("WhatTheHack.Harmony.Thing_ButcherProducts"), "GenerateExtraButcherProducts"),
                AccessTools.Method(AccessTools.TypeByName("WhatTheHack.Needs.Need_Maintenance"), "MaybeUnhackMechanoid"),
                AccessTools.Method(AccessTools.TypeByName("WhatTheHack.Recipes.Recipe_Hacking"), "CheckHackingFail"),
            };

            PatchingUtilities.PatchSystemRand(methods, false);
        }
    }
}
