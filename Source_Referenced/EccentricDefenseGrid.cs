using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using Verse;
using EccentricDefenseGrid;
using System.Linq;
using Steamworks;
using System;

namespace Multiplayer.Compat
{
    /// <summary>EccentricTech.DefenseGrid by Aelanna</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3066838686"/>
    [MpCompatFor("Aelanna.EccentricTech.DefenseGrid")]
    internal class EccentricTechDefenseGrid
    // 
    {
        private static ISyncField autoReload;
        private static ISyncField slots;
        //private static SyncType ordnanceSlot;
        private static Type compArtilleryMissileLauncherType;
        private static CompArtilleryMissileLauncher parentCompArtilleryMissileLauncher;
        public EccentricTechDefenseGrid(ModContentPack mod)
        {
            // RNG
            {
            }

            // Gizmos
            {
                var type = AccessTools.TypeByName("EccentricDefenseGrid.CompArtilleryDesignator");
                MP.RegisterSyncMethod(type, "NextDesignationMode");
                MP.RegisterSyncMethod(type, "SetDesignationMode");
                MP.RegisterSyncMethod(type, "SetOrdnanceIndex");
                // This delegate set field `ordnanceDef` & call `DoArtilleryDesignation`
                MpCompat.RegisterLambdaDelegate(type, "DoArtilleryTargeting", 0);

                type = AccessTools.TypeByName("EccentricDefenseGrid.DefenseGridNetwork");
                MP.RegisterSyncMethod(type, "NextDesignationMode");
                MP.RegisterSyncMethod(type, "SetDesignationMode");
                MP.RegisterSyncMethod(type, "SetSelectedOrdnance");
                MpCompat.RegisterLambdaDelegate(type, "DoArtilleryTargeting", 0);

                MP.RegisterSyncWorker<object>(SyncDefenseGridNetwork, type);
                type = AccessTools.TypeByName("EccentricDefenseGrid.OrdnanceCount");
                MP.RegisterSyncWorker<object>(SyncOrdnanceCount, type, shouldConstruct: true);



                type = AccessTools.TypeByName("EccentricDefenseGrid.DefenseGridMapComponent");

                MP.RegisterSyncMethod(type, "RecountOrdnance");

                type = compArtilleryMissileLauncherType = AccessTools.TypeByName("EccentricDefenseGrid.CompArtilleryMissileLauncher");
                MpCompat.RegisterLambdaMethod(type, "CompGetGizmosExtra", 0, 1);


                type = AccessTools.TypeByName("EccentricDefenseGrid.CompDefenseProjector");
                //called from gizmo
                MP.RegisterSyncMethod(type, "ApplyRadius").SetContext(SyncContext.MapSelected);
                MP.RegisterSyncMethod(type, "ApplyColor").SetContext(SyncContext.MapSelected);
                MpCompat.RegisterLambdaMethod(type, "CompGetGizmosExtra", 1).SetDebugOnly();

                type = AccessTools.TypeByName("EccentricDefenseGrid.CompDefenseGenerator");
                MpCompat.RegisterLambdaMethod(type, "CompGetGizmosExtra", 2, 4, 6, 8, 10, 11, 12).TakeLast(2).SetDebugOnly();

                type = AccessTools.TypeByName("EccentricDefenseGrid.CompDefenseHeatsink");
                MpCompat.RegisterLambdaMethod(type, "CompGetGizmosExtra", 0, 1, 2).SetDebugOnly();


            }
            {
                var type = AccessTools.TypeByName("EccentricDefenseGrid.OrdnanceSlot");
                //ordnanceSlot.expose = true;

                MP.RegisterSyncWorker<OrdnanceSlot>(SyncOrdnanceSlot, type);
            }
            // ITab
            {

                var type = AccessTools.TypeByName("EccentricDefenseGrid.ITab_OrdnanceStorage");


                MpCompat.harmony.Patch(AccessTools.Method(type, "FillTab"),
                prefix: new HarmonyMethod(typeof(EccentricTechDefenseGrid), nameof(PreFillTab)),
                postfix: new HarmonyMethod(typeof(EccentricTechDefenseGrid), nameof(PostFillTab)));


                MpCompat.RegisterLambdaDelegate(type, "DrawOrdnance_GenerateMenu", 0, 1).SetContext(SyncContext.MapSelected);



                type = AccessTools.TypeByName("EccentricDefenseGrid.OrdnanceSlot");
                autoReload = MP.RegisterSyncField(type, "autoReload");
                type = AccessTools.TypeByName("EccentricDefenseGrid.CompArtilleryMissileLauncher");
                slots = MP.RegisterSyncField(type, "slots");

                MP.RegisterSyncWorker<CompArtilleryMissileLauncher>(SyncCompArtilleryMissileLauncher, type);
            }

        }

        private static void PreFillTab(ITab __instance)
        {
            if (!MP.IsInMultiplayer)
            {
                return;
            }
            Building building = __instance.SelThing as Building;
            parentCompArtilleryMissileLauncher = building.GetComp<CompArtilleryMissileLauncher>();

            MP.WatchBegin();
            //slots.Watch(parentCompArtilleryMissileLauncher);
            parentCompArtilleryMissileLauncher.slots.ForEach((slot) => autoReload.Watch(slot));
        }
        private static void PostFillTab()
        {
            if (!MP.IsInMultiplayer)
                return;
            parentCompArtilleryMissileLauncher = null;
            MP.WatchEnd();
        }
        private static void SyncOrdnanceSlot(SyncWorker sync, ref OrdnanceSlot obj)
        {
            if (sync.isWriting)
            {
                sync.Write<bool>(obj is null);
                if (obj is null)
                    return;


            }
            else
            {
                // is null
                // does exist as method `DrawOrdnance_GenerateMenu` could be called with this param null
                if (sync.Read<bool>())
                {
                    obj = null;
                    return;
                }
            }

            CompArtilleryMissileLauncher compArtilleryMissileLauncher = Find.Selector.SingleSelectedThing?.TryGetComp<CompArtilleryMissileLauncher>();
            if (compArtilleryMissileLauncher != null)
            {
                if (sync.isWriting)
                {
                    sync.Write<int>(compArtilleryMissileLauncher.slots.IndexOf(obj));
                    return;
                }
                else
                {
                    int index = sync.Read<int>();
                    if (index != -1)
                    {
                        obj = compArtilleryMissileLauncher.slots[index];
                        return;

                    }
                    else
                    {
                        // to normal
                    }
                }

            }

            // normally copied by exposeFields
            //sync.Bind(ref obj, ordnanceSlot);
            if (sync.isWriting)
            {

                sync.Write<bool>(obj.autoReload);
                sync.Write<ThingWithComps>(obj.ordnance as ThingWithComps);
                sync.Write<string>(obj.def.defName);
            }
            else
            {


                obj.autoReload = sync.Read<bool>();
                obj.ordnance = sync.Read<ThingWithComps>() as Ordnance;
                obj.def = DefDatabase<OrdnanceDef>.GetNamed(sync.Read<string>());

            }
        }
        private static void SyncOrdnanceCount(SyncWorker sync, ref object ordnanceCount)
        {
            OrdnanceCount _oc = ordnanceCount as OrdnanceCount;
            if (sync.isWriting)
            {
                sync.Write(_oc.def.defName);
                sync.Write(_oc.count);
            }
            else
            {
                _oc.def = DefDatabase<OrdnanceDef>.GetNamed(sync.Read<string>());
                _oc.count = sync.Read<int>();
            }
        }
        private static void SyncDefenseGridNetwork(SyncWorker sync, ref object network)
        {
            DefenseGridNetwork _net = network as DefenseGridNetwork;
            if (sync.isWriting)
            {
                sync.Write(_net.mapComponent.map);
                sync.Write(_net.id);
            }
            else
            {
                Map map = sync.Read<Map>();
                int id = sync.Read<int>();
                network = map.GetComponent<DefenseGridMapComponent>().networks.Find((net) => net.id == id);
            }
        }

        private static void SyncCompArtilleryMissileLauncher(SyncWorker sync, ref CompArtilleryMissileLauncher compArtilleryMissileLauncher)
        {
            if (sync.isWriting)
            {
                sync.Write(compArtilleryMissileLauncher.parent as ThingWithComps);
            }
            else
            {
                compArtilleryMissileLauncher = sync.Read<ThingWithComps>().GetComp<CompArtilleryMissileLauncher>();
            }
        }

    }
}
