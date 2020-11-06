using Verse;

namespace Multiplayer.Compat
{
    /// <summary>RimWorld - Witcher Monster Hunt by Oskar Potocki and Sarg Bjornson</summary>
    /// <see href="https://github.com/juanosarg/RimWorld---Witcher-Monster-Hunt"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2008529522"/>
    [MpCompatFor("sargoskar.witcherhunt")]
    class WitcherMonsterHunt
    {
        public WitcherMonsterHunt(ModContentPack mod)
        {
            var methods = new[]
            {
                "WMHAnimalBehaviours.DamageWorker_ExtraInfecter:ApplySpecialEffectsToPart",
                "WMHAnimalBehaviours.IncidentWorker_MonsterEncounter:TryExecuteWorker",
            };

            PatchingUtilities.PatchSystemRand(methods, false);
        }
    }
}
