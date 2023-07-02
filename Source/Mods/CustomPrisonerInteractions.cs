using System.Linq;
using HarmonyLib;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Custom Prisoner Interactions by Mlie</summary>
    /// <see href="https://github.com/emipa606/CustomPrisonerInteractions"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2841231775"/>
    [MpCompatFor("Mlie.CustomPrisonerInteractions")]
    public class CustomPrisonerInteractions
    {
        public CustomPrisonerInteractions(ModContentPack mod)
        {
            var type = AccessTools.TypeByName("CustomPrisonerInteractions.ITab_Pawn_Visitor_FillTab");
            MpCompat.RegisterLambdaDelegate(type, "Prefix", Enumerable.Range(0, 11).ToArray()); // 0 to 10
            MpCompat.RegisterLambdaDelegate(type, "Postfix", 0);
        }
    }
}