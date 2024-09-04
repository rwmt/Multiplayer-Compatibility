using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat;

/// <summary>Vanilla Temperature Expanded by Oskar Potocki, xrushha, Arquebus, Taranchuk</summary>
/// <see href="https://github.com/Vanilla-Expanded/VanillaTemperatureExpanded"/>
/// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3202046258"/>
[MpCompatFor("VanillaExpanded.Temperature")]
public class VanillaTemperatureExpanded
{
    public VanillaTemperatureExpanded(ModContentPack mod)
    {
        LongEventHandler.ExecuteWhenFinished(LatePatch);

        // Unlink/relink
        MpCompat.RegisterLambdaDelegate("VanillaTemperatureExpanded.Comps.CompAcTempControl", nameof(ThingComp.CompGetGizmosExtra), 0);
    }

    private static void LatePatch()
    {
        var type = AccessTools.TypeByName("VanillaTemperatureExpanded.Buildings.Building_AcControlUnit");
        // Change temperature by +/- 1/10 
        MP.RegisterSyncMethod(type, "InterfaceChangeTargetNetworkTemperature");
        // Reset temperature
        MP.RegisterSyncMethodLambda(type, nameof(Thing.GetGizmos), 2);
    }
}