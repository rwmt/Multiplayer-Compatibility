using Verse;

namespace Multiplayer.Compat
{
    /// <summary>
    /// Harvest Organs Post Mortem by Smuffle
    /// </summary>
    /// <see href="https://github.com/DenJur/RimwoldAutopsy"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=1204502413"/>
    [MpCompatFor("Smuffle.HarvestOrgansPostMortem")]
    public class HarvestOrgansPostMortem
    {
        public HarvestOrgansPostMortem(ModContentPack mod)
            => PatchingUtilities.PatchSystemRand("Autopsy.NewMedicalRecipesUtility:TraverseBody");
    }
}