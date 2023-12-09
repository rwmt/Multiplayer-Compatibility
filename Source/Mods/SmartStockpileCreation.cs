using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Quick Stockpile Creation by Slofa</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=1742151109"/>
    [MpCompatFor("longwater.smartstockpilecreation")]
    internal class SmartStockpileCreation
    {

        static Type designatorType;
        static FastInvokeHandler allowedThingsGetter;
        static AccessTools.FieldRef<object, SpecialThingFilterDef> specialThingFilterDefLabel;
        static AccessTools.FieldRef<object, StoragePriority> priorityLabel;
        static AccessTools.FieldRef<object, string> contentsLabel;

        static Type reallowGizmoType;
        static AccessTools.FieldRef<object, ThingDef> thingDefLabel;
        static AccessTools.FieldRef<object, Zone_Stockpile> zoneStockpileLabel;
        static AccessTools.FieldRef<object, ThingCategoryDef> categoryLabel;

        static Type disallowGizmoType;
        static AccessTools.FieldRef<object, Thing> thingLabel;

        public SmartStockpileCreation(ModContentPack mod) 
        {
            // Create Stockpile from Thing
            {
                designatorType = AccessTools.TypeByName("SmartStockpileCreation.RimObjs.SmartStockpile.SmartStockpileDesignator");
                
                allowedThingsGetter = MethodInvoker.GetHandler(AccessTools.PropertyGetter(designatorType, "AllowedThings"));
                specialThingFilterDefLabel = AccessTools.FieldRefAccess<SpecialThingFilterDef>(designatorType, "_specialThingFilterDef");
                priorityLabel = AccessTools.FieldRefAccess<StoragePriority>(designatorType, "_priority");
                contentsLabel = AccessTools.FieldRefAccess<string>(designatorType, "_stockpileContentsLabel");

                MP.RegisterSyncWorker<Designator_ZoneAddStockpile>(SmartStockpileDesignatorWorker, designatorType);
            }

            // Reallow Thing in Stockpile
            {
                reallowGizmoType = AccessTools.TypeByName("SmartStockpileCreation.RimObjs.DisallowInStockpile.ReallowInStockpileGizmo");

                thingDefLabel = AccessTools.FieldRefAccess<ThingDef>(reallowGizmoType, "_thingDef");
                zoneStockpileLabel = AccessTools.FieldRefAccess<Zone_Stockpile>(reallowGizmoType, "_zoneStockpile");
                categoryLabel = AccessTools.FieldRefAccess<ThingCategoryDef>(reallowGizmoType, "_category");

                MP.RegisterSyncMethod(AccessTools.Method(reallowGizmoType, "ProcessInput")).SetContext(SyncContext.MapSelected);
                MP.RegisterSyncWorker<Command>(ReallowInStockpileGizmoWorker, reallowGizmoType);
            }

            // Disallow Thing in Stockpile
            {
                disallowGizmoType = AccessTools.TypeByName("SmartStockpileCreation.RimObjs.DisallowInStockpile.RemoveFromStockpileGizmo");

                thingLabel = AccessTools.FieldRefAccess<Thing>(disallowGizmoType, "_thing");

                MP.RegisterSyncMethod(AccessTools.Method(disallowGizmoType, "Process")).SetContext(SyncContext.MapSelected);
                MP.RegisterSyncWorker<Command>(RemoveFromStockpileGizmoWorker, disallowGizmoType);
            }
        }

        void SmartStockpileDesignatorWorker(SyncWorker sync, ref Designator_ZoneAddStockpile obj) {
            if (sync.isWriting) {
                sync.Write(new List<ThingDef>((IEnumerable<ThingDef>) allowedThingsGetter.Invoke(obj)));
                sync.Write(specialThingFilterDefLabel(obj));
                sync.Write(priorityLabel(obj));
                sync.Write(contentsLabel(obj));
            } else {
                var allowedThings = sync.Read<List<ThingDef>>();
                var specialThingFilterDef = sync.Read<SpecialThingFilterDef>();
                var priority = sync.Read<StoragePriority>();
                var label = sync.Read<string>();
                obj = (Designator_ZoneAddStockpile) Activator.CreateInstance(designatorType, allowedThings, specialThingFilterDef, priority, label);
            }
        }

        void ReallowInStockpileGizmoWorker(SyncWorker sync, ref Command obj) {
            if (sync.isWriting) {
                sync.Write(thingDefLabel(obj));
                sync.Write(zoneStockpileLabel(obj));
                sync.Write(categoryLabel(obj));
            } else {
                obj = (Command)FormatterServices.GetUninitializedObject(reallowGizmoType);
                thingDefLabel(obj) = sync.Read<ThingDef>();
                zoneStockpileLabel(obj) = sync.Read<Zone_Stockpile>();
                categoryLabel(obj) = sync.Read<ThingCategoryDef>();
            }
        }

        void RemoveFromStockpileGizmoWorker(SyncWorker sync, ref Command obj) {
            if (sync.isWriting) {
                sync.Write(thingLabel(obj));
            } else {
                obj = (Command)FormatterServices.GetUninitializedObject(disallowGizmoType);
                thingLabel(obj) = sync.Read<Thing>();
            }
        }
    }
}