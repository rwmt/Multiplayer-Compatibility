using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Vanilla Animals Expanded — Waste Animals by Oskar Potocki, Sarg Bjornson, Sir Van</summary>
    /// <see href="https://github.com/Vanilla-Expanded/VanillaAnimalsExpanded-WasteAnimals"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2962126499"/>
    [MpCompatFor("VanillaExpanded.VAEWaste")]
    public class VanillaAnimalsWaste
    {
        public VanillaAnimalsWaste(ModContentPack mod)
            => PatchingUtilities.PatchSystemRand(new[]
            {
                "VanillaAnimalsExpandedWaste.HediffComp_Toxflu:CompPostPostRemoved",
                "VanillaAnimalsExpandedWaste.VanillaAnimalsExpandedWaste_InteractionWorker_Interacted_Patch:Toxflu",
            }, false);
    }
}