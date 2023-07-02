using System.Linq;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Hunt for Me by aRandomKiwi</summary>
    /// <see href="https://github.com/aRandomKiwi/Hunt-For-Me"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=1593245720"/>
    [MpCompatFor("aRandomKiwi.HuntForMe")]
    internal class HuntForMe
    {
        public HuntForMe(ModContentPack mod)
        {
            var type = AccessTools.TypeByName("aRandomKiwi.HFM.Pawn_MindState_Patch");
            type = AccessTools.Inner(type, "GetGizmos");
            MpCompat.RegisterLambdaDelegate(type, "Listener", 1, 3, 5, 6, 7, 8)
                .Take(3).ToArray().SetDebugOnly();

            var typeNames = new[]
            {
                "aRandomKiwi.HFM.PawnColumnWorker_Hunting",
                "aRandomKiwi.HFM.PawnColumnWorker_HuntingCanAssist",
                "aRandomKiwi.HFM.PawnColumnWorker_PreyModeHunting",
                "aRandomKiwi.HFM.PawnColumnWorker_SupervisedHunting",
            };

            foreach (var typeName in typeNames)
            {
                type = AccessTools.TypeByName(typeName);
                MP.RegisterSyncMethod(type, "SetValue");
            }
        }
    }
}
