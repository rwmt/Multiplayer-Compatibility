using Verse;

namespace Multiplayer.Compat
{
    /// <summary>
    /// Hospitality: Spa by Adamas
    /// </summary>
    /// <see href="https://github.com/tomvd/HospitalitySpa"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2971831654"/>
    [MpCompatFor("Adamas.HospitalitySpa")]
    public class HospitalitySpa
    {
        public HospitalitySpa(ModContentPack mod)
            => HospitalityCasino.InitializeGizmos("HospitalitySpa");
    }
}
