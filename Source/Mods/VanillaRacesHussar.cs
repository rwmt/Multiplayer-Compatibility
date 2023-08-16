using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Vanilla Races Expanded - Hussar by Oskar Potocki, xrushha, Taranchuk, Sarg</summary>
    /// <see href="https://github.com/Vanilla-Expanded/VanillaRacesExpanded-Hussar"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2893586390"/>
    [MpCompatFor("vanillaracesexpanded.hussar")]
    public class VanillaRacesHussar
    {
        public VanillaRacesHussar(ModContentPack mod)
        {
            // Trigger queued mental break when undrafted, which will happen right after clicking but before it's synced.
            PatchingUtilities.PatchCancelInInterface("VREHussars.Pawn_DraftController_Drafted_Patch:Postfix");
        }
    }
}