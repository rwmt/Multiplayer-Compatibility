using HarmonyLib;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Windows by Owlchemist, jptrrs</summary>
    /// <see href="https://github.com/Owlchemist/OpenTheWindows"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2571189146"/>
    [MpCompatFor("Owlchemist.Windows")]
    internal class Windows
    {
        public Windows(ModContentPack mod)
        {
            var type = AccessTools.TypeByName("OpenTheWindows.Building_Window");
            MpCompat.RegisterLambdaMethod(type, "GetGizmos", 3, 5);

            type = AccessTools.TypeByName("OpenTheWindows.CompWindow");
            MpCompat.RegisterLambdaMethod(type, "CompGetGizmosExtra", 1);
        }
    }
}
