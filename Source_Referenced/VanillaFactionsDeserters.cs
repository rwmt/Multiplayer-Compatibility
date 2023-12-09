using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using RimWorld.QuestGen;
using UnityEngine;
using Verse;
using Verse.Sound;
using VFED;
using VFEEmpire;

namespace Multiplayer.Compat
{
    /// <summary>Vanilla Factions Expanded - Deserters by Oskar Potocki, xrushha, legodude17</summary>
    /// <see href="https://github.com/Vanilla-Expanded/VanillaFactionsExpanded-Deserters"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3025493377"/>
    [MpCompatFor("OskarPotocki.VFE.Deserters")]
    public class VanillaFactionsDeserters
    {
        #region Init

        // Dialog_DeserterNetwork
        private static float nextRecacheTime = 0;
        // DeserterTabWorker_Plots
        private static int currentApproachIndex = -1;

        public VanillaFactionsDeserters(ModContentPack mod)
        {
            // Gizmos
            {
                #region Gizmos

                // Remote detonate/trigger
                MpCompat.RegisterLambdaMethod(typeof(Building_BombPackDeployed), nameof(Building.GetGizmos), 0);
                MpCompat.RegisterLambdaMethod(typeof(Building_RemoteTrap), nameof(Building.GetGizmos), 0);

                // Deploy
                MpCompat.RegisterLambdaMethod(typeof(CompDeployable), nameof(ThingComp.CompGetWornGizmosExtra), 0);

                // Add intel extraction designation
                MP.RegisterSyncMethod(typeof(CompIntelExtract), nameof(CompIntelExtract.AddDesignation));
                // Add intel extraction designation and order take the job
                MpCompat.RegisterLambdaDelegate(typeof(CompIntelExtract), nameof(CompIntelExtract.CompFloatMenuOptions), 0);

                // Disable/enable (0/1)
                MpCompat.RegisterLambdaMethod(typeof(CompIntelScraper), nameof(ThingComp.CompGetGizmosExtra), 0, 1);

                // Enable invisibility
                MP.RegisterSyncMethod(typeof(CompInvisibilityEngulfer), nameof(CompInvisibilityEngulfer.Activate));

                // In vanilla, doesn't need syncing. With this mod it's called directly by a gizmo.
                // Alternative approach would involve patching the mod itself, and replacing the action
                // for the command with our own, synced one.
                // Also, make sure to sync the method for ShipJob_WaitSendable, not ShipJob_Wait.
                MP.RegisterSyncMethod(typeof(ShipJob_WaitSendable), nameof(ShipJob_WaitSendable.SendAway));

                // VFED.CompIntelExtractor should be synced through MP's handling of Pawn_JobTracker.TryTakeOrderedJob
                // If this changes in the future (extra code, etc.) sync lambda for `CompGetWornGizmosExtra` and ordinal 0.

                #endregion
            }

            // Dialogs
            {
                #region Other dialogs

                // Dialog_NodeTree
                MP.RegisterSyncDialogNodeTree(typeof(IncidentWorker_ImperialPatrol), nameof(IncidentWorker.TryExecuteWorker));

                #endregion

                #region Deserter network dialog

                #region Re-cache

                // Re-cache intel amount
                // Since we change how it's handled by allowing players to open the dialog locally without forcing pause,
                // we need to re-cache the value in case it ends up changing due to decay/other player actions.
                MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(Dialog_DeserterNetwork), nameof(Dialog_Debug.DoWindowContents)),
                    prefix: new HarmonyMethod(typeof(VanillaFactionsDeserters), nameof(PreDeserterNetworkDraw)));

                #endregion

                #region Shared

                // Replace the code handling purchase button with our own, so we can properly sync stuff
                MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(DeserterTabWorker_Services), nameof(DeserterTabWorker_Services.DrawService)),
                    transpiler: new HarmonyMethod(typeof(VanillaFactionsDeserters), nameof(ReplaceServicePurchaseButton)));
                // Sync the possible results for generic purchase button
                MP.RegisterSyncMethod(typeof(VanillaFactionsDeserters), nameof(SyncedPurchaseContraband));
                MP.RegisterSyncMethod(typeof(VanillaFactionsDeserters), nameof(SyncedPurchaseContrabandRushedDelivery));
                MP.RegisterSyncMethod(typeof(VanillaFactionsDeserters), nameof(SyncedPurchaseQuest)).CancelIfAnyArgNull();
                MP.RegisterSyncMethod(typeof(VanillaFactionsDeserters), nameof(SyncedPurchaseService));

                // Patch the purchase dialog so we catch the button press and sync it
                MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(DesertersUIUtility), nameof(DesertersUIUtility.DoPurchaseButton)),
                    prefix: new HarmonyMethod(typeof(VanillaFactionsDeserters), nameof(PreDoPurchaseButton)));

                // Patch dialog opening to only open for current player
                MpCompat.harmony.Patch(MpMethodUtil.GetLambda(typeof(WorldComponent_Deserters), nameof(WorldComponent_Deserters.CommFloatMenuOption), lambdaOrdinal: 0),
                    prefix: new HarmonyMethod(typeof(VanillaFactionsDeserters), nameof(PreTryOpenDeserterDialog)));

                // Ensure the quest list, plots, etc. are always generated so when we open the dialog it won't cause issues.
                MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(GameComponentUtility), nameof(GameComponentUtility.FinalizeInit)),
                    postfix: new HarmonyMethod(typeof(VanillaFactionsDeserters), nameof(EnsureDeserterStuffPreGenerated)));

                #endregion

                #region Plots

                // Sync the method for generating plots.
                // Shouldn't ever be needed as it's only called if they are not generated (which we handle in GameComponentUtility.FinalizeInit),
                // but sync it just in case it ever gets called.
                MP.RegisterSyncMethod(typeof(WorldComponent_Deserters), nameof(WorldComponent_Deserters.InitializePlots));

                MP.RegisterSyncMethod(typeof(VanillaFactionsDeserters), nameof(SyncedAcceptPlot));

                // Since we're patching a method that accesses classes that are (correctly) marked with `StaticConstructorOnStartup`,
                // we need to make sure to do it in a late patch to prevent issues with missing textures and graphics.
                // This could go into a late patch, but it just felt more convenient to have it here instead of breaking file's structure.
                LongEventHandler.ExecuteWhenFinished(() =>
                {
                    // Catch the method accepting the final quest to sync it.
                    // Could technically be a SyncMethod, but it would also require a SyncWorker, and would add on more complexity.
                    MpCompat.harmony.Patch(MpMethodUtil.GetLambda(typeof(DeserterTabWorker_Plots), nameof(DeserterTabWorker_Plots.DoMainPart), lambdaOrdinal: 0),
                        prefix: new HarmonyMethod(typeof(VanillaFactionsDeserters), nameof(PreAcceptFinalQuest)));

                    // Intercept the button to accept a quest approach
                    MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(DeserterTabWorker_Plots), nameof(DeserterTabWorker_Plots.DoMainPart)),
                        prefix: new HarmonyMethod(typeof(VanillaFactionsDeserters), nameof(ResetApproachIndex)),
                        transpiler: new HarmonyMethod(typeof(VanillaFactionsDeserters), nameof(ReplaceAcceptPlotButton)));
                });

                #endregion

                #region Services

                // Sync the method for generating quests in service tab.
                // Only called when the tab is opened (Notify_Open), or when we should be in the sync method anyway (SyncedPurchaseService).
                MP.RegisterSyncMethod(typeof(WorldComponent_Deserters), nameof(WorldComponent_Deserters.EnsureQuestListFilled));
                // The check if the plots should be generated is handled in the tab's Notify_Open, but we call it more often than that.
                // Ensure that it never ends up generating more plots than necessary.
                MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(WorldComponent_Deserters), nameof(WorldComponent_Deserters.InitializePlots)),
                    prefix: new HarmonyMethod(typeof(VanillaFactionsDeserters), nameof(EnsurePlotsGeneratedWithoutRepeats)));

                #endregion

                #endregion
            }
        }

        #endregion

        #region Deserter network dialog

        #region Caching

        private static bool PreTryOpenDeserterDialog(Pawn ___negotiator)
        {
            // Let it run normally in SP
            if (!MP.IsInMultiplayer)
                return true;

            // Instead of giving the pawn job to go to the comms console and open the dialog, open it immediately instead.
            Find.WindowStack.Add(new Dialog_DeserterNetwork(___negotiator.MapHeld));
            return false;
        }

        // Recache the intel amount ~once per 1.5 seconds
        private static void PreDeserterNetworkDraw(ref int ___TotalIntel, ref int ___TotalCriticalIntel, Map ___Map)
        {
            if (Time.realtimeSinceStartup >= nextRecacheTime)
                RecacheDialogIntel(ref ___TotalIntel, ref ___TotalCriticalIntel, ___Map);
        }

        private static void RecacheDialogIntel(Dialog_DeserterNetwork dialog) => RecacheDialogIntel(ref dialog.TotalIntel, ref dialog.TotalCriticalIntel, dialog.Map);

        private static void RecacheDialogIntel(ref int totalIntel, ref int totalCriticalIntel, Map map)
        {
            (totalIntel, totalCriticalIntel) = GetIntelCountForMap(map);
            nextRecacheTime = Time.realtimeSinceStartup + 1.5f;
        }

        #endregion

        #region Utilities

        private static (int intel, int criticalIntel) GetIntelCountForMap(Map map)
        {
            var totalIntel = 0;
            var totalCriticalIntel = 0;

            foreach (var intVec in Building_OrbitalTradeBeacon.AllPowered(map).SelectMany(x => x.TradeableCells).Distinct())
            {
                foreach (var thing in intVec.GetThingList(map))
                {
                    if (thing.def == VFED_DefOf.VFED_Intel)
                        totalIntel += thing.stackCount;
                    if (thing.def == VFED_DefOf.VFED_CriticalIntel)
                        totalCriticalIntel += thing.stackCount;
                }
            }

            return (totalIntel, totalCriticalIntel);
        }

        private static bool TrySpendIntel(int intelCost, bool useCritical, Map map)
        {
            if (useCritical)
                return TrySpendIntel(0, intelCost, map);
            return TrySpendIntel(intelCost, 0, map);
        }

        private static bool TrySpendIntel(int normalIntelCost, int criticalIntelCost, Map map)
        {
            var (normalIntel, criticalIntel) = GetIntelCountForMap(map);

            if (normalIntel < normalIntelCost)
            {
                if (MP.IsExecutingSyncCommandIssuedBySelf)
                    Messages.Message("VFED.NotEnough".Translate(VFED_DefOf.VFED_Intel.LabelCap, normalIntelCost, normalIntel), MessageTypeDefOf.RejectInput, false);
                return false;
            }

            if (criticalIntel < criticalIntelCost)
            {
                if (MP.IsExecutingSyncCommandIssuedBySelf)
                    Messages.Message("VFED.NotEnough".Translate(VFED_DefOf.VFED_CriticalIntel.LabelCap, criticalIntelCost, criticalIntel), MessageTypeDefOf.RejectInput, false);
                return false;
            }

            TradeUtility.LaunchThingsOfType(VFED_DefOf.VFED_Intel, normalIntelCost, map, null);
            TradeUtility.LaunchThingsOfType(VFED_DefOf.VFED_CriticalIntel, criticalIntelCost, map, null);

            // Try to re-cache the dialog
            var currentDialog = Find.WindowStack.WindowOfType<Dialog_DeserterNetwork>();
            if (currentDialog != null && currentDialog.Map == map)
                RecacheDialogIntel(currentDialog);

            return true;
        }

        private static List<(ThingDef thing, int count)> ShoppingCartToList()
        {
            // In our list the first item is ThingDef, and the second is DefModExtension's count multiplier * the amount of item purchased.
            return ContrabandManager.ShoppingCart.Select(x => (x.Key.Item1, x.Key.Item2.countMult * x.Value)).ToList();
        }

        #endregion

        #region Shared

        // The purchase button is used by contraband tab to purchase contraband, and services tab to purchase quests.
        private static bool PreDoPurchaseButton(Rect inRect, string text, int intelCost, int criticalIntelCost, Dialog_DeserterNetwork parent, ref bool __result)
        {
            if (!MP.IsInMultiplayer)
                return true;

            if (intelCost > parent.TotalIntel || criticalIntelCost > parent.TotalCriticalIntel)
                GUI.color = Color.grey;

            if (Widgets.ButtonText(inRect, text))
            {
                RecacheDialogIntel(ref parent.TotalIntel, ref parent.TotalCriticalIntel, parent.Map);
                // Check if actually can afford after re-caching, will have to do again after syncing.
                if (intelCost <= parent.TotalIntel && criticalIntelCost <= parent.TotalCriticalIntel)
                {
                    SoundDefOf.ExecuteTrade.PlayOneShotOnCamera();

                    if (text == "VFED.Purchase".Translate())
                        SyncedPurchaseContraband(intelCost, criticalIntelCost, parent.Map, ShoppingCartToList(), ContrabandManager.TotalAmount);
                    else if (text == "VFED.RushDelivery".Translate())
                        SyncedPurchaseContrabandRushedDelivery(intelCost, criticalIntelCost, parent.Map, ShoppingCartToList());
                    else if (text == "VFED.Activate".Translate())
                        SyncedPurchaseQuest(intelCost, criticalIntelCost, parent.Map, (parent.curTab.Worker as DeserterTabWorker_Services)?.selected);
                    else
                        Log.Error($"Deserter network dialog - unsupported purchase button. Button's text: {text}");
                }
            }

            GUI.color = Color.white;
            // Prevent the original from running and make sure it returned false
            __result = false;
            return false;
        }

        private static void EnsureDeserterStuffPreGenerated()
        {
            WorldComponent_Deserters.Instance.EnsureQuestListFilled();
            // Normally we'd need to check if there's no plots already, but we handle that
            // through the patch to InitializePlots method itself.
            WorldComponent_Deserters.Instance.InitializePlots();
        }

        #endregion

        #region Contraband

        private static void SyncedPurchaseContraband(int intelCost, int criticalIntelCost, Map targetMap, List<(ThingDef def, int count)> shoppingCart, int totalAmount)
        {
            if (!TrySpendIntel(intelCost, criticalIntelCost, targetMap))
                return;

            var slate = new Slate();
            slate.Set("delayTicks", Utilities.ReceiveTimeRange(totalAmount).RandomInRange.DaysToTicks());
            slate.Set("availableTime", Utilities.SiteExistTime(totalAmount).DaysToTicks());
            var things = new List<ThingDef>();
            foreach (var (thing, count) in shoppingCart)
                for (var i = count; i-- > 0;)
                    things.Add(thing);
            slate.Set("itemStashThings", things);
            QuestUtility.GenerateQuestAndMakeAvailable(VFED_DefOf.VFED_DeadDrop, slate);

            // Delay cart clearing to syncing in case we can't afford it
            if (MP.IsExecutingSyncCommandIssuedBySelf)
                ContrabandManager.ClearCart();
        }

        private static void SyncedPurchaseContrabandRushedDelivery(int intelCost, int criticalIntelCost, Map targetMap, List<(ThingDef def, int count)> shoppingCart)
        {
            if (!TrySpendIntel(intelCost, criticalIntelCost, targetMap))
                return;

            var things = new List<List<Thing>>();
            var curList = new List<Thing>();
            foreach (var (thing, count) in shoppingCart)
            {
                for (var i = count; i-- > 0;)
                {
                    curList.Add(ThingMaker.MakeThing(thing, thing.MadeFromStuff ? GenStuff.DefaultStuffFor(thing) : null).TryMakeMinified());
                    if (curList.Count > 10)
                    {
                        things.Add(curList);
                        curList = new List<Thing>();
                    }
                }
            }

            things.Add(curList);
            var cell = DropCellFinder.TradeDropSpot(targetMap);
            DropPodUtility.DropThingGroupsNear(cell, targetMap, things, canRoofPunch: false, allowFogged: false, forbid: false, faction: EmpireUtility.Deserters);
            Messages.Message("VFED.ContrabandArrived".Translate(), new TargetInfo(cell, targetMap), MessageTypeDefOf.PositiveEvent);

            // Delay cart clearing to syncing in case we can't afford it.
            if (MP.IsExecutingSyncCommandIssuedBySelf)
                ContrabandManager.ClearCart();
        }

        #endregion

        #region Plots

        private static bool EnsurePlotsGeneratedWithoutRepeats(WorldComponent_Deserters __instance) => __instance.PlotMissions.NullOrEmpty();

        private static void ResetApproachIndex() => currentApproachIndex = -1;
        
        private static bool ReplacedAcceptPlotButton(Rect rect, string label, bool drawBackground, bool doMouseoverSound, bool active, TextAnchor? overrideTextAnchor, DeserterTabWorker_Plots instance)
        {
            // Starts at -1, so for the first call we'll start with index 0.
            // Used to avoid making too many changes with the transpiler, as the instructions
            // we'd insert could easily break due to small changes in original code (local indices).
            currentApproachIndex++;
        
            var result = Widgets.ButtonText(rect, label, drawBackground, doMouseoverSound, active, overrideTextAnchor);
            if (!MP.IsInMultiplayer || !result)
                return result;

            SyncedAcceptPlot(instance.selectedPlot.quest, currentApproachIndex, instance.Parent.Map);
            // Return the button state as not pressed to original method
            return false;
        }

        private static bool PreAcceptFinalQuest(DeserterTabWorker_Plots __instance)
        {
            if (!MP.IsInMultiplayer)
                return true;

            // The final quest doesn't have approaches, so pass -1.
            SyncedAcceptPlot(__instance.selectedPlot.quest, -1, __instance.Parent.Map);
            return false;
        }

        private static void SyncedAcceptPlot(Quest quest, int approachIndex, Map map)
        {
            var questPart_ApproachChoices = quest.PartsListForReading.OfType<QuestPart_ApproachChoices>().FirstOrDefault();
            // Only do stuff related to approaches if the quest has them
            if (questPart_ApproachChoices != null)
            {
                if (approachIndex < 0 || approachIndex >= questPart_ApproachChoices.choices.Count)
                {
                    Log.Error($"Received approach index out of bounds (index={approachIndex}, count={questPart_ApproachChoices.choices.Count}) for quest {quest}");
                    return;
                }

                var choice = questPart_ApproachChoices.choices[approachIndex];
                if (!TrySpendIntel(choice.info.intelCost, choice.info.useCriticalIntel, map))
                    return;

                questPart_ApproachChoices.Choose(choice);
            }

            quest.hidden = false;
            quest.hiddenInUI = false;
            quest.Accept(null);

            if (MP.IsExecutingSyncCommandIssuedBySelf)
            {
                Find.WindowStack.WindowOfType<Dialog_DeserterNetwork>()?.Close();
                MainButtonDefOf.Quests.Worker.Activate();
                ((MainTabWindow_Quests)MainButtonDefOf.Quests.TabWindow).Select(quest);
            }
        }

        private static IEnumerable<CodeInstruction> ReplaceAcceptPlotButton(IEnumerable<CodeInstruction> instr, MethodBase baseMethod)
        {
            var target = AccessTools.DeclaredMethod(typeof(Widgets), nameof(Widgets.ButtonText),
                new[] { typeof(Rect), typeof(string), typeof(bool), typeof(bool), typeof(bool), typeof(TextAnchor?) });
            var replacement = AccessTools.DeclaredMethod(typeof(VanillaFactionsDeserters), nameof(ReplacedAcceptPlotButton));
        
            var replacedCount = 0;
            var lookForButtonMethod = false;
        
            foreach (var ci in instr)
            {
                if (lookForButtonMethod)
                {
                    if ((ci.opcode == OpCodes.Call || ci.opcode == OpCodes.Callvirt) && ci.operand is MethodInfo method && method == target)
                    {
                        ci.opcode = OpCodes.Call;
                        ci.operand = replacement;
                        replacedCount++;
                        
                        // Insert `this` (DeserterTabWorker_Plots)
                        yield return new CodeInstruction(OpCodes.Ldarg_0);
        
                        // Stop looking for the method, unless we encounter the string we look for again.
                        lookForButtonMethod = false;
                    }
                }
                // Make sure we only patch the button to accept the quest and nothing else.
                else if (ci.opcode == OpCodes.Ldstr && ci.operand is "VFED.Select")
                    lookForButtonMethod = true;
        
                yield return ci;
            }
        
            const int expected = 1;
            if (replacedCount != expected)
            {
                var name = (baseMethod.DeclaringType?.Namespace).NullOrEmpty() ? baseMethod.Name : $"{baseMethod.DeclaringType!.Name}:{baseMethod.Name}";
                Log.Warning($"Patched incorrect number of Widgets.ButtonText calls (patched {replacedCount}, expected {expected}) for method {name}");
            }
        }

        #endregion

        #region Service

        private static void SyncedPurchaseQuest(int intelCost, int criticalIntelCost, Map targetMap, Quest selected)
        {
            // Prevent repeated attempts to accept the quest
            if (selected.acceptanceTick != -1)
                return;

            if (!TrySpendIntel(intelCost, criticalIntelCost, targetMap))
                return;

            selected.hidden = false;
            selected.hiddenInUI = false;
            WorldComponent_Deserters.Instance.ServiceQuests.Remove(selected);
            WorldComponent_Deserters.Instance.EnsureQuestListFilled();
            selected.Accept(null);

            if (MP.IsExecutingSyncCommandIssuedBySelf)
            {
                Find.WindowStack.WindowOfType<Dialog_DeserterNetwork>()?.Close();
                MainButtonDefOf.Quests.Worker.Activate();
                ((MainTabWindow_Quests)MainButtonDefOf.Quests.TabWindow).Select(selected);
            }
        }

        private static bool ServicePurchaseButtonReplacement(Rect inRect, bool doMouseoverSound, DeserterServiceDef serviceDef, DeserterTabWorker instance)
        {
            var result = Widgets.ButtonInvisible(inRect, doMouseoverSound);

            // Ignore if not in MP as we don't handle it, or if it's false in which case we don't have anything to do.
            if (!MP.IsInMultiplayer || !result)
                return result;

            // Recache intel amount and check if we can afford it before syncing
            RecacheDialogIntel(instance.Parent);
            if (instance.Parent.HasIntel(Mathf.FloorToInt(serviceDef.intelCost * WorldComponent_Deserters.Instance.VisibilityLevel.intelCostModifier), serviceDef.useCriticalIntel))
                SyncedPurchaseService(instance.Parent.Map, serviceDef, WorldComponent_Deserters.Instance.VisibilityLevel.intelCostModifier);
            // Force the button to not be pressed in MP, as the mod will just do a bunch of stuff.
            return false;
        }

        // Include the modifier cost at the time of syncing to prevent prices being different than what was shown
        // to the player due to changes that may have happened due to delay between the local and synced call.
        private static void SyncedPurchaseService(Map map, DeserterServiceDef def, float currentCostModifier)
        {
            var cost = Mathf.FloorToInt(def.intelCost * currentCostModifier);

            if (TrySpendIntel(cost, def.useCriticalIntel, map))
                def.worker.Call();
        }

        private static IEnumerable<CodeInstruction> ReplaceServicePurchaseButton(IEnumerable<CodeInstruction> instr, MethodBase baseMethod)
        {
            var target = AccessTools.DeclaredMethod(typeof(Widgets), nameof(Widgets.ButtonInvisible), 
                new[] { typeof(Rect), typeof(bool) });
            var replacement = AccessTools.DeclaredMethod(typeof(VanillaFactionsDeserters), nameof(ServicePurchaseButtonReplacement));

            var replacedCount = 0;

            foreach (var ci in instr)
            {
                if ((ci.opcode == OpCodes.Call || ci.opcode == OpCodes.Callvirt) && ci.operand is MethodInfo method && method == target)
                {
                    ci.opcode = OpCodes.Call;
                    ci.operand = replacement;
                    replacedCount++;

                    // Insert more parameters
                    // Load in 2nd method's parameter (DeserterServiceDef)
                    yield return new CodeInstruction(OpCodes.Ldarg_2);
                    // Load in `this` (DeserterTabWorker_Services)
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                }

                yield return ci;
            }

            const int expected = 1;
            if (replacedCount != expected)
            {
                var name = (baseMethod.DeclaringType?.Namespace).NullOrEmpty() ? baseMethod.Name : $"{baseMethod.DeclaringType!.Name}:{baseMethod.Name}";
                Log.Warning($"Patched incorrect number of Widgets.ButtonInvisible calls (patched {replacedCount}, expected {expected}) for method {name}");
            }
        }

        #endregion

        #endregion
    }
}