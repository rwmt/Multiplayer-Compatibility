using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Medicines+ by Atlas, TedDraws</summary>
    /// <see href="https://github.com/Atla55/Medicines-Plus"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2558272601"/>
    [MpCompatFor("TedDraws.MedicinesPlus.AT")]
    internal class MedicinesPlus
    {
        public MedicinesPlus(ModContentPack mod)
            => PatchingUtilities.PatchSystemRand("AT_MedicinesPlus.Hediff_HypnotolAddiction:PostRemoved", false);
    }
}
