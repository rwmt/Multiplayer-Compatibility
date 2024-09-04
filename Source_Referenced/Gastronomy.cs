using System.Collections.Generic;
using System.Linq;
using CashRegister;
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
    internal class Gastronomy
    {
        #region Fields

        // RestaurantController
        private static ISyncField openForBusinessField;
        private static ISyncField allowGuestsField;
        private static ISyncField allowColonistsField;
        private static ISyncField allowPrisonersField;
        private static ISyncField allowSlavesField;
        private static ISyncField guestPricePercentageField;

        #endregion

        #region Main patch

        public Gastronomy(ModContentPack mod)
        {
            // Don't even ask why it needs to be execute late. If it isn't then it breaks other mods (VFE Security and VFE Mechanoids). That's all I know.
            LongEventHandler.ExecuteWhenFinished(LatePatch);
        }

        private static void LatePatch()
        {
            var type = typeof(RestaurantController);
            openForBusinessField = MP.RegisterSyncField(type, nameof(RestaurantController.openForBusiness));
            allowGuestsField = MP.RegisterSyncField(type, nameof(RestaurantController.allowGuests));
            allowColonistsField = MP.RegisterSyncField(type, nameof(RestaurantController.allowColonists));
            allowPrisonersField = MP.RegisterSyncField(type, nameof(RestaurantController.allowPrisoners));
            allowSlavesField = MP.RegisterSyncField(type, nameof(RestaurantController.allowSlaves));
            guestPricePercentageField = MP.RegisterSyncField(type, nameof(RestaurantController.guestPricePercentage));
            MP.RegisterSyncMethod(type, nameof(RestaurantController.Name));
            MP.RegisterSyncMethod(type, nameof(RestaurantController.LinkRegister));
            MP.RegisterSyncWorker<RestaurantController>(SyncRestaurantController, type);

            type = typeof(RestaurantsManager);
            MP.RegisterSyncMethod(type, nameof(RestaurantsManager.AddRestaurant)).SetContext(SyncContext.MapSelected);
            MP.RegisterSyncMethod(type, nameof(RestaurantsManager.DeleteRestaurant));
            MpCompat.harmony.Patch(AccessTools.Method(type, nameof(RestaurantsManager.AddRestaurant)),
                prefix: new HarmonyMethod(typeof(Gastronomy), nameof(PreAddRestaurant)),
                postfix: new HarmonyMethod(typeof(Gastronomy), nameof(PostAddRestaurant)));

            type = typeof(ITab_Register_Restaurant);
            MpCompat.harmony.Patch(AccessTools.Method(type, nameof(ITab_Register_Restaurant.FillTab)),
                prefix: new HarmonyMethod(typeof(Gastronomy), nameof(PreFillTab)),
                finalizer: new HarmonyMethod(typeof(Gastronomy), nameof(PostFillTab)));
            MpCompat.harmony.Patch(AccessTools.Method(type, nameof(ITab_Register_Restaurant.SetRestaurant)),
                prefix: new HarmonyMethod(typeof(Gastronomy), nameof(PreSetRestaurant)));

            type = typeof(CompCanDineAt);
            MP.RegisterSyncMethod(type, nameof(CompCanDineAt.ToggleDining));
            MP.RegisterSyncMethod(type, nameof(CompCanDineAt.ChangeDeco));

            // Ensure filters always initialized
            MpCompat.harmony.Patch(AccessTools.Constructor(typeof(RestaurantMenu)),
                postfix: new HarmonyMethod(typeof(Gastronomy), nameof(PostRestaurantMenuConstructor)));
        }

        #endregion

        #region Sync Workers

        private static void SyncRestaurantController(SyncWorker sync, ref RestaurantController controller)
        {
            if (sync.isWriting)
            {
                var comp = controller.Map?.GetComponent<RestaurantsManager>();
                var index = comp?.restaurants.IndexOf(controller) ?? -1;

                sync.Write(index);
                if (index >= 0)
                    sync.Write(comp);
            }
            else
            {
                var index = sync.Read<int>();
                if (index >= 0)
                {
                    var manager = sync.Read<RestaurantsManager>();
                    if (manager.restaurants.Count > index)
                        controller = manager.restaurants[index];
                    else
                        Log.Error($"Received out-of-range store index, received={index}, count={manager.restaurants.Count}");
                }
            }
        }

        #endregion

        #region Patches

        private static void PreFillTab(ITab_Register_Restaurant __instance, out bool __state)
        {
            if (!MP.IsInMultiplayer)
            {
                __state = false;
                return;
            }

            // Set up restaurant, as it'll cause issues if it's incorrect/missing
            __instance.restaurant = __instance.Register.GetRestaurant();
            __instance.restaurant ??= __instance.Register.GetAllRestaurants().First();

            MP.SetThingFilterContext(new RestaurantWrapper(__instance.restaurant));

            MP.WatchBegin();
            openForBusinessField.Watch(__instance.restaurant);
            allowGuestsField.Watch(__instance.restaurant);
            allowColonistsField.Watch(__instance.restaurant);
            allowPrisonersField.Watch(__instance.restaurant);
            allowSlavesField.Watch(__instance.restaurant);
            guestPricePercentageField.Watch(__instance.restaurant);

            __state = true;
        }

        private static void PostFillTab(bool __state)
        {
            if (!__state)
                return;

            MP.SetThingFilterContext(null);
            MP.WatchEnd();
        }

        private static void PreAddRestaurant(RestaurantsManager __instance, ref bool __state)
            => __state = MP.IsInMultiplayer && __instance.restaurants.Any();

        private static void PostAddRestaurant(ref bool __state, RestaurantController __result)
        {
            if (!__state || __result == null)
                return;

            __result.LinkRegister((Building_CashRegister)Find.Selector.SingleSelectedThing);

            // If the player added a restaurant, in SP it would have opened up the new one.
            // Due to us syncing it, it doesn't happen by default so this should ensure it does.
            if (MP.IsExecutingSyncCommandIssuedBySelf && Find.MainTabsRoot.OpenTab == MainButtonDefOf.Inspect)
            {
                var mainTabWindow_Inspect = (MainTabWindow_Inspect)Find.MainTabsRoot.OpenTab.TabWindow;
                var tab = mainTabWindow_Inspect.CurTabs?.OfType<ITab_Register_Restaurant>().FirstOrDefault();
                if (tab != null)
                    tab.restaurant = __result;
            }
        }

        private static bool PreSetRestaurant(object newRestaurant)
            => newRestaurant != null;

        private static void PostRestaurantMenuConstructor(RestaurantMenu __instance)
            => __instance.GetMenuFilters(out _, out _);

        #endregion

        #region Thing Filter Context

        public record RestaurantWrapper(RestaurantController Controller) : ThingFilterContext
        {
            public override ThingFilter Filter => Controller.Menu.menuFilter;
            public override ThingFilter ParentFilter => Controller.Menu.menuGlobalFilter;

            public override IEnumerable<SpecialThingFilterDef> HiddenFilters
            {
                get { yield return SpecialThingFilterDefOf.AllowFresh; }
            }

            public RestaurantController Controller { get; } = Controller;
        }

        #endregion
    }
}
