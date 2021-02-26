using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
        // ITab_Register
        private static MethodInfo hiddenSpecialThingFiltersMethod;
        // RestauranController
        private static Type restaurantControllerType;
        private static FieldInfo controllerTimetableField;
        private static FieldInfo controllerMenuField;
        private static ISyncField[] controllerFieldsToSync;
        private static ISyncField controllerTimesSync; // Synced manually
        private static FieldInfo controllerDebtField;
        // RestaurantDebt
        private static FieldInfo restaurantDebtAllDebtsField;
        // Building_CashRegister
        private static FieldInfo restaurantControllerField;
        // RestaurantMenu
        private static FieldInfo menuFilterField;
        private static FieldInfo menuGlobalFilterField;
        private static MethodInfo initMenuFilterMethod;
        private static MethodInfo initMenuGlobalFilterMethod;
        // TimetableBool
        private static FieldInfo timetableTimesField;
        // MP overrides
        private static object restaurantController;
        private static MethodInfo shouldSyncGetter;
        private static MethodInfo allowCategoryHelperMethod;

        public Gastronomy(ModContentPack mod)
        {
            // Don't even ask why it needs to be execute late. If it isn't then it breaks other mods (VFE Security and VFE Mechanoids). That's all I know.
            LongEventHandler.ExecuteWhenFinished(LatePatch);
        }

        private static void LatePatch()
        {
            // Tables
            {
                var type = AccessTools.TypeByName("Gastronomy.Dining.CompCanDineAt");
                MP.RegisterSyncMethod(type, "ChangeDeco");
                MP.RegisterSyncMethod(type, "ToggleDining");
            }
            // Register menu
            {
                // ITab_Register
                var type = AccessTools.TypeByName("Gastronomy.TableTops.ITab_Register");
                hiddenSpecialThingFiltersMethod = AccessTools.Method(type, "HiddenSpecialThingFilters");
                MpCompat.harmony.Patch(
                    AccessTools.Method(type, "FillTab"),
                    prefix: new HarmonyMethod(typeof(Gastronomy), nameof(FillTabPrefix)),
                    postfix: new HarmonyMethod(typeof(Gastronomy), nameof(FillTabPostfix)));

                // Clear debt on cliking it
                MP.RegisterSyncDelegate(type, "<>c__DisplayClass18_0", "<DrawDebts>b__1").CancelIfAnyFieldNull().CancelIfNoSelectedObjects().SetContext(SyncContext.CurrentMap);

                // Sync worker for debt itself, requires SyncContext.CurrentMap
                type = AccessTools.TypeByName("Gastronomy.Restaurant.Debt");
                MP.RegisterSyncWorker<object>(SyncDebt, type);

                // RestaurantDebt, used for finding the debt
                type = AccessTools.TypeByName("Gastronomy.Restaurant.RestaurantDebt");
                restaurantDebtAllDebtsField = AccessTools.Field(type, "debts");

                // Building_CashRegister
                type = AccessTools.TypeByName("Gastronomy.TableTops.Building_CashRegister");
                restaurantControllerField = AccessTools.Field(type, "restaurant");

                // TimetableBool
                type = AccessTools.TypeByName("Gastronomy.Restaurant.Timetable.TimetableBool");
                timetableTimesField = AccessTools.Field(type, "times");

                // RestaurantMenu
                type = AccessTools.TypeByName("Gastronomy.Restaurant.RestaurantMenu");
                menuFilterField = AccessTools.Field(type, "menuFilter");
                menuGlobalFilterField = AccessTools.Field(type, "menuGlobalFilter");
                initMenuFilterMethod = AccessTools.Method(type, "InitMenuFilter");
                initMenuGlobalFilterMethod = AccessTools.Method(type, "InitMenuGlobalFilter");
                MpCompat.harmony.Patch(
                    AccessTools.Constructor(type),
                    prefix: new HarmonyMethod(typeof(Gastronomy), nameof(RestaurantMenuConstructorPrefix)));

                // RestaurantController
                restaurantControllerType = AccessTools.TypeByName("Gastronomy.RestaurantController");
                controllerFieldsToSync = (new[] { "openForBusiness", "allowGuests", "allowColonists", "allowPrisoners", "guestPricePercentage" }).Select(x => MP.RegisterSyncField(restaurantControllerType, x)).ToArray();
                controllerTimetableField = AccessTools.Field(restaurantControllerType, "timetableOpen");
                controllerMenuField = AccessTools.Field(restaurantControllerType, "menu");
                controllerTimesSync = MP.RegisterSyncField(restaurantControllerType, "timetableOpen/times").SetBufferChanges();
                controllerDebtField = AccessTools.Field(restaurantControllerType, "debts");
            }
            // MP overrides
            {
                // https://github.com/rwmt/Multiplayer/blob/7aac8b54727d8626ec39429b97225fc88c807ab8/Source/Client/Multiplayer.cs#L58
                shouldSyncGetter = AccessTools.PropertyGetter(AccessTools.TypeByName("Multiplayer.Client.Multiplayer"), "ShouldSync");
                // https://github.com/rwmt/Multiplayer/blob/7aac8b54727d8626ec39429b97225fc88c807ab8/Source/Client/Sync/SyncHandlers.cs#L771
                allowCategoryHelperMethod = AccessTools.Method(AccessTools.TypeByName("Multiplayer.Client.SyncThingFilters"), "ThingFilter_AllowCategory_Helper");

                // https://github.com/rwmt/Multiplayer/blob/7aac8b54727d8626ec39429b97225fc88c807ab8/Source/Client/Sync/SyncSerialization.cs#L33
                // We get the thingFilterTarget, and add our own filter to it
                var thingFilterTarget = AccessTools.Field(AccessTools.TypeByName("Multiplayer.Client.Sync"), "thingFilterTarget");
                var target = thingFilterTarget.GetValue(null);

                // https://github.com/rwmt/Multiplayer/blob/7aac8b54727d8626ec39429b97225fc88c807ab8/Source/Client/Sync/Sync.cs#L1243
                // Get and call the method to add our filter
                AccessTools.Method(AccessTools.TypeByName("Multiplayer.Client.MultiTarget"), "Add", new Type[] { typeof(Type), typeof(string) })
                    .Invoke(target, new object[] { restaurantControllerType, "menu/menuFilter" });

                // https://github.com/rwmt/Multiplayer/blob/7aac8b54727d8626ec39429b97225fc88c807ab8/Source/Client/Sync/SyncHandlers.cs#L960
                MpCompat.harmony.Patch(AccessTools.PropertyGetter(AccessTools.TypeByName("Multiplayer.Client.SyncMarkers"), "ThingFilterOwner"),
                    prefix: new HarmonyMethod(typeof(Gastronomy), nameof(ThingFilterOwnerPrefix)));

                // Set prefixes to the 3 methods that need some additional work that isn't universal to all filters
                // (See the methods for github links)
                MpCompat.harmony.Patch(AccessTools.Method(typeof(ThingFilter), nameof(ThingFilter.SetAllow), new[] { typeof(ThingCategoryDef), typeof(bool), typeof(IEnumerable<ThingDef>), typeof(IEnumerable<SpecialThingFilterDef>) }),
                    prefix: new HarmonyMethod(typeof(Gastronomy), nameof(ThingFilter_SetAllowPrefix)));
                MpCompat.harmony.Patch(AccessTools.Method(typeof(ThingFilter), nameof(ThingFilter.SetDisallowAll)),
                    prefix: new HarmonyMethod(typeof(Gastronomy), nameof(ThingFilter_SetDisallowAllPrefix)));
                MpCompat.harmony.Patch(AccessTools.Method(typeof(ThingFilter), nameof(ThingFilter.SetAllowAll)),
                    prefix: new HarmonyMethod(typeof(Gastronomy), nameof(ThingFilter_SetAllowAllPrefix)));

                // Methods that we'll need to setup SyncFields/SyncMethods for MultiTarget
                var type = AccessTools.TypeByName("Multiplayer.Client.Sync");
                // https://github.com/rwmt/Multiplayer/blob/7aac8b54727d8626ec39429b97225fc88c807ab8/Source/Client/Sync/Sync.cs#L801
                var methodMultiTarget = AccessTools.Method(type, "MethodMultiTarget");
                // https://github.com/rwmt/Multiplayer/blob/7aac8b54727d8626ec39429b97225fc88c807ab8/Source/Client/Sync/Sync.cs#L818
                var fieldMultiTarget = AccessTools.Method(type, "FieldMultiTarget");

                // Create fields for SyncFields that will include our filter
                type = AccessTools.TypeByName("Multiplayer.Client.SyncFieldsPatches");
                // https://github.com/rwmt/Multiplayer/blob/7aac8b54727d8626ec39429b97225fc88c807ab8/Source/Client/Sync/SyncHandlers.cs#L66
                var values = (Array)fieldMultiTarget.Invoke(null, new[] { target, nameof(ThingFilter.AllowedHitPointsPercents) });
                foreach (var value in values)
                    ((ISyncField)value).SetBufferChanges();
                AccessTools.Field(type, "SyncThingFilterHitPoints").SetValue(null, values);
                // https://github.com/rwmt/Multiplayer/blob/7aac8b54727d8626ec39429b97225fc88c807ab8/Source/Client/Sync/SyncHandlers.cs#L69
                values = (Array)fieldMultiTarget.Invoke(null, new[] { target, nameof(ThingFilter.AllowedQualityLevels) });
                foreach (var value in values)
                    ((ISyncField)value).SetBufferChanges();
                AccessTools.Field(type, "SyncThingFilterQuality").SetValue(null, values);

                // Create fields for SyncMethods that will include our filter
                type = AccessTools.TypeByName("Multiplayer.Client.SyncThingFilters");
                // https://github.com/rwmt/Multiplayer/blob/7aac8b54727d8626ec39429b97225fc88c807ab8/Source/Client/Sync/SyncHandlers.cs#L658
                AccessTools.Field(type, "SyncThingFilterAllowThing").SetValue(null, methodMultiTarget.Invoke(null, new[] { target, nameof(ThingFilter.SetAllow), new SyncType[] { typeof(ThingDef), typeof(bool) } }));
                // https://github.com/rwmt/Multiplayer/blob/7aac8b54727d8626ec39429b97225fc88c807ab8/Source/Client/Sync/SyncHandlers.cs#L659
                AccessTools.Field(type, "SyncThingFilterAllowSpecial").SetValue(null, methodMultiTarget.Invoke(null, new[] { target, nameof(ThingFilter.SetAllow), new SyncType[] { typeof(SpecialThingFilterDef), typeof(bool) } }));
                // https://github.com/rwmt/Multiplayer/blob/7aac8b54727d8626ec39429b97225fc88c807ab8/Source/Client/Sync/SyncHandlers.cs#L660
                AccessTools.Field(type, "SyncThingFilterAllowStuffCategory").SetValue(null, methodMultiTarget.Invoke(null, new[] { target, nameof(ThingFilter.SetAllow), new SyncType[] { typeof(StuffCategoryDef), typeof(bool) } }));

                // Sync our helper methods
                // (See the methods for github links)
                MP.RegisterSyncMethod(typeof(Gastronomy), nameof(ThingFilter_DisallowAll_HelperRestaurant));
                MP.RegisterSyncMethod(typeof(Gastronomy), nameof(ThingFilter_AllowAll_HelperRestaurant));
                MP.RegisterSyncMethod(typeof(Gastronomy), nameof(ThingFilter_AllowCategory_HelperRestaurant));
            }
        }

        // Called before the tab is drawn, we set our field watches in here
        // On top of that, we set our ThingFilterOwner like the MP
        // https://github.com/rwmt/Multiplayer/blob/7aac8b54727d8626ec39429b97225fc88c807ab8/Source/Client/Sync/SyncHandlers.cs#L975
        private static void FillTabPrefix(ref object[] __state)
        {
            if (MP.IsInMultiplayer)
            {
                // ITab_Register.Register points to ITab.SelThing, which points to Find.Selector.SingleSelectedThing
                var controller = restaurantControllerField.GetValue(Find.Selector.SingleSelectedThing);
                var timetable = controllerTimetableField.GetValue(controller);
                __state = new object[] { timetable, ((List<bool>)timetableTimesField.GetValue(timetable)).ToArray() };

                MP.WatchBegin();
                restaurantController = controller;

                foreach (var field in controllerFieldsToSync)
                    field.Watch(controller);
            }
        }

        // Called after the tab is drawn, we end watching here and if needed manually sync the timetable
        // On top of that, we clear our ThingFilterOwner like the MP
        // https://github.com/rwmt/Multiplayer/blob/7aac8b54727d8626ec39429b97225fc88c807ab8/Source/Client/Sync/SyncHandlers.cs#L978
        private static void FillTabPostfix(ref object[] __state)
        {
            if (MP.IsInMultiplayer)
            {
                var timetable = __state[0];
                var oldTimes = (bool[])__state[1];
                var newTimes = (List<bool>)timetableTimesField.GetValue(timetable);

                for (int i = 0; i < 24; i++)
                {
                    if (oldTimes[i] != newTimes[i])
                    {
                        timetableTimesField.SetValue(timetable, new List<bool>(oldTimes));
                        controllerTimesSync.DoSync(restaurantController, newTimes);
                        break;
                    }
                }

                restaurantController = null;
                MP.WatchEnd();
            }
        }

        // It's needed to initially fill the menu.
        // It's normally initialized when the tab is open, which doesn't seem to work in MP environment,
        // so this is a workaround for that.
        private static void RestaurantMenuConstructorPrefix(object __instance)
        {
            var globalFilter = initMenuGlobalFilterMethod.Invoke(__instance, Array.Empty<object>());
            initMenuFilterMethod.Invoke(__instance, new[] { globalFilter });
        }

        // https://github.com/rwmt/Multiplayer/blob/7aac8b54727d8626ec39429b97225fc88c807ab8/Source/Client/Sync/SyncHandlers.cs#L960
        // It's used by several MP method patches for syncing, so we need to provide our filter too
        private static bool ThingFilterOwnerPrefix(ref object __result)
        {
            if (!MP.IsInMultiplayer || restaurantController == null) return true;

            __result = restaurantController;
            return false;
        }

        // https://github.com/rwmt/Multiplayer/blob/7aac8b54727d8626ec39429b97225fc88c807ab8/Source/Client/Sync/SyncHandlers.cs#L681
        // One of the places where we need to manually handle the filling/clearing of the filter, same as MP
        private static bool ThingFilter_SetAllowPrefix(ThingCategoryDef categoryDef, bool allow)
        {
            if (!(bool)shouldSyncGetter.Invoke(null, Array.Empty<object>()) || restaurantController == null) return true;

            ThingFilter_AllowCategory_HelperRestaurant((MapComponent)restaurantController, categoryDef, allow);

            return false;
        }

        // https://github.com/rwmt/Multiplayer/blob/7aac8b54727d8626ec39429b97225fc88c807ab8/Source/Client/Sync/SyncHandlers.cs#L698
        // One of the places where we need to manually handle the filling/clearing of the filter, same as MP
        private static bool ThingFilter_SetDisallowAllPrefix()
        {
            if (!(bool)shouldSyncGetter.Invoke(null, Array.Empty<object>()) || restaurantController == null) return true;

            ThingFilter_DisallowAll_HelperRestaurant((MapComponent)restaurantController);

            return false;
        }

        // https://github.com/rwmt/Multiplayer/blob/7aac8b54727d8626ec39429b97225fc88c807ab8/Source/Client/Sync/SyncHandlers.cs#L715
        // One of the places where we need to manually handle the filling/clearing of the filter, same as MP
        private static bool ThingFilter_SetAllowAllPrefix()
        {
            if (!(bool)shouldSyncGetter.Invoke(null, Array.Empty<object>()) || restaurantController == null) return true;

            ThingFilter_AllowAll_HelperRestaurant((MapComponent)restaurantController);

            return false;
        }

        // https://github.com/rwmt/Multiplayer/blob/7aac8b54727d8626ec39429b97225fc88c807ab8/Source/Client/Sync/SyncHandlers.cs#L736
        // Synced helper method, similarly to MP (IStoreSettingsParent is the closest to what we're dealing with in here, but there's not much difference)
        private static void ThingFilter_DisallowAll_HelperRestaurant(MapComponent controller)
        {
            var menu = controllerMenuField.GetValue(controller);
            var filter = (ThingFilter)menuFilterField.GetValue(menu);
            var hiddenSpecialFilter = hiddenSpecialThingFiltersMethod.Invoke(null, Array.Empty<object>());

            filter.SetDisallowAll(null, (IEnumerable<SpecialThingFilterDef>)hiddenSpecialFilter);
        }

        // https://github.com/rwmt/Multiplayer/blob/7aac8b54727d8626ec39429b97225fc88c807ab8/Source/Client/Sync/SyncHandlers.cs#L748
        // Synced helper method, similarly to MP (IStoreSettingsParent is the closest to what we're dealing with in here, but there's not much difference)
        private static void ThingFilter_AllowAll_HelperRestaurant(MapComponent controller)
        {
            var menu = controllerMenuField.GetValue(controller);
            var filter = (ThingFilter)menuFilterField.GetValue(menu);
            var parentFilter = (ThingFilter)menuGlobalFilterField.GetValue(menu);

            filter.SetAllowAll(parentFilter);
        }

        // https://github.com/rwmt/Multiplayer/blob/7aac8b54727d8626ec39429b97225fc88c807ab8/Source/Client/Sync/SyncHandlers.cs#L760
        // Synced helper method, similarly to MP (IStoreSettingsParent is the closest to what we're dealing with in here, but there's not much difference)
        private static void ThingFilter_AllowCategory_HelperRestaurant(MapComponent controller, ThingCategoryDef categoryDef, bool allow)
        {
            var menu = controllerMenuField.GetValue(controller);
            var filter = menuFilterField.GetValue(menu);
            var parentFilter = menuGlobalFilterField.GetValue(menu);
            var hiddenSpecialFilter = hiddenSpecialThingFiltersMethod.Invoke(null, Array.Empty<object>());

            allowCategoryHelperMethod.Invoke(null, new object[] { filter, categoryDef, allow, parentFilter, null, hiddenSpecialFilter });
        }

        private static void SyncDebt(SyncWorker sync, ref object obj)
        {
            var controller = Find.CurrentMap.GetComponent(restaurantControllerType);

            if (sync.isWriting)
            {
                var debt = controllerDebtField.GetValue(controller);
                var allDebts = restaurantDebtAllDebtsField.GetValue(debt) as IList;

                sync.Write(allDebts.IndexOf(obj));
            }
            else
            {
                var index = sync.Read<int>();

                if (index >= 0)
                {
                    var debt = controllerDebtField.GetValue(controller);
                    var allDebts = restaurantDebtAllDebtsField.GetValue(debt) as IList;

                    obj = allDebts[index];
                }
            }
        }
    }
}
