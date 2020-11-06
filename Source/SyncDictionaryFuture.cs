using System.Reflection;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Multiplayer.Compat
{
    static class SyncDictionaryFuture
    {
        // LandInSpecificCell
        private static FieldInfo mapParentField;
        private static FieldInfo cellField;
        private static FieldInfo landInShuttleField;
        // FormCaravan
        private static FieldInfo arrivalMessageKeyField;
        // AttackSettlement
        private static FieldInfo attackSettlementField;
        private static FieldInfo attackSettlementArrivalModeField;
        // GiveGift
        private static FieldInfo giveGiftSettlementField;
        // VisitSettlement
        private static FieldInfo visitSettlementField;
        // VisitSite
        private static FieldInfo siteField;
        private static FieldInfo visitSiteArrivalModeField;

        internal static void RegisterSyncWorkers()
        {
            MP.RegisterSyncWorker<Color>(SyncColor);

            var type = typeof(TransportPodsArrivalAction_LandInSpecificCell);
            mapParentField = AccessTools.Field(type, "mapParent");
            cellField = AccessTools.Field(type, "cell");
            landInShuttleField = AccessTools.Field(type, "landInShuttle");
            MP.RegisterSyncWorker<TransportPodsArrivalAction_LandInSpecificCell>(SyncLandInSpecificCell, type);

            type = typeof(TransportPodsArrivalAction_FormCaravan);
            arrivalMessageKeyField = AccessTools.Field(type, "arrivalMessageKey");
            MP.RegisterSyncWorker<TransportPodsArrivalAction_FormCaravan>(SyncFormCaravan, type);

            type = typeof(TransportPodsArrivalAction_AttackSettlement);
            attackSettlementField = AccessTools.Field(type, "settlement");
            attackSettlementArrivalModeField = AccessTools.Field(type, "arrivalMode");
            MP.RegisterSyncWorker<TransportPodsArrivalAction_AttackSettlement>(SyncAttackSettlement, type);

            type = typeof(TransportPodsArrivalAction_GiveGift);
            giveGiftSettlementField = AccessTools.Field(type, "settlement");
            MP.RegisterSyncWorker<TransportPodsArrivalAction_GiveGift>(SyncGiveGifts, type);

            type = typeof(TransportPodsArrivalAction_VisitSettlement);
            visitSettlementField = AccessTools.Field(type, "settlement");
            MP.RegisterSyncWorker<TransportPodsArrivalAction_VisitSettlement>(SyncVisitSettlement, type);

            type = typeof(TransportPodsArrivalAction_VisitSite);
            siteField = AccessTools.Field(type, "site");
            visitSiteArrivalModeField = AccessTools.Field(type, "arrivalMode");
            MP.RegisterSyncWorker<TransportPodsArrivalAction_VisitSite>(SyncVisitSite, type);
        }

        private static void SyncColor(SyncWorker sync, ref Color color)
        {
            sync.Bind(ref color.r);
            sync.Bind(ref color.g);
            sync.Bind(ref color.b);
            sync.Bind(ref color.a);
        }

        private static void SyncLandInSpecificCell(SyncWorker sync, ref TransportPodsArrivalAction_LandInSpecificCell transportPodAction)
        {
            if (sync.isWriting)
            {
                sync.Write((MapParent)mapParentField.GetValue(transportPodAction));
                sync.Write((IntVec3)cellField.GetValue(transportPodAction));
                sync.Write((bool)landInShuttleField.GetValue(transportPodAction));
            }
            else
                transportPodAction = new TransportPodsArrivalAction_LandInSpecificCell(sync.Read<MapParent>(), sync.Read<IntVec3>(), sync.Read<bool>());
        }

        private static void SyncFormCaravan(SyncWorker sync, ref TransportPodsArrivalAction_FormCaravan transportPodAction)
        {
            if (sync.isWriting)
                sync.Write((string)arrivalMessageKeyField.GetValue(transportPodAction));
            else
                transportPodAction = new TransportPodsArrivalAction_FormCaravan(sync.Read<string>());
        }

        private static void SyncAttackSettlement(SyncWorker sync, ref TransportPodsArrivalAction_AttackSettlement transportPodAction)
        {
            if (sync.isWriting)
            {
                sync.Write((Settlement)attackSettlementField.GetValue(transportPodAction));
                sync.Write((PawnsArrivalModeDef)attackSettlementArrivalModeField.GetValue(transportPodAction));
            }
            else
                transportPodAction = new TransportPodsArrivalAction_AttackSettlement(sync.Read<Settlement>(), sync.Read<PawnsArrivalModeDef>());
        }

        private static void SyncGiveGifts(SyncWorker sync, ref TransportPodsArrivalAction_GiveGift transportPodAction)
        {
            if (sync.isWriting)
                sync.Write((Settlement)giveGiftSettlementField.GetValue(transportPodAction));
            else
                transportPodAction = new TransportPodsArrivalAction_GiveGift(sync.Read<Settlement>());
        }

        private static void SyncVisitSettlement(SyncWorker sync, ref TransportPodsArrivalAction_VisitSettlement transportPodAction)
        {
            if (sync.isWriting)
                sync.Write((Settlement)visitSettlementField.GetValue(transportPodAction));
            else
                transportPodAction = new TransportPodsArrivalAction_VisitSettlement(sync.Read<Settlement>());
        }

        private static void SyncVisitSite(SyncWorker sync, ref TransportPodsArrivalAction_VisitSite transportPodAction)
        {
            if (sync.isWriting)
            {
                sync.Write((Site)siteField.GetValue(transportPodAction));
                sync.Write((PawnsArrivalModeDef)visitSiteArrivalModeField.GetValue(transportPodAction));
            }
            else
                transportPodAction = new TransportPodsArrivalAction_VisitSite(sync.Read<Site>(), sync.Read<PawnsArrivalModeDef>());
        }
    }
}
