using HarmonyLib;
using RimWorld;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Animals logic by Oblitus</summary>
    /// <see href="https://github.com/quicksilverfox/RimworldMods/tree/master/AnimalsLogic"/>
    /// <remarks>Beds are already watched, they just lack ownership. Adding it in the button desyncs. This is a better place.</remarks>
    [MpCompatFor("Oblitus.AnimalsLogic")]
    public class AnimalLogicCompat
    {
        public AnimalLogicCompat(ModContentPack mod)
        {
            MpCompat.harmony.Patch(
                AccessTools.Method(typeof(PawnComponentsUtility), nameof(PawnComponentsUtility.CreateInitialComponents)),
                postfix: new HarmonyMethod(typeof(AnimalLogicCompat), nameof(CreateInitialComponentsPostfix))
                );
        }

        static void CreateInitialComponentsPostfix(Pawn pawn)
        {
            if (pawn.RaceProps.Animal && pawn.ownership == null) {
                pawn.ownership = new Pawn_Ownership(pawn);
            }
        }
    }
}
