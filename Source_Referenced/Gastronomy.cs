using System.Collections.Generic;
using System.Linq;
using Gastronomy.Dining;
using Gastronomy.Restaurant;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Gastronomy by Orion</summary>
    /// <see href="https://github.com/OrionFive/Gastronomy"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2279786905"/>
    [MpCompatFor("Orion.Gastronomy")]
    class Gastronomy
    {
        // RestaurantController
        private static FastInvokeHandler linkRegisterMethod;
        private static ISyncField openForBusinessField;
        private static ISyncField allowGuestsField;
        private static ISyncField allowColonistsField;
        private static ISyncField allowPrisonersField;
        private static ISyncField allowSlavesField;
        private static ISyncField guestPricePercentageField;

        // ITab_Register_Restaurant
        private static AccessTools.FieldRef<object, object> iTabRestaurantField;

        public Gastronomy(ModContentPack mod)
        {
            // Don't even ask why it needs to be execute late. If it isn't then it breaks other mods (VFE Security and VFE Mechanoids). That's all I know.
            LongEventHandler.ExecuteWhenFinished(LatePatch);
        }

        private static void LatePatch()
        {
            var type = typeof(RestaurantController);
            linkRegisterMethod = MethodInvoker.GetHandler(AccessTools.Method(type, "LinkRegister"));
            openForBusinessField = MP.RegisterSyncField(type, nameof(RestaurantController.openForBusiness));
            allowGuestsField = MP.RegisterSyncField(type, nameof(RestaurantController.allowGuests));
            allowColonistsField = MP.RegisterSyncField(type, nameof(RestaurantController.allowColonists));
            allowPrisonersField = MP.RegisterSyncField(type, nameof(RestaurantController.allowPrisoners));
            allowSlavesField = MP.RegisterSyncField(type, nameof(RestaurantController.allowSlaves));
            guestPricePercentageField = MP.RegisterSyncField(type, nameof(RestaurantController.guestPricePercentage));
            MP.RegisterSyncMethod(AccessTools.PropertySetter(type, nameof(RestaurantController.Name)));
            MP.RegisterSyncMethod(type, "LinkRegister");
            MP.RegisterSyncWorker<RestaurantController>(SyncRestaurantController, type);

            type = typeof(RestaurantsManager);
            MP.RegisterSyncMethod(type, nameof(RestaurantsManager.AddRestaurant)).SetContext(SyncContext.MapSelected);
            MP.RegisterSyncMethod(type, nameof(RestaurantsManager.DeleteRestaurant));
            MpCompat.harmony.Patch(AccessTools.Method(type, nameof(RestaurantsManager.AddRestaurant)),
                prefix: new HarmonyMethod(typeof(Gastronomy), nameof(PreAddRestaurant)),
                postfix: new HarmonyMethod(typeof(Gastronomy), nameof(PostAddRestaurant)));

            type = typeof(ITab_Register_Restaurant);
            iTabRestaurantField = AccessTools.FieldRefAccess<object>(type, nameof(ITab_Register_Restaurant.restaurant));
            MpCompat.harmony.Patch(AccessTools.Method(type, "FillTab"),
                prefix: new HarmonyMethod(typeof(Gastronomy), nameof(PreFillTab)),
                postfix: new HarmonyMethod(typeof(Gastronomy), nameof(PostFillTab)));
            MpCompat.harmony.Patch(AccessTools.Method(type, "SetRestaurant"),
                prefix: new HarmonyMethod(typeof(Gastronomy), nameof(PreSetRestaurant)));
            MP.ThingFilters.RegisterThingFilterListener(GetThingFilter);
            MP.ThingFilters.RegisterThingFilterTarget<RestaurantWrapper>();

            type = typeof(CompCanDineAt);
            MP.RegisterSyncMethod(type, nameof(CompCanDineAt.ToggleDining));
            MP.RegisterSyncMethod(type, nameof(CompCanDineAt.ChangeDeco));

            type = typeof(RestaurantMenu);
            MP.RegisterSyncWorker<RestaurantMenu>(SyncRestaurantMenu, type);
            MpCompat.harmony.Patch(AccessTools.Constructor(type),
                postfix: new HarmonyMethod(typeof(Gastronomy), nameof(PostRestaurantMenuConstructor)));
        }

        private static void SyncRestaurantController(SyncWorker sync, ref RestaurantController controller)
        {
            var comp = Find.CurrentMap.GetComponent<RestaurantsManager>();

            if (sync.isWriting)
            {
                var index = comp.restaurants.IndexOf(controller);
                sync.Write(index);
            }
            else
            {
                var index = sync.Read<int>();
                if (index >= 0)
                    controller = comp.restaurants[index];
            }
        }

        private static void SyncRestaurantMenu(SyncWorker sync, ref RestaurantMenu menu)
        {
            var comp = Find.CurrentMap.GetComponent<RestaurantsManager>();

            if (sync.isWriting)
            {
                var index = -1;

                for (var i = 0; i < comp.restaurants.Count; i++)
                {
                    if (comp.restaurants[i].menu == menu)
                    {
                        index = i;
                        break;
                    }
                }

                sync.Write(index);
            }
            else
            {
                var index = sync.Read<int>();

                if (index >= 0)
                    menu = comp.restaurants[index].menu;
            }
        }

        private static void PreFillTab(ITab_Register_Restaurant __instance)
        {
            if (!MP.IsInMultiplayer || __instance.restaurant == null) // ___restaurant field is set up on first open tick
                return;

            restaurantFilter = new(__instance.restaurant.menu);

            MP.WatchBegin();
            openForBusinessField.Watch(__instance.restaurant);
            allowGuestsField.Watch(__instance.restaurant);
            allowColonistsField.Watch(__instance.restaurant);
            allowPrisonersField.Watch(__instance.restaurant);
            allowSlavesField.Watch(__instance.restaurant);
            guestPricePercentageField.Watch(__instance.restaurant);
        }

        private static void PostFillTab()
        {
            if (!MP.IsInMultiplayer)
                return;

            restaurantFilter = null;
            MP.WatchEnd();
        }

        private static void PreAddRestaurant(RestaurantsManager __instance, ref bool __state)
            => __state = MP.IsInMultiplayer && __instance.restaurants.Any();

        private static void PostAddRestaurant(ref bool __state, RestaurantController __result)
        {
            if (!__state)
                return;

            var register = Find.Selector.SingleSelectedThing;
            linkRegisterMethod(__result, register);

            if (MP.IsExecutingSyncCommandIssuedBySelf && Find.MainTabsRoot.OpenTab == MainButtonDefOf.Inspect)
            {
                var mainTabWindow_Inspect = (MainTabWindow_Inspect)Find.MainTabsRoot.OpenTab.TabWindow;
                var tab = mainTabWindow_Inspect.CurTabs?.FirstOrDefault(x => x.GetType() == typeof(ITab_Register_Restaurant));
                if (tab != null)
                    iTabRestaurantField(tab) = __result;
            }
        }

        private static bool PreSetRestaurant(object newRestaurant)
            => newRestaurant != null;

        private static void PostRestaurantMenuConstructor(RestaurantMenu __instance)
            => __instance.GetMenuFilters(out _, out _);

        private static ThingFilterContext GetThingFilter() => restaurantFilter;

        private static RestaurantWrapper restaurantFilter;

        private record RestaurantWrapper(RestaurantMenu Menu) : ThingFilterContext
        {
            public override ThingFilter Filter => Menu.menuFilter;
            public override ThingFilter ParentFilter => Menu.menuGlobalFilter;

            public override IEnumerable<SpecialThingFilterDef> HiddenFilters
            {
                get { yield return SpecialThingFilterDefOf.AllowFresh; }
            }
        }
    }
}
