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

        // CompUtility
        private static FastInvokeHandler compGuestMethod;
        // Multiplayer
        private static AccessTools.FieldRef<ISyncField[]> syncFields;

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

            // Sync fields
            {
                // Hospitality starts watching the fields way too late (inside of PawnColumnWorker_Relationship.DoCell),
                // causing it to fail syncing in some situations. For example, shift-clicking on the header of make friends/entertain guests checkboxes.
                syncFields = AccessTools.StaticFieldRefAccess<ISyncField[]>(AccessTools.DeclaredField("Hospitality.Multiplayer:guestFields"));

                // Basically an extension method to pawn.GetComp<CompGuest>, but with extra caching and null.
                compGuestMethod = MethodInvoker.GetHandler(AccessTools.DeclaredMethod("Hospitality.CompUtility:CompGuest"));

                // Hospitality starts watching in this DoHeader is called before DoCell, so it should be good enough of a place to put it in.
                var type = AccessTools.TypeByName("Hospitality.MainTab.PawnColumnWorker_Relationship");

                MpCompat.harmony.Patch(AccessTools.DeclaredMethod(type, nameof(PawnColumnWorker.DoHeader)),
                    prefix: new HarmonyMethod(typeof(Hospitality), nameof(PreHeader)));
                MpCompat.harmony.Patch(AccessTools.DeclaredMethod(type, nameof(PawnColumnWorker.DoCell)),
                    prefix: new HarmonyMethod(typeof(Hospitality), nameof(PreCell)),
                    finalizer: new HarmonyMethod(typeof(Hospitality), nameof(PostCell)));
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

        // When making the PR to Hospitality, put it after MP WatchBegin in Hospitality.MainTab.MainTabWindow_Hospitality:DoWindowContents.
        // Also remember to remove the sync field watch from Hospitality.MainTab.PawnColumnWorker_Relationship:DoCell.
        private static void PreHeader(PawnTable table)
        {
            if (!MP.IsInMultiplayer)
                return;
            ref var fields = ref syncFields();
            if (fields == null)
                return;

            foreach (var pawn in table.PawnsListForReading)
            {
                var comp = compGuestMethod(null, pawn);

                if (comp != null)
                {
                    foreach (var field in fields)
                        field.Watch(comp);
                }
            }
        }

        // Stop watching sync fields for the second time, as it breaks syncing area syncing.
        private static void PreCell(ref ISyncField[] __state)
        {
            if (MP.IsInMultiplayer)
            {
                // Hospitality doesn't sync those if the field is null, so temporarily change it to null
                __state = syncFields();
                syncFields() = null;
            }
        }

        private static void PostCell(ISyncField[] __state)
        {
            // Restore data to the state it was beforehand
            if (__state != null)
                syncFields() = __state;
        }
    }
}