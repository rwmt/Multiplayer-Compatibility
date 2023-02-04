using System;
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
        private static AccessTools.FieldRef<object, Outpost> outpostsInnerClassThisField;
        private static AccessTools.FieldRef<object, Pawn> outpostsInnerClassPawnField;

        public VanillaExpandedFrameworkReferenced(ModContentPack mod)
        {
            // Outposts
            {
                // Create dialog
                MpCompat.harmony.Patch(AccessTools.Method(typeof(Dialog_CreateCamp), nameof(Dialog_CreateCamp.DoOutpostDisplay)),
                    prefix: new HarmonyMethod(typeof(VanillaExpandedFrameworkReferenced), nameof(PreDoOutpostDisplay)));
                MP.RegisterSyncMethod(typeof(VanillaExpandedFrameworkReferenced), nameof(SyncedCreateOutpost));

                // Rename dialog
                MP.RegisterSyncWorker<Dialog_RenameOutpost>(SyncRenameOutpostDialog, typeof(Dialog_RenameOutpost));
                MP.RegisterSyncMethod(typeof(Dialog_RenameOutpost), nameof(Dialog_RenameOutpost.SetName));

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
                // Pick delivery method
                MpCompat.RegisterLambdaDelegate(typeof(Outpost), nameof(Outpost.GetGizmos), 8);
                // Remove pawn from outpost/create caravan (delegate, 4)
                // We need a slight workaround, as the gizmo itself won't work - the pawn is inaccessible for syncing
                var innerMethod = MpMethodUtil.GetLambda(typeof(Outpost), nameof(Outpost.GetGizmos), lambdaOrdinal: 4);
                if (innerMethod == null)
                    Log.Error("Cannot find the inner class for Vanilla Expanded Framework - Outposts module. Removing pawns from outposts 1 at a time will desync.");
                else
                {
                    var fields = AccessTools.GetDeclaredFields(innerMethod.DeclaringType);
                    if (fields.Count < 2)
                        Log.Error($"The inner class for Vanilla Expanded Framework - Outposts module had {fields.Count} fields, expected 2. There was most likely an update and this patch needs fixing. Removing pawns from outposts 1 at a time will desync.");
                    else
                    {
                        if (fields.Count > 2)
                            Log.Error($"The inner class for Vanilla Expanded Framework - Outposts module had {fields.Count} fields, expected 2. There was most likely an update and this patch needs fixing. Removing pawns from outposts 1 at a time could possibly desync.");

                        try
                        {
                            outpostsInnerClassThisField = AccessTools.FieldRefAccess<Outpost>(
                                innerMethod.DeclaringType, 
                                fields.FirstOrDefault(x => x.FieldType == typeof(Outpost))?.Name ?? "<>4__this");
                            outpostsInnerClassPawnField = AccessTools.FieldRefAccess<Pawn>(
                                innerMethod.DeclaringType, 
                                fields.FirstOrDefault(x => x.FieldType == typeof(Pawn))?.Name ?? "p");
                        }
                        catch (Exception)
                        {
                            Log.Error("Couldn't setup sync using the inner class for Vanilla Expanded Framework - Outposts module. Removing pawns from outposts 1 at a time will desync.");
                        }
                    }
                }
                MpCompat.harmony.Patch(innerMethod, 
                    prefix: new HarmonyMethod(typeof(VanillaExpandedFrameworkReferenced), nameof(PreRemoveFromOutpost)));
                MP.RegisterSyncMethod(typeof(VanillaExpandedFrameworkReferenced), nameof(SyncedRemoveFromOutpost));
                MP.RegisterSyncWorker<ResultOption>(SyncResultOption);
                // Add pawn to outpost
                MpCompat.RegisterLambdaDelegate(typeof(Outpost), nameof(Outpost.GetCaravanGizmos), 2);

                // Outpost with results you can choose from
                MpCompat.RegisterLambdaDelegate("Outposts.Outpost_ChooseResult", "GetGizmos", 2);
            }
        }

        private static void SyncRenameOutpostDialog(SyncWorker sync, ref Dialog_RenameOutpost dialog)
        {
            if (sync.isWriting) sync.Write(dialog.outpost as MapParent);
            else
            {
                var outpost = sync.Read<MapParent>() as Outpost;
                dialog = new Dialog_RenameOutpost(outpost);
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

        private static bool PreRemoveFromOutpost(object __instance)
        {
            if (outpostsInnerClassThisField == null || outpostsInnerClassPawnField == null)
            {
                Log.Error("Removing pawns from outposts while setting it up failed - the game will now most likely desync!");
                // It'll cause a desync because we let it run, but it'll at least
                // let people playing use this feature and then resync.
                // Shouldn't have too much of an impact.
                return true;
            }
            
            var outpost = outpostsInnerClassThisField(__instance);
            var pawn = outpostsInnerClassPawnField(__instance);

            // The pawn is normally inaccessible while in outpost (can't sync them),
            // so we use a workaround to get them
            var index = outpost.occupants.IndexOf(pawn);
            SyncedRemoveFromOutpost(outpost, index);

            return false;
        }

        private static void SyncedRemoveFromOutpost(Outpost outpost, int pawnIndex)
        {
            if (outpost.occupants.Count <= pawnIndex)
            {
                Log.Error($"Trying to remove a pawn with index {pawnIndex}, but there only {outpost.occupants.Count} pawns in the outpost {outpost.Name}!");
                return;
            }

            var pawn = outpost.occupants[pawnIndex];
            CaravanMaker.MakeCaravan(Gen.YieldSingle(outpost.RemovePawn(pawn)), pawn.Faction, outpost.Tile, true);
        }
	}
}
