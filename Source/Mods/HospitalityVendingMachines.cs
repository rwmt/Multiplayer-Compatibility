using Verse;

namespace Multiplayer.Compat
{
    /// <summary>
    /// Hospitality: Vending machines by Adamas
    /// </summary>
    /// <see href="https://github.com/tomvd/VendingMachines"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3014885065"/>
    [MpCompatFor("Adamas.VendingMachines")]
    public class HospitalityVendingMachines
    {
        public HospitalityVendingMachines(ModContentPack mod)
            => HospitalityCasino.InitializeGizmos("VendingMachines");
    }
}