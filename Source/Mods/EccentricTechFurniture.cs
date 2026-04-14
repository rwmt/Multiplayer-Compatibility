using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using Verse;
using System.Linq;
using System.Reflection;
using System;

namespace Multiplayer.Compat.Mods
{
    /// <summary>EccentricTech.Furniture by Aelanna</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3118185542"/>
    [MpCompatFor("Aelanna.EccentricTech.Furniture")]
    internal class EccentricTechFurniture
    // 
    {

        static Type typeCompFabricator;
        //static ISyncField repeatMode;
        static ISyncField targetCount;
        static ISyncField pushProductsToNetwork;
        public EccentricTechFurniture(ModContentPack mod)
        {
            typeCompFabricator = AccessTools.TypeByName("EccentricFurniture.CompFabricator");
            MP.RegisterSyncWorker<ThingComp>(SyncCompFabricator, typeCompFabricator);

            //repeatMode = MP.RegisterSyncField(typeCompFabricator, "repeatMode");
            targetCount = MP.RegisterSyncField(typeCompFabricator, "targetCount");
            pushProductsToNetwork = MP.RegisterSyncField(typeCompFabricator, "pushProductsToNetwork");

            var type = AccessTools.TypeByName("EccentricFurniture.ITab_Fabricator");
            MpCompat.harmony.Patch(AccessTools.Method(type, "FillTab"),
                prefix: new HarmonyMethod(PreFillTab),
                postfix: new HarmonyMethod(PostFillTab)
                );

            MP.RegisterSyncMethod(typeCompFabricator, "SetActiveRecipe");
            MpCompat.RegisterLambdaDelegate(type, "DoRepeatModeFloatMenu", 0, 1, 2);
            MpCompat.RegisterLambdaDelegate(type, "DoConfirmCancelDialog", 0);

            // Building_CeilingLight gizmos
            var ceilingLightType = AccessTools.TypeByName("EccentricFurniture.Building_CeilingLight");
            MpCompat.RegisterLambdaMethod(ceilingLightType, "GetGizmos", 1);        // toggle alwaysDrawOpaque
            MpCompat.RegisterLambdaMethod(ceilingLightType, "DoAdjustHeightDialog", 1); // set heightOffset

            // Building_SmartTable gizmos
            var smartTableType = AccessTools.TypeByName("EccentricFurniture.Building_SmartTable");
            MpCompat.RegisterLambdaMethod(smartTableType, "GetGizmos", 1);          // toggle isForcedOn

            // Building_StorageWithStackLimit gizmos
            var storageType = AccessTools.TypeByName("EccentricFurniture.Building_StorageWithStackLimit");
            MpCompat.RegisterLambdaMethod(storageType, "CreateCachedGizmos", 2);    // set maxItemStacks
        }


        private static void SyncCompFabricator(SyncWorker sync, ref ThingComp comp)
        {
            if(sync.isWriting)
            {
                sync.Write(comp.parent);
            } else
            {
                foreach (var _comp in sync.Read<ThingWithComps>().AllComps)
                    if (_comp.GetType() == typeCompFabricator)
                        comp = _comp;
            }
        }

        private static void PreFillTab(ITab __instance)
        {
            if (!MP.IsInMultiplayer)
            {
                return;
            }
            ThingWithComps selthing = __instance.SelThing as ThingWithComps;

            ThingComp comp = null;
            foreach (var _comp in selthing.AllComps)
                if (_comp.GetType() == typeCompFabricator)
                    comp = _comp;

            MP.WatchBegin();
            //repeatMode.Watch(comp);
            targetCount.Watch(comp);
            pushProductsToNetwork.Watch(comp);
        }
        private static void PostFillTab()
        {
            if (!MP.IsInMultiplayer)
                return;

            MP.WatchEnd();
        }
    }
}
