using HarmonyLib;
using Multiplayer.API;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;
using static UnityEngine.UI.CanvasScaler;

namespace Multiplayer.Compat
{
    /// <summary>ZiTools objects seeker by MaxZiCode</summary>
    /// <see href="https://github.com/MaxZiCode/ZiTools"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3280114640"/>
    [MpCompatFor("MaxZiCode.dws.ZiTools")]
    public class ZiTools
    {
        static object FavChange = null;
        static MethodInfo GetObjectsDatabase;
        static PropertyInfo PropFavList;
        static FieldInfo UnitsDict;

        static FieldInfo DontSync;

        static PropertyInfo PropLabel;

        public ZiTools(ModContentPack mod)
        {
            LongEventHandler.ExecuteWhenFinished(LatePatch);
        }
        void LatePatch()
        {
            DontSync = AccessTools.Field(AccessTools.TypeByName("Multiplayer.Client.Multiplayer"), "dontSync");

            var type = AccessTools.TypeByName("ZiTools.ZiTools_GameComponent");
            GetObjectsDatabase = AccessTools.Method(type, "GetObjectsDatabase");

            type = AccessTools.TypeByName("ZiTools.ObjectsDatabase");
            PropFavList = AccessTools.Property(type, "UnitsInFavourites");
            UnitsDict = AccessTools.Field(type, "unitsDict");

            type = AccessTools.TypeByName("ZiTools.ObjectSeeker_Window");
            MpCompat.harmony.Patch(AccessTools.Method(type, "DoWindowContents"),
                postfix: new HarmonyMethod(typeof(ZiTools), nameof(PostDoWindowContents))
                );
            MpCompat.harmony.Patch(AccessTools.Method(type, "DrawObjectsList"),
                postfix: new HarmonyMethod(typeof(ZiTools), nameof(PostDrawObjectsList))
                );


            type = AccessTools.TypeByName("ZiTools.MapMarksManager");
            MpCompat.harmony.Patch(AccessTools.Method(type, "SetMarks"),
                prefix: new HarmonyMethod(typeof(MapMarksManager_Marks_Patch), nameof(MapMarksManager_Marks_Patch.Prefix)),
                finalizer: new HarmonyMethod(typeof(MapMarksManager_Marks_Patch), nameof(MapMarksManager_Marks_Patch.Finalizer))
                );
            MpCompat.harmony.Patch(AccessTools.Method(type, "RemoveMarks"),
                prefix: new HarmonyMethod(typeof(MapMarksManager_Marks_Patch), nameof(MapMarksManager_Marks_Patch.Prefix)),
                finalizer: new HarmonyMethod(typeof(MapMarksManager_Marks_Patch), nameof(MapMarksManager_Marks_Patch.Finalizer))
                );

            //DBUnit
            type = AccessTools.TypeByName("ZiTools.DBUnit");
            //MP.RegisterSyncWorker<object>(SyncDBUnit, type);
            PropLabel = AccessTools.Property(type, "Label");

            MP.RegisterSyncMethod(typeof(ZiTools), nameof(ZiTools.SyncedRemoveFromFav));
            MP.RegisterSyncMethod(typeof(ZiTools), nameof(ZiTools.SyncedAddToFav));

        }
        //static void SyncDBUnit(SyncWorker sync, ref object obj)
        //{
        //    if(sync.isWriting)
        //    {
        //        sync.Write(PropLabel.GetValue(obj) as string);
        //    } 
        //    else
        //    {
        //        var label = sync.Read<string>();
        //        GetUnitByLabel(label, ref obj);
        //    }
        //}

        static class MapMarksManager_Marks_Patch
        {
            static object preDontSync;
            public static void Prefix()
            {
                preDontSync = DontSync.GetValue(null);
                DontSync.SetValue(null, true);
            }
            public static void Finalizer()
            {
                DontSync.SetValue(null, preDontSync);
            }
        }


        public static void PostDrawObjectsList(ref IExposable favChange)
        {
            if (favChange != null)
            {
                FavChange = favChange;
            }
        }
        public static void PostDoWindowContents()
        {
            if(FavChange != null)
            {
                IList list = GetFavList();
                if(list == null)
                {
                    Log.Warning("ZiTools have no Favlist now");
                    FavChange = null;
                    return;
                }
                if (!list.Contains(FavChange))
                {
                    list.Add(FavChange);
                    SyncedRemoveFromFav(PropLabel.GetValue(FavChange) as string);
                }
                else
                {
                    list.Remove(FavChange);
                    SyncedAddToFav(PropLabel.GetValue(FavChange) as string);
                }
                FavChange = null;
            }
        }
        static object GetUnitByLabel(string label)
        {
            object odb = GetObjectsDatabase.Invoke(null, null);
            IDictionary unitsDict = UnitsDict.GetValue(odb) as IDictionary;

            foreach(var obj in unitsDict.Values)
                if (PropLabel.GetValue(obj) as string == label)
                    return obj;
            return null;
        }
        static IList GetFavList()
        {
            object odb = GetObjectsDatabase.Invoke(null, null);
            IList list = PropFavList?.GetValue(odb) as IList;

            return list;
        }
        static void SyncedRemoveFromFav(string favChangeLabel)
        {
            var unit = GetUnitByLabel(favChangeLabel);
            if(unit != null)
                GetFavList().Remove(unit);
        }
        static void SyncedAddToFav(string favChangeLabel)
        {
            var unit = GetUnitByLabel(favChangeLabel);
            if (unit != null)
                GetFavList().Add(unit);
        }
    }
}