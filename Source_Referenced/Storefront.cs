using HarmonyLib;
using Multiplayer.API;
using Storefront.Store;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Hospitality: Storefront by Adamas</summary>
    /// <see href="https://github.com/tomvd/Storefront"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2952321484"/>
    [MpCompatFor("Adamas.Storefront")]
    public class Storefront
    {
        private static ISyncField openForBusinessField;
        private static ISyncField guestPricePercentageField;

        public Storefront(ModContentPack mod)
        {
            var type = typeof(StoreController);
            openForBusinessField = MP.RegisterSyncField(type, nameof(StoreController.openForBusiness));
            guestPricePercentageField = MP.RegisterSyncField(type, nameof(StoreController.guestPricePercentage));
            MP.RegisterSyncMethod(type, nameof(StoreController.Name));
            MP.RegisterSyncMethod(type, "LinkRegister");
            MP.RegisterSyncWorker<StoreController>(SyncStoreController, type);

            type = typeof(ITab_Register_Store);
            MpCompat.harmony.Patch(AccessTools.DeclaredMethod(type, nameof(InspectTabBase.FillTab)),
                prefix: new HarmonyMethod(typeof(Storefront), nameof(PreFillTab)),
                finalizer: new HarmonyMethod(typeof(Storefront), nameof(PostFillTab)));
        }

        private static void SyncStoreController(SyncWorker sync, ref StoreController controller)
        {
            if (sync.isWriting)
            {
                var comp = controller.Map?.GetComponent<StoresManager>();
                var index = comp?.Stores.IndexOf(controller) ?? -1;

                sync.Write(index);
                if (index >= 0)
                    sync.Write(comp);
            }
            else
            {
                var index = sync.Read<int>();
                if (index >= 0)
                {
                    var manager = sync.Read<StoresManager>();
                    if (manager.Stores.Count < index)
                        controller = manager.Stores[index];
                }
            }
        }

        private static void PreFillTab(ITab_Register_Store __instance, out bool __state)
        {
            if (!MP.IsInMultiplayer || __instance.store == null)
            {
                __state = false;
                return;
            }

            MP.SetThingFilterContext(new StoreWrapper(__instance.store));
            
            MP.WatchBegin();
            openForBusinessField.Watch(__instance.store);
            guestPricePercentageField.Watch(__instance.store);

            __state = true;
        }

        private static void PostFillTab(bool __state)
        {
            if (!__state)
                return;

            MP.SetThingFilterContext(null);
            MP.WatchEnd();
        }

        private record StoreWrapper(StoreController Controller) : ThingFilterContext
        {
            public override ThingFilter Filter => Controller.GetStoreFilter();
            public override ThingFilter ParentFilter => null;

            public StoreController Controller { get; } = Controller;
        }
    }
}