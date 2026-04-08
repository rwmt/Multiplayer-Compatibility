using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using Verse;
using System.Linq;

namespace Multiplayer.Compat.Mods
{
    /// <summary>EccentricTech.Flares by Aelanna</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2552628275"/>
    [MpCompatFor("Aelanna.EccentricTech.Flares2")]
    internal class EccentricTechFlares
    // 
    {
        public EccentricTechFlares(ModContentPack mod)
        {

            LongEventHandler.ExecuteWhenFinished(LatePatch);
        }

        void LatePatch()
        {

            // Verb
            {
                var type = AccessTools.TypeByName("EccentricFlares.CompIlluminatorPack");
                MP.RegisterSyncMethod(type, "DoStartThrow");
            }
            // Gizmo
            {
                var type = AccessTools.TypeByName("EccentricFlares.CompIlluminatorPack");
                MpCompat.RegisterLambdaMethod(type, "CompGetWornGizmosExtra", 1);
            }
        }
    }
}
