using Verse;

namespace Multiplayer.Compat;

/// <summary>HugsLib by UnlimitedHugs</summary>
/// <see href="https://github.com/UnlimitedHugs/RimworldHugsLib"/>
/// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=818773962"/>
[MpCompatFor("UnlimitedHugs.HugsLib")]
public class HugsLib
{
    public HugsLib(ModContentPack mod)
    {
        // Stop the long event from running when it could break stuff.
        PatchingUtilities.CancelCallIfLongEventsAreUnsafe("HugsLib.HugsLibController:OnMapInitFinalized");
    }
}