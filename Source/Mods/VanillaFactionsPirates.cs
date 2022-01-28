using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Multiplayer.API;
using UnityEngine;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Vanilla Factions Expanded - Pirates by Oskar Potocki, Sarg Bjornson, erdelf, Roolo, Smash Phil, Taranchuk, xrushha, Kikohi, legodude17</summary>
    /// <see href="https://github.com/AndroidQuazar/VanillaFactionsExpanded-Pirates"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2723801948"/>
    [MpCompatFor("OskarPotocki.VFE.Pirates")]
    internal class VanillaFactionsPirates
    {
        // Warcasket
        // Dialog_WarcasketCustomization
        private static Type warcasketDialogType;
        private static MethodInfo notifySettingsChangedMethod;
        private static AccessTools.FieldRef<object, object> warcasketProjectField;
        // WarcasketProject
        private static AccessTools.FieldRef<object, Color> helmetColorField;
        private static AccessTools.FieldRef<object, Color> shoulderPadsColorField;
        private static AccessTools.FieldRef<object, Color> armorColorField;

        // Curses
        // GameComponent_CurseManager
        private static AccessTools.FieldRef<GameComponent> curseManagerInstanceField;
        private static AccessTools.FieldRef<object, IEnumerable> activeCurseDefsField;
        private static MethodInfo removeCurseMethod;
        private static MethodInfo addCurseMethod;
        // CurseDef
        private static MethodInfo curseDefWorkerGetter;
        // CurseWorker
        private static MethodInfo curseWorkerDisactivateMethod;
        private static MethodInfo curseWorkerStartMethod;

        public VanillaFactionsPirates(ModContentPack mod)
        {
            // Gizmos
            {
                // Enable/disable siege mode
                MpCompat.RegisterLambdaMethod("VFEPirates.Ability_SiegeMode", "GetGizmo", 0, 2);
                // Trigger flight towards tile (right now, the TransportPodsArrivalAction parameter is always null)
                MP.RegisterSyncMethod(AccessTools.TypeByName("VFEPirates.Ability_BlastOff"), "TryLaunch").ExposeParameter(1);
            }

            // Warcasket dialog
            {
                // Dialog_WarcasketCustomization
                warcasketDialogType = AccessTools.TypeByName("VFEPirates.Buildings.Dialog_WarcasketCustomization");
                notifySettingsChangedMethod = AccessTools.Method(warcasketDialogType, "Notify_SettingsChanged");
                warcasketProjectField = AccessTools.FieldRefAccess<object>(warcasketDialogType, "project");

                MP.RegisterSyncWorker<Window>(SyncWarcasketCustomizationDialog, warcasketDialogType);

                MP.RegisterSyncMethod(warcasketDialogType, "OnAcceptKeyPressed");
                MP.RegisterSyncMethod(warcasketDialogType, "OnCancelKeyPressed");

                MpCompat.RegisterLambdaMethod(warcasketDialogType, "DoWindowContents", 0, 1, 2);

                MpCompat.harmony.Patch(AccessTools.Method(warcasketDialogType, "DoWindowContents"),
                    prefix: new HarmonyMethod(typeof(VanillaFactionsPirates), nameof(PreDoWindowContents)),
                    postfix: new HarmonyMethod(typeof(VanillaFactionsPirates), nameof(PostDoWindowContents)));

                // WarcasketProject
                var type = AccessTools.TypeByName("VFEPirates.WarcasketProject");

                helmetColorField = AccessTools.FieldRefAccess<Color>(type, "colorHelmet");
                shoulderPadsColorField = AccessTools.FieldRefAccess<Color>(type, "colorShoulderPads");
                armorColorField = AccessTools.FieldRefAccess<Color>(type, "colorArmor");

                MP.RegisterSyncMethod(typeof(VanillaFactionsPirates), nameof(SyncedSetColors));
                MP.RegisterPauseLock(PauseIfDialogOpen);
            }

            // Curse window
            {
                // Page_ChooseCurses
                MP.RegisterSyncMethod(typeof(VanillaFactionsPirates), nameof(SyncedSetCurse));
                MpCompat.harmony.Patch(AccessTools.Method("VFEPirates.Page_ChooseCurses:DoCurse"),
                    transpiler: new HarmonyMethod(typeof(VanillaFactionsPirates), nameof(ReplaceButton)));

                // GameComponent_CurseManager
                var type = AccessTools.TypeByName("VFEPirates.GameComponent_CurseManager");

                curseManagerInstanceField = AccessTools.StaticFieldRefAccess<GameComponent>(AccessTools.Field(type, "Instance"));
                activeCurseDefsField = AccessTools.FieldRefAccess<IEnumerable>(type, "activeCurseDefs");
                removeCurseMethod = AccessTools.Method(type, "Remove");
                addCurseMethod = AccessTools.Method(type, "Add");

                // CurseDef
                curseDefWorkerGetter = AccessTools.PropertyGetter(AccessTools.TypeByName("VFEPirates.CurseDef"), "Worker");

                // CurseWorker
                type = AccessTools.TypeByName("VFEPirates.CurseWorker");

                curseWorkerDisactivateMethod = AccessTools.Method(type, "Disactivate");
                curseWorkerStartMethod = AccessTools.Method(type, "Start");
            }
        }

        private static void PreDoWindowContents(Window __instance, ref Color[] __state)
        {
            if (!MP.IsInMultiplayer)
                return;

            var project = warcasketProjectField(__instance);
            __state = new[]
            {
                helmetColorField(project),
                shoulderPadsColorField(project),
                armorColorField(project),
            };
        }

        private static void PostDoWindowContents(Window __instance, Color[] __state)
        {
            if (!MP.IsInMultiplayer)
                return;

            var project = warcasketProjectField(__instance);

            var helmetColor = helmetColorField(project);
            var shoulderPadsColor = shoulderPadsColorField(project);
            var armorColor = armorColorField(project);

            if (__state[0] != helmetColor || __state[1] != shoulderPadsColor || __state[2] != armorColor)
            {
                helmetColorField(project) = __state[0];
                shoulderPadsColorField(project) = __state[1];
                armorColorField(project) = __state[2];

                SyncedSetColors(helmetColor, shoulderPadsColor, armorColor);
            }
        }

        private static void SyncedSetColors(Color helmetColor, Color shoulderPadsColor, Color armorColor)
        {
            var window = Find.WindowStack.Windows.FirstOrDefault(w => w.GetType() == warcasketDialogType);

            if (window == null)
            {
                Log.Error("Couldn't find the warcasket customization dialog while trying to sync it");
                return;
            }

            var project = warcasketProjectField(window);

            helmetColorField(project) = helmetColor;
            shoulderPadsColorField(project) = shoulderPadsColor;
            armorColorField(project) = armorColor;

            notifySettingsChangedMethod.Invoke(window, Array.Empty<object>());
        }

        private static void SyncWarcasketCustomizationDialog(SyncWorker sync, ref Window dialog)
        {
            if (!sync.isWriting)
                dialog = Find.WindowStack.Windows.FirstOrDefault(w => w.GetType() == warcasketDialogType);
        }

        private static void SyncedSetCurse(Def curseDef)
        {
            var curseManager = curseManagerInstanceField();
            var set = activeCurseDefsField(curseManager);
            var curseWorker = curseDefWorkerGetter.Invoke(curseDef, Array.Empty<object>());

            var found = false;

            foreach (var def in set)
            {
                if (def == curseDef)
                {
                    found = true;
                    break;
                }
            }

            if (found)
            {
                removeCurseMethod.Invoke(curseManager, new object[] { curseDef });
                curseWorkerDisactivateMethod.Invoke(curseWorker, Array.Empty<object>());
            }
            else
            {
                addCurseMethod.Invoke(curseManager, new object[] { curseDef });
                curseWorkerStartMethod.Invoke(curseWorker, Array.Empty<object>());
            }
        }

        private static bool InjectedButton(Rect rect, bool doMouseoverSound, Def curseDef)
        {
            if (Widgets.ButtonInvisible(rect, doMouseoverSound))
            {
                if (MP.IsInMultiplayer)
                    SyncedSetCurse(curseDef);
                // Not in MP - return true as the button was clicked
                else
                    return true;
            }

            return false;
        }

        private static IEnumerable<CodeInstruction> ReplaceButton(IEnumerable<CodeInstruction> instr)
        {
            var method = AccessTools.Method(typeof(Widgets), nameof(Widgets.ButtonInvisible));

            foreach (var ci in instr)
            {
                if (ci.opcode == OpCodes.Call && ci.operand is MethodInfo info && info == method)
                {
                    // Inject another argument (CurseDef)
                    yield return new CodeInstruction(OpCodes.Ldarg_2);

                    ci.operand = AccessTools.Method(typeof(VanillaFactionsPirates), nameof(InjectedButton));
                }

                yield return ci;
            }
        }

        // Once we add non-blocking dialogs to the API
        // we should apply this only to the map it's used on
        private static bool PauseIfDialogOpen(Map map)
            => Find.WindowStack.IsOpen(warcasketDialogType);
    }
}
