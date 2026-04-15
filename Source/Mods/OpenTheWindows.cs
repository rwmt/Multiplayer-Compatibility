using HarmonyLib;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Windows by jptrrs</summary>
    /// <see href="https://github.com/jptrrs/OpenTheWindows"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=1971860929"/>
    [MpCompatFor("JPT.OpenTheWindows")]
    internal class Windows
    {
        public Windows(ModContentPack mod)
        {
            var type = AccessTools.TypeByName("OpenTheWindows.Building_Window");
            MpCompat.RegisterLambdaMethod(type, "GetGizmos", 1, 3, 5);

            type = AccessTools.TypeByName("OpenTheWindows.CompWindow");
            MpCompat.RegisterLambdaMethod(type, "CompGetGizmosExtra", 1);
        }
    }
}
