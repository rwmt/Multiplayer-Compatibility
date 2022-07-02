using HarmonyLib;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Smarter Construction by Hultis</summary>
    /// <see href="https://github.com/dhultgren/rimworld-smarter-construction"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2202185773"/>
    [MpCompatFor("dhultgren.smarterconstruction")]
    class SmarterConstruction
    {
        public SmarterConstruction(ModContentPack mod)
        {
            var type = AccessTools.TypeByName("SmarterConstruction.Patches.Patch_WorkGiver_Scanner_GetPriority");
            var field = AccessTools.Field(type, "random");

            field.SetValue(null, PatchingUtilities.RandRedirector.Instance);
        }
    }
}
