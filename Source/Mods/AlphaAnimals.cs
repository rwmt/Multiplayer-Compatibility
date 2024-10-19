using System.Reflection;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Alpha Animals by Sarg Bjornson</summary>
    /// <see href="https://github.com/juanosarg/AlphaAnimals"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=1541721856"/>
    /// contribution to Multiplayer Compatibility by Reshiram and Sokyran
    [MpCompatFor("sarg.alphaanimals")]
    class AlphaAnimals
    {
        #region Fields

        private static MethodBase unsafeMethod;

        #endregion

        #region Main patch

        public AlphaAnimals(ModContentPack mod)
        {
            LongEventHandler.ExecuteWhenFinished(LatePatch);

            #region MP unsafe method patching

            // Only apply if VFE-I2 is inactive.
            // Need to check for _steam due to RW bug when
            // running a workshop version while a local
            // copy of a mod is active.
            if (!ModsConfig.IsActive("OskarPotocki.VFE.Insectoid2") && !ModsConfig.IsActive("OskarPotocki.VFE.Insectoid2_steam"))
            {
                unsafeMethod = AccessTools.DeclaredMethod("AlphaBehavioursAndEvents.BlackCocoon:Tick");

                // Make sure the method actually exists
                if (unsafeMethod != null)
                    MpCompat.harmony.Patch(AccessTools.DeclaredMethod("Multiplayer.Client.Extensions:PatchMeasure"),
                        prefix: new HarmonyMethod(DontPatchUnsafeMethods));
            }

            #endregion
        }

        private static void LatePatch()
        {
            #region Gizmos

            {
                // Detonate.
                // Unused in 1.4 (code moved to Alpha Memes), so it could potentially
                // be removed in the future. Include a null method check.
                var method = AccessTools.DeclaredMethod($"AlphaBehavioursAndEvents.Pawn_Detonator:{nameof(Pawn.GetGizmos)}");
                if (method != null)
                    MP.RegisterSyncMethodLambda(method.DeclaringType, method.Name, 0);
            }

            #endregion
        }

        #endregion

        #region MP unsafe method patching

        private static bool DontPatchUnsafeMethods(MethodBase original)
        {
            // Multiplayer patches all ticking methods for any Thing
            // subtype. Alpha Animals uses methods from Vanilla
            // Factions Expanded - Insectoids 2, which (if that mod
            // is not loaded) will cause an exception when attempting
            // to patch that mod. We cannot patch VFE-I2 method
            // directly, as that is specifically the issue MP itself
            // is encountering, so we have to prevent MP from patching
            // that method to make sure that it can load correctly.
            return original != unsafeMethod;
        }

        #endregion
    }
}