using System.Collections;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Hospitality by Orion</summary>
    /// <see href="https://github.com/OrionFive/Hospitality"/>
    /// <see href="https://steamcommunity.com/workshop/filedetails/?id=753498552"/>
    [MpCompatFor("Orion.Hospitality")]
    public class Hospitality
    {
        // CompUtility
        private static AccessTools.FieldRef<IDictionary> guestCompsField;
        // RelationUtility
        private static AccessTools.FieldRef<IDictionary> relationCacheField;
        // GenericUtility
        private static AccessTools.FieldRef<IDictionary> travelDaysCacheField;
        // GuestUtility
        private static AccessTools.FieldRef<IDictionary> relatedCacheField;
        private static AccessTools.FieldRef<int> relatedCacheNextClearTickField;

        public Hospitality(ModContentPack mod)
        {
            // TODO: Check Hospitality.Patches.MinifiedThing_Patch.DrawAt:Prefix, specifically the HospitalityModBase.RegisterTickAction call

            LongEventHandler.ExecuteWhenFinished(LatePatch);

            // Alerts
            {
                MpCompat.harmony.Patch(AccessTools.DeclaredMethod("Hospitality.Alert_Guest:GetReport"),
                    prefix: new HarmonyMethod(typeof(Hospitality), nameof(NoAlertInMp)));
            }

            // Gizmos
            {
                MP.RegisterSyncMethod(AccessTools.DeclaredMethod("Hospitality.CompVendingMachine:SetEmptyThreshold"));
            }
        }

        private static void LatePatch()
        {
            // Cache
            {
                var type = AccessTools.TypeByName("Hospitality.CompUtility");
                guestCompsField = AccessTools.StaticFieldRefAccess<IDictionary>(AccessTools.DeclaredField(type, "guestComps"));

                type = AccessTools.TypeByName("Hospitality.RelationUtility");
                relationCacheField = AccessTools.StaticFieldRefAccess<IDictionary>(AccessTools.DeclaredField(type, "relationCache"));

                type = AccessTools.TypeByName("Hospitality.Utilities.GenericUtility");
                travelDaysCacheField = AccessTools.StaticFieldRefAccess<IDictionary>(AccessTools.DeclaredField(type, "travelDaysCache"));

                type = AccessTools.TypeByName("Hospitality.Utilities.GuestUtility");
                relatedCacheField = AccessTools.StaticFieldRefAccess<IDictionary>(AccessTools.DeclaredField(type, "relatedCache"));
                relatedCacheNextClearTickField = AccessTools.StaticFieldRefAccess<int>(AccessTools.DeclaredField(type, "relatedCacheNextClearTick"));

                MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(GameComponentUtility), nameof(GameComponentUtility.FinalizeInit)),
                    postfix: new HarmonyMethod(typeof(Hospitality), nameof(ClearCache)));
            }
        }

        private static bool NoAlertInMp(ref AlertReport __result)
        {
            if (!MP.IsInMultiplayer)
                return true;

            __result = AlertReport.Inactive;
            return false;
        }

        private static void ClearCache()
        {
            // CompUtility
            guestCompsField().Clear();

            // RelationUtility
            relationCacheField().Clear();

            // GenericUtility
            // This one probably doesn't need clearing, but it will just keep on filling up with
            // more data each time someone desyncs, or a game is loaded, etc.
            travelDaysCacheField().Clear();

            // GuestUtility
            relatedCacheField().Clear();
            relatedCacheNextClearTickField() = 0;

            // Hospitality.Patches.ThingFilter_Patch.Allows:bedNameToThing doesn't look like it needs clearing
            // Hospitality.Utilities.ThoughtResultCache clears the caches on load (but not when hosting new game).
        }
    }
}