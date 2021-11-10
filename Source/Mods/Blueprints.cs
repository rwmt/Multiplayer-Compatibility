using HarmonyLib;
using Multiplayer.API;
using System;
using System.Runtime.Serialization;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Verse;

namespace Multiplayer.API
{
    public static class SyncWorkerExtension
    {
        public static void Write(this SyncWorker sync, Type type, object obj)
        => typeof(SyncWorker).GetMethod("Write").MakeGenericMethod(type).Invoke(sync, new object[] { obj });
        public static object Read(this SyncWorker sync, Type type)
        => typeof(SyncWorker).GetMethod("Read").MakeGenericMethod(type).Invoke(sync, new object[0]);
    }
}

namespace Multiplayer.Compat
{
    /// <summary>Misc. Robots by HaploX1</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=708455313"/>
    [MpCompatFor("Fluffy.Blueprints")]
    class Blueprints
    {
        public Blueprints(ModContentPack mod) => LongEventHandler.ExecuteWhenFinished(LatePatch);

        //All used types, fields and methods for BuildableInfo
        readonly static Type buildableInfo = AccessTools.TypeByName("Blueprints.BuildableInfo");
        readonly static FieldInfo buildableInfoTerrainDef = AccessTools.Field(buildableInfo, "_terrainDef");
        readonly static FieldInfo buildableInfoThingDef = AccessTools.Field(buildableInfo, "_thingDef");
        readonly static FieldInfo buildableInfoPosition = AccessTools.Field(buildableInfo, "_position");
        readonly static FieldInfo buildableInfoRotation = AccessTools.Field(buildableInfo, "_rotation");
        readonly static FieldInfo buildableInfoStuff = AccessTools.Field(buildableInfo, "_stuff");
        readonly static ConstructorInfo buildableInfoConstructorTerrain = AccessTools.Constructor(buildableInfo, new Type[] { typeof(TerrainDef), typeof(IntVec3), typeof(IntVec3) });
        readonly static ConstructorInfo buildableInfoConstructorThing = AccessTools.Constructor(buildableInfo, new Type[] { typeof(Thing), typeof(IntVec3) });

        //SyncWorker for BuildableInfo
        private static void SyncBuildableInfo(SyncWorker sync, ref IExposable obj)
        {
            if(sync.isWriting)
            {
                TerrainDef terrain = (TerrainDef) buildableInfoTerrainDef.GetValue(obj);
                bool is_terrain = (terrain != null);
                sync.Write(is_terrain);
                if (is_terrain)
                {
                    sync.Write(terrain);
                    IntVec3 position = (IntVec3)buildableInfoPosition.GetValue(obj);
                    sync.Write(position.x);
                    sync.Write(position.y);
                    sync.Write(position.z);
                } else {
                    sync.Write((ThingDef)buildableInfoThingDef.GetValue(obj));
                    IntVec3 position = (IntVec3)buildableInfoPosition.GetValue(obj);
                    sync.Write(position.x);
                    sync.Write(position.y);
                    sync.Write(position.z);
                    sync.Write((Rot4)buildableInfoRotation.GetValue(obj));
                    sync.Write((ThingDef)buildableInfoStuff.GetValue(obj));

                }
            } else {
                bool is_terrain = sync.Read<bool>();
                if (is_terrain)
                {
                    TerrainDef terrain = sync.Read<TerrainDef>();
                    IntVec3 position = new IntVec3(sync.Read<int>(), sync.Read<int>(), sync.Read<int>());
                    obj = (IExposable)buildableInfoConstructorTerrain.Invoke(new object[] { terrain, position, IntVec3.Zero });
                } else {
                    ThingDef def = sync.Read<ThingDef>();
                    IntVec3 position = new IntVec3(sync.Read<int>(), sync.Read<int>(), sync.Read<int>());
                    Rot4 rotation = sync.Read<Rot4>();
                    ThingDef stuff = sync.Read<ThingDef>();
                    Thing thing = new Thing();
                    thing.def = def;
                    thing.Position = position;
                    thing.Rotation = rotation;
                    thing.SetStuffDirect(stuff);
                    obj = (IExposable)buildableInfoConstructorThing.Invoke(new object[] { thing, IntVec3.Zero });
                }
                
            }
        }

        //All types, fields and constructors used for syncing Blueprint
        readonly static Type listBuildableInfo = typeof(List<>).MakeGenericType(buildableInfo);
        readonly static Type blueprint = AccessTools.TypeByName("Blueprints.Blueprint");
        readonly static FieldInfo contentsField = AccessTools.Field(blueprint, "contents");
        readonly static FieldInfo _sizeField = AccessTools.Field(blueprint, "_size");
        readonly static ConstructorInfo blueprintConstructor = AccessTools.Constructor(blueprint, new Type[] { listBuildableInfo, typeof(IntVec2), typeof(string), typeof(bool) });

        //SyncWorker for Blueprint
        private static void SyncBlueprint(SyncWorker sync, ref IExposable obj)
        {
            if (sync.isWriting)
            {
                sync.Write(listBuildableInfo, contentsField.GetValue(obj));
                sync.Write(((IntVec2)_sizeField.GetValue(obj)).x);
                sync.Write(((IntVec2)_sizeField.GetValue(obj)).z);
            } else
            {
                obj = (IExposable)blueprintConstructor.Invoke(new[] { sync.Read(listBuildableInfo), new IntVec2(sync.Read<int>(), sync.Read<int>()), null, true });
            }
        }

        //All types, fields and constructors used to sync Designator_Blueprint
        readonly static Type designator_Blueprint = AccessTools.TypeByName("Blueprints.Designator_Blueprint");
        readonly static MethodInfo blueprintGetter = AccessTools.DeclaredPropertyGetter(designator_Blueprint, "Blueprint");
        readonly static ConstructorInfo designator_BlueprintConstructor = AccessTools.Constructor(designator_Blueprint, new Type[] { blueprint });

        //SyncWorker for Designator_Blueprint
        private static void SyncDesignator_Blueprint(SyncWorker sync, ref Designator obj)
        {
            if (sync.isWriting)
                sync.Write(blueprint, blueprintGetter.Invoke(obj, null));
            else
            {
                //Designator_Blueprint(Blueprint) constructor
                obj = (Designator)designator_BlueprintConstructor.Invoke(new[]{ sync.Read(blueprint) });
            }
        }

        private static void LatePatch()
        {
            //Register the SyncWorkers : ready to build blueprints !
            MP.RegisterSyncWorker<IExposable>(SyncBuildableInfo, buildableInfo);
            MP.RegisterSyncWorker<IExposable>(SyncBlueprint, blueprint);
            MP.RegisterSyncWorker<Designator>(SyncDesignator_Blueprint, designator_Blueprint);
        }
    }
}
