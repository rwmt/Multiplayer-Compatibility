using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Multiplayer.API;
using Outposts;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Multiplayer.Compat
{
    /// <summary>Vanilla Expanded Framework and other Vanilla Expanded mods by Oskar Potocki, Sarg Bjornson, Chowder, XeoNovaDan, Orion, Kikohi, erdelf, Taranchuk, and more</summary>
    /// <see href="https://github.com/Vanilla-Expanded/VanillaExpandedFramework"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2023507013"/>
    [MpCompatFor("OskarPotocki.VanillaFactionsExpanded.Core")]
    internal class VanillaExpandedFrameworkReferenced
    {
        public VanillaExpandedFrameworkReferenced(ModContentPack mod)
        {
            // Outposts
            {
                // Create dialog
                MpCompat.harmony.Patch(AccessTools.Method(typeof(Dialog_CreateCamp), nameof(Dialog_CreateCamp.DoOutpostDisplay)),
                    prefix: new HarmonyMethod(typeof(VanillaExpandedFrameworkReferenced), nameof(PreDoOutpostDisplay)));
                MP.RegisterSyncMethod(typeof(VanillaExpandedFrameworkReferenced), nameof(SyncedCreateOutpost));

                // Take items dialog
                MpCompat.harmony.Patch(AccessTools.Method(typeof(Dialog_TakeItems), nameof(Dialog_TakeItems.DoBottomButtons)),
                    prefix: new HarmonyMethod(typeof(VanillaExpandedFrameworkReferenced), nameof(PreTakeItemsDoBottomButtons)));
                MP.RegisterSyncMethod(typeof(VanillaExpandedFrameworkReferenced), nameof(SyncedTakeItems));

                // Give items dialog
                MpCompat.harmony.Patch(AccessTools.Method(typeof(Dialog_GiveItems), nameof(Dialog_GiveItems.DoBottomButtons)),
                    prefix: new HarmonyMethod(typeof(VanillaExpandedFrameworkReferenced), nameof(PreGiveItemsDoBottomButtons)));
                MP.RegisterSyncMethod(typeof(VanillaExpandedFrameworkReferenced), nameof(SyncedGiveItems));

                // Generic outpost
                // Stop packing (0), pack (1), pick colony to deliver to (8), and (dev) produce now (9), random pawn takes 10 damage (10), all pawns become hungry (11), pack instantly (12)
                MpCompat.RegisterLambdaMethod(typeof(Outpost), nameof(Outpost.GetGizmos), 0, 1, 8, 9, 10, 11, 12).Reverse().Take(4).SetDebugOnly();
                // Pick delivery method (8)
                MpCompat.RegisterLambdaDelegate(typeof(Outpost), nameof(Outpost.GetGizmos), 8);
                // Remove pawn from outpost/create caravan (4), needs a slight workaround as pawn is inacessible
                var innerMethod = MpMethodUtil.GetLambda(typeof(Outpost), nameof(Outpost.GetGizmos), lambdaOrdinal: 4);
                var outpostsInnerClassThisField = AccessTools.FieldRefAccess<Outpost>(innerMethod.DeclaringType, "<>4__this");
                MP.RegisterSyncDelegate(typeof(Outpost), innerMethod.DeclaringType!.Name, innerMethod.Name)
                    .TransformField("p", Serializer.New(
                        (p, instance, _) => (outpostsInnerClassThisField(instance), p.thingIDNumber),
                        ((Outpost o, int pawnId) tuple) => tuple.o.AllPawns.FirstOrDefault(p => p.thingIDNumber == tuple.pawnId)));

                MP.RegisterSyncWorker<ResultOption>(SyncResultOption);
                // Add pawn to outpost
                MpCompat.RegisterLambdaDelegate(typeof(Outpost), nameof(Outpost.GetCaravanGizmos), 2);

                // Outpost with results you can choose from
                MpCompat.RegisterLambdaDelegate("Outposts.Outpost_ChooseResult", "GetGizmos", 2);

                // Outpost gear tab
                MP.RegisterSyncWorker<WITab_Outpost_Gear>(SyncWITabOutpostGear);

                // Equip item from outpost's WITab
                // This method equips dragged item if it's apparel.
                // If it's a weapon/equipment, it'll call TryEquipDraggedItem_Equipment (after a potential confirmation dialog).
                var method = AccessTools.DeclaredMethod(typeof(WITab_Outpost_Gear), nameof(WITab_Outpost_Gear.TryEquipDraggedItem));
                MP.RegisterSyncMethod(MpMethodUtil.MethodOf(SyncedTryEquipDraggedItem)).SetContext(SyncContext.WorldSelected);
                // We need to clear the dragged item, as the synced method won't be able to.
                MpCompat.harmony.Patch(method,
                    prefix: new HarmonyMethod(MpMethodUtil.MethodOf(PreTryEquipDraggedItem)),
                    finalizer: new HarmonyMethod(MpMethodUtil.MethodOf(ClearDraggedItem)));

                // Equip weapon from outpost's WItab
                method = AccessTools.DeclaredMethod(typeof(WITab_Outpost_Gear), nameof(WITab_Outpost_Gear.TryEquipDraggedItem_Equipment));
                MP.RegisterSyncMethod(method).SetContext(SyncContext.WorldSelected)
                    // Need to sync as ID, since the pawn will be inaccessible
                    .TransformArgument(0, Serializer.New<Pawn, int>
                    (
                        p => p.thingIDNumber,
                        id => ((Outpost)Find.WorldSelector.SingleSelectedObject).AllPawns.FirstOrDefault(p => p.thingIDNumber == id)
                    ))
                    // The thing will be inaccessible. We sync it as null and assign it in
                    // SetupArgumentFromDraggedItem, as the Thing (before casting) is synced
                    // in SyncWITabOutpostGear - basically don't sync same thing twice.
                    .TransformArgument(1, Serializer.SimpleReader(() => (ThingWithComps)null));
                // We need to set the second argument to draggedItem (after casting).
                // We need to clear the dragged item, as the synced method won't be able to.
                MpCompat.harmony.Patch(method,
                    prefix: new HarmonyMethod(MpMethodUtil.MethodOf(SetupArgumentFromDraggedItem)),
                    finalizer: new HarmonyMethod(MpMethodUtil.MethodOf(ClearDraggedItem)));

                method = AccessTools.DeclaredMethod(typeof(WITab_Outpost_Gear), nameof(WITab_Outpost_Gear.MoveDraggedItemToInventory));
                MP.RegisterSyncMethod(method).SetContext(SyncContext.WorldSelected);
                // We need to clear the dragged item, as the synced method won't be able to.
                MpCompat.harmony.Patch(method,
                    finalizer: new HarmonyMethod(MpMethodUtil.MethodOf(ClearDraggedItem)));
            }
        }

        private static void SyncResultOption(SyncWorker sync, ref ResultOption option)
        {
            // There's more stuff in the class, but for syncing (at least right now), this is all we need
            if (sync.isWriting) sync.Write(option.Thing);
            else
            {
                option = new ResultOption()
                {
                    Thing = sync.Read<ThingDef>()
                };
            }
        }

        // Basically replace the original method with our own, that's basically the same
        // (with the main difference being how we are handling the button)
        private static bool PreDoOutpostDisplay(ref Rect inRect, WorldObjectDef outpostDef, Dialog_CreateCamp __instance)
        {
            if (!MP.IsInMultiplayer)
                return true;

            var font = Text.Font;
            var anchor = Text.Anchor;
            Text.Font = GameFont.Tiny;
            inRect.height = Text.CalcHeight(outpostDef.description, inRect.width - 90f) + 60f;
            var outerRect = inRect.LeftPartPixels(50f);
            var rect = inRect.RightPartPixels(inRect.width - 60f);
            var expandingIconTexture = outpostDef.ExpandingIconTexture;
            GUI.color = __instance.creator.Faction.Color;
            Widgets.DrawTextureFitted(outerRect, expandingIconTexture, 1f, new Vector2(expandingIconTexture.width, expandingIconTexture.height), new Rect(0f, 0f, 1f, 1f));
            GUI.color = Color.white;
            Text.Font = GameFont.Medium;
            Widgets.Label(rect.TopPartPixels(30f), outpostDef.label.CapitalizeFirst(outpostDef));
            var rect2 = rect.BottomPartPixels(30f).LeftPartPixels(100f);
            var rect3 = rect.BottomPartPixels(30f).RightPartPixels(rect.width - 120f);
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(rect.x, rect.y + 30f, rect.width, rect.height - 60f), outpostDef.description);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(rect3, __instance.validity[outpostDef].First);
            Text.Font = font;
            Text.Anchor = anchor;

            if (Widgets.ButtonText(rect2, "Outposts.Dialog.Create".Translate()))
            {
                if (__instance.validity[outpostDef].First.NullOrEmpty())
                {
                    // If the button was clicked and the outpost is valid, call the synced creation method
                    SyncedCreateOutpost(outpostDef, __instance.creator);
                    __instance.Close();
                    Find.WorldSelector.Deselect(__instance.creator);
                }
                else
                    Messages.Message(__instance.validity[outpostDef].First, MessageTypeDefOf.RejectInput, false);
            }

            TooltipHandler.TipRegion(inRect, __instance.validity[outpostDef].Second);

            return false;
        }

        private static void SyncedCreateOutpost(WorldObjectDef outpostDef, Caravan creator)
        {
            var outpost = (Outpost)WorldObjectMaker.MakeWorldObject(outpostDef);
            outpost.Name = NameGenerator.GenerateName(creator.Faction.def.settlementNameMaker,
                Find.WorldObjects.AllWorldObjects.OfType<Outpost>().Select(o => o.Name));
            outpost.Tile = creator.Tile;
            outpost.SetFaction(creator.Faction);
            Find.WorldObjects.Add(outpost);

            foreach (var pawn in creator.PawnsListForReading.ListFullCopy()) outpost.AddPawn(pawn);

            // Normally we would select the outpost here, but I'm not sure how
            // we could accomplish this ONLY for the player calling the method
        }

        // Basically replace the original method with our own, that's basically the same
        // (with the main difference being how we are handling the button)
        private static bool PreTakeItemsDoBottomButtons(Rect rect, Dialog_TakeItems __instance)
        {
            if (!MP.IsInMultiplayer)
                return true;

            var rect2 = new Rect(rect.width - __instance.BottomButtonSize.x, rect.height - 40f, __instance.BottomButtonSize.x, __instance.BottomButtonSize.y);

            if (Widgets.ButtonText(rect2, "Outposts.Take".Translate()))
            {
                var thingsToTransfer = __instance.transferables
                    .Where(x => x.HasAnyThing && x.CountToTransfer > 0)
                    .ToDictionary(x => x.ThingDef, x => x.CountToTransfer);

                SyncedTakeItems(__instance.outpost, __instance.caravan, thingsToTransfer);
                __instance.Close();
            }

            if (Widgets.ButtonText(new Rect(0f, rect2.y, __instance.BottomButtonSize.x, __instance.BottomButtonSize.y), "CancelButton".Translate()))
                __instance.Close();

            if (Widgets.ButtonText(new Rect(rect.width / 2f - __instance.BottomButtonSize.x, rect2.y, __instance.BottomButtonSize.x, __instance.BottomButtonSize.y), "ResetButton".Translate()))
            {
                SoundDefOf.Tick_Low.PlayOneShotOnCamera();
                __instance.CalculateAndRecacheTransferables();
            }

            return false;
        }

        private static void SyncedTakeItems(Outpost outpost, Caravan caravan, Dictionary<ThingDef, int> itemsToTransfer)
        {
            var dummyDialog = new Dialog_TakeItems(outpost, caravan);
            dummyDialog.CalculateAndRecacheTransferables();

            foreach (var transferable in dummyDialog.transferables)
            {
                if (transferable.HasAnyThing && itemsToTransfer.TryGetValue(transferable.ThingDef, out var count))
                    transferable.ForceTo(count);

                while (transferable.HasAnyThing && transferable.CountToTransfer > 0)
                {
                    var thing = transferable.things.Pop();

                    if (thing.stackCount <= transferable.CountToTransfer)
                    {
                        transferable.AdjustBy(-thing.stackCount);
                        caravan.AddPawnOrItem(outpost.TakeItem(thing), true);
                    }
                    else
                    {
                        caravan.AddPawnOrItem(thing.SplitOff(transferable.CountToTransfer), true);
                        transferable.AdjustTo(0);
                        transferable.things.Add(thing);
                    }
                }
            }
        }

        // Basically replace the original method with our own, that's basically the same
        // (with the main difference being how we are handling the button)
        private static bool PreGiveItemsDoBottomButtons(Rect rect, Dialog_GiveItems __instance)
        {
            if (!MP.IsInMultiplayer)
                return true;

            var rect2 = new Rect(rect.width - __instance.BottomButtonSize.x, rect.height - 40f, __instance.BottomButtonSize.x, __instance.BottomButtonSize.y);

            if (Widgets.ButtonText(rect2, "Outposts.Give".Translate()))
            {
                var thingsToTransfer = __instance.transferables
                    .Where(x => x.HasAnyThing && x.CountToTransfer > 0)
                    .ToDictionary(x => x.ThingDef, x => x.CountToTransfer);

                SyncedGiveItems(__instance.outpost, __instance.caravan, thingsToTransfer);
                __instance.Close();
            }

            if (Widgets.ButtonText(new Rect(0f, rect2.y, __instance.BottomButtonSize.x, __instance.BottomButtonSize.y), "CancelButton".Translate()))
                __instance.Close();

            if (Widgets.ButtonText(new Rect(rect.width / 2f - __instance.BottomButtonSize.x, rect2.y, __instance.BottomButtonSize.x, __instance.BottomButtonSize.y), "ResetButton".Translate()))
            {
                SoundDefOf.Tick_Low.PlayOneShotOnCamera();
                __instance.CalculateAndRecacheTransferables();
            }

            return false;
        }

        private static void SyncedGiveItems(Outpost outpost, Caravan caravan, Dictionary<ThingDef, int> itemsToTransfer)
        {
            var dummyDialog = new Dialog_GiveItems(outpost, caravan);
            dummyDialog.CalculateAndRecacheTransferables();

            foreach (var transferable in dummyDialog.transferables)
            {
                if (transferable.HasAnyThing && itemsToTransfer.TryGetValue(transferable.ThingDef, out var count))
                    transferable.ForceTo(count);

                while (transferable.HasAnyThing && transferable.CountToTransfer > 0)
                {
                    var thing = transferable.things.Pop();

                    if (thing.stackCount <= transferable.CountToTransfer)
                    {
                        transferable.AdjustBy(-thing.stackCount);
                        outpost.AddItem(thing);
                    }
                    else
                    {
                        outpost.AddItem(thing.SplitOff(transferable.CountToTransfer));
                        transferable.AdjustTo(0);
                        transferable.things.Add(thing);
                    }
                }
            }
        }

        private static void SyncWITabOutpostGear(SyncWorker sync, ref WITab_Outpost_Gear tab)
        {
            if (sync.isWriting)
            {
                // Will be inaccessible, need to sync using ID number
                sync.Write(tab.draggedItem.thingIDNumber);
            }
            else
            {
                var id = sync.Read<int>();

                tab = new WITab_Outpost_Gear();
                // This requires SyncContext.WorldSelected.
                // We could have synced the selected outpost itself, as well,
                // but the methods will SyncContext.WorldSelected, so there's no point.
                tab.draggedItem = tab.SelOutpost.Things.FirstOrDefault(t => t.thingIDNumber == id);
            }
        }

        // A call to TryEquipDraggedItem, but synced.
        private static void SyncedTryEquipDraggedItem(WITab_Outpost_Gear tab, int pawnId)
        {
            var pawn = tab.SelOutpost.AllPawns.FirstOrDefault(p => p.thingIDNumber == pawnId);
            if (pawn != null)
                tab.TryEquipDraggedItem(pawn);
        }

        private static bool PreTryEquipDraggedItem(WITab_Outpost_Gear __instance, Pawn p)
        {
            // Let it run out of MP/if synced.
            if (!MP.IsInMultiplayer || MP.IsExecutingSyncCommand)
                return true;

            // Let it run if not apparel (weapon/equipment), as it'll potentially create
            // a confirmation dialog and call TryEquipDraggedItem_Equipment (which we sync).
            if (p.apparel == null || __instance.draggedItem is not Apparel)
                return true;

            // Cancel and sync if it's apparel and the pawn has apparel tracker.
            // The method itself is handling the equipping interaction, so we need
            // to sync it instead.
            SyncedTryEquipDraggedItem(__instance, p.thingIDNumber);
            return false;
        }

        private static bool SetupArgumentFromDraggedItem(ref ThingWithComps eq, Thing ___draggedItem)
        {
            if (!MP.IsInMultiplayer || !MP.IsExecutingSyncCommand)
                return true;

            // If (for whatever reason) not ThingWithComps, cancel execution. Will likely cause issues otherwise.
            if (___draggedItem is not ThingWithComps thing)
                return false;

            eq = thing;
            return true;
        }

        private static void ClearDraggedItem(ref Thing ___draggedItem) => ___draggedItem = null;
    }
}