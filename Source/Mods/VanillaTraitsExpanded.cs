using System.Collections;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Vanilla Traits Expanded by Oskar Potocki, Taranchuk, Chowder</summary>
    /// <see href="https://github.com/Vanilla-Expanded/VanillaTraitsExpanded"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2296404655"/>
    [MpCompatFor("VanillaExpanded.VanillaTraitsExpanded")]
    public class VanillaTraitsExpanded
    {
        private static AccessTools.FieldRef<IDictionary> mapPawnsAnimalListField;
        
        public VanillaTraitsExpanded(ModContentPack mod)
        {
            // Stop doing stuff with absent minded/rebel/submissive pawns outside of synced context
            {
                // Basically, this method adds/removes slow work speed hediff to/from rebel/submissive pawns.
                // On top of that, it also adds the current job to the list of forced jobs for absent minded pawns.
                // This can cause issues when the mod operates and does stuff based on those, but they are in different state for all players.
                // The issue is that a sync method catches this call, but all prefixes/postfixes/etc. still run.
                MpCompat.harmony.Patch(AccessTools.DeclaredMethod("VanillaTraitsExpanded.TryTakeOrderedJob_Patch:Postfix"),
                    prefix: new HarmonyMethod(typeof(VanillaTraitsExpanded), nameof(CancelUnlessSynced)));
            }

            // Clear cache
            {
                // List of animals on current map for menagerist trait
                var field = AccessTools.DeclaredField("VanillaTraitsExpanded.ThoughtWorker_AnimalsInColony:mapPawns");
                mapPawnsAnimalListField = AccessTools.StaticFieldRefAccess<IDictionary>(field);

                MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(GameComponentUtility), nameof(GameComponentUtility.FinalizeInit)),
                    postfix: new HarmonyMethod(typeof(VanillaTraitsExpanded), nameof(ClearCache)));
            }
        }

        private static bool CancelUnlessSynced() => !MP.IsInMultiplayer || MP.IsExecutingSyncCommand;

        private static void ClearCache() => mapPawnsAnimalListField().Clear();
    }
}