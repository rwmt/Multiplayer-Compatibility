using HarmonyLib;
using Multiplayer.API;
using System.Collections.Generic;
using System.Reflection;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>ZiTools objects seeker by MaxZiCode</summary>
    /// <see href="https://github.com/MaxZiCode/ZiTools"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3280114640"/>
    [MpCompatFor("MaxZiCode.dws.ZiTools")]
    public class ZiTools
    {
        static object FavChange;
        static MethodInfo GetObjectsDatabase;
        static PropertyInfo PropFavList;
        public ZiTools(ModContentPack mod)
        {
            var type = AccessTools.TypeByName("ZiTools.ZiTools_GameComponent");
            GetObjectsDatabase = AccessTools.Method(type, "GetObjectsDatabase");

            type = AccessTools.TypeByName("ZiTools.ObjectsDatabase");
            PropFavList = AccessTools.Property(type, "UnitsInFavourites");

            type = AccessTools.TypeByName("ZiTools.ObjectSeeker_Window");
            MpCompat.harmony.Patch(AccessTools.Method(type, "DoWindowContents"),
                postfix: new HarmonyMethod(typeof(ZiTools), nameof(PostDoWindowContents))
                );
            MpCompat.harmony.Patch(AccessTools.Method(type, "DrawObjectsList"),
                postfix: new HarmonyMethod(typeof(ZiTools),nameof(PostDrawObjectsList))
                );

            MP.RegisterSyncMethod(AccessTools.Method(typeof(ZiTools), nameof(ZiTools.SyncedRemoveFromFav)));
            MP.RegisterSyncMethod(AccessTools.Method(typeof(ZiTools), nameof(ZiTools.SyncedAddToFav)));
        }

        public static void PostDrawObjectsList(ref object favChange)
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
                if (!GetFavList().Contains(FavChange))
                    SyncedRemoveFromFav(FavChange);
                else
                    SyncedAddToFav(FavChange);

                FavChange = null;
            }
        }
        static List<object> GetFavList()
        {
            object odb = GetObjectsDatabase.Invoke(null, null);
            return PropFavList.GetValue(odb) as List<object>;
        }
        static void SyncedRemoveFromFav(object favChange)
        {
            GetFavList().Remove(favChange);
        }
        static void SyncedAddToFav(object favChange)
        {
            GetFavList().Add(favChange);
        }
    }
}