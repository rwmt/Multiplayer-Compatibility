using RimWorld.Planet;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Almost There! by Roolo</summary>
    /// <see href="https://github.com/rheirman/AlmostThere"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2372543327"/>
    [MpCompatFor("roolo.AlmostThere")]
    [MpCompatFor("Chad.Almostthere1.5")]
    public class AlmostThere
    {
        public AlmostThere(ModContentPack mod)
        {
            // Toggle: almost there (1), never rest (3), force rest (5)
            MpCompat.RegisterLambdaMethod("AlmostThere.AlmostThereWorldObjectComp", nameof(WorldObjectComp.GetGizmos), 1, 3, 5);
        }
    }
}