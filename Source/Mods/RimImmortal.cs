using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    [MpCompatFor("RI.RimImmortal.Core")]
    public class RimImmortalCompat
    {
        public RimImmortalCompat(ModContentPack mod)
        {
            var type = AccessTools.TypeByName("WhoXiuXian.Abilities.CompAbilityEffect_ToggleHediff"); 
            MP.RegisterSyncDelegate(type, "<>c__DisplayClass8_0", "<GetGizmos>b__2");
        }
    }
}
