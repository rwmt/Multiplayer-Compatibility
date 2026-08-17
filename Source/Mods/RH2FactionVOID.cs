using HarmonyLib;
using Verse;

namespace Multiplayer.Compat
{
    // Fixes ONLY ChickenCorpses.dll method MakeMotes
    // There can be more to repair, but only this caused desyncs in our game
    [MpCompatFor("RH2.Faction.VOID")]
    public class ChickenCorpsesCompat
    {
        public ChickenCorpsesCompat(ModContentPack mod)
        {
            var type = AccessTools.TypeByName("ChickenCorpses.CompDecayAfterDelay");
            PatchingUtilities.PatchPushPopRand(AccessTools.Method(type, "MakeMotes"));
        }
    }
}