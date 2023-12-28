using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Tacticowl by Roolo, Owlchemist</summary>
    /// <see href="https://github.com/Owlchemist/RunGunAndDestroy"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2936140288"/>
    // The workshop link will need updating once it goes out of beta, as it's going to get a new page while this one will be taken down
    [MpCompatFor("Owlchemist.Tacticowl")]
    public class Tacticowl
    {
        public Tacticowl(ModContentPack mod)
        {
            // May be worth keeping an eye on those patches while the mod is still in the beta, as stuff may break easily
            // Search and Destroy
            MpCompat.RegisterLambdaDelegate("Tacticowl.Patch_GetGizmos", "CreateGizmo_SearchAndDestroy_Melee", 1);
            MpCompat.RegisterLambdaDelegate("Tacticowl.Patch_GetGizmos", "CreateGizmo_SearchAndDestroy_Ranged", 1);

            // Run and Gun
            MpCompat.RegisterLambdaDelegate("Tacticowl.Patch_PawnGetGizmos", "Postfix", 1);
            PatchingUtilities.PatchSystemRand("Tacticowl.Patch_TryStartMentalState:Postfix", false);
        }
    }
}