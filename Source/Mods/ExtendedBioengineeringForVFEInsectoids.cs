using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Extended Bioengineering for VFE Insectoids by Turnovus</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2706534548"/>
    [MpCompatFor("turnovus.submod.extendedbioengineering")]
    public class ExtendedBioengineeringForVFEInsectoids
    {
        public ExtendedBioengineeringForVFEInsectoids(ModContentPack mod)
        {
            // Gizmos
            {
                var type = AccessTools.TypeByName("extendedbioengineering.Building_Inducer");
                MP.RegisterSyncMethod(type, "TryFireInducerNow");
                MP.RegisterSyncMethod(type, "SetCountdownToNearZero").SetDebugOnly();

                // Unused - artificial hive is not included in the mod
                // MP.RegisterSyncMethod(AccessTools.Method("extendedbioengineering.Building_ArtificialHive:TryRequestNewCulture"));
            }

            // RNG
            {
                var type = AccessTools.TypeByName("extendedbioengineering.OutputWorker_Dissect");
                type = AccessTools.FirstInner(type, _ => true); // There should be only 1 inner class
                PatchingUtilities.PatchSystemRand(AccessTools.Method(type, "MoveNext"), false);

                type = AccessTools.TypeByName("extendedbioengineering.OutputWorker_Genes");
                type = AccessTools.FirstInner(type, _ => true); // There should be only 1 inner class
                PatchingUtilities.PatchSystemRand(AccessTools.Method(type, "MoveNext"), false);
            }

            LongEventHandler.ExecuteWhenFinished(LatePatch);
        }

        private static void LatePatch()
        {
            // Gizmos
            {
                var type = AccessTools.TypeByName("extendedbioengineering.ThingComp_JellySynthesizer");

                MP.RegisterSyncMethod(type, "TrySetNewSetting");
                MP.RegisterSyncMethod(AccessTools.PropertySetter(type, "TargetFuel"));
                MpCompat.RegisterLambdaMethod(type, "CompGetGizmosExtra", 1);
            }
        }
    }
}