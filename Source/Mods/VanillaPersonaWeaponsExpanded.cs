using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using Verse;

namespace Multiplayer.Compat;

/// <summary>Vanilla Persona Weapons Expanded by Oskar Potocki, Taranchuk</summary>
/// <see href="https://github.com/Vanilla-Expanded/VanillaPersonaWeaponsExpanded"/>
/// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2826922787"/>
[MpCompatFor("VanillaExpanded.VPersonaWeaponsE")]
public class VanillaPersonaWeaponsExpanded
{
    #region Fields

    // CompGraphicCustomization
    private static Type compGraphicCustomizationType;
    // Dialog_ChoosePersonaWeapon
    private static Type personaWeaponDialogType;
    private static FastInvokeHandler sendWeaponMethod;
    private static AccessTools.FieldRef<Window, Thing> weaponCustomizationCurrentWeaponField;
    private static AccessTools.FieldRef<Window, WeaponTraitDef> weaponCustomizationCurrentWeaponTraitField;
    private static AccessTools.FieldRef<Window, Def> weaponCustomizationCurrentPsycastField;
    private static AccessTools.FieldRef<Window, ChoiceLetter> weaponCustomizationChoiceLetterField;

    #endregion

    #region Main Patch

    public VanillaPersonaWeaponsExpanded(ModContentPack mod)
    {
        LongEventHandler.ExecuteWhenFinished(LatePatch);

        compGraphicCustomizationType = AccessTools.TypeByName("GraphicCustomization.CompGraphicCustomization");
    }

    private static void LatePatch()
    {
        MpCompatPatchLoader.LoadPatch<VanillaPersonaWeaponsExpanded>();

        var type = personaWeaponDialogType = AccessTools.TypeByName("VanillaPersonaWeaponsExpanded.Dialog_ChoosePersonaWeapon");
        sendWeaponMethod = MethodInvoker.GetHandler(AccessTools.DeclaredMethod(type, "SendWeapon"));
        weaponCustomizationCurrentWeaponField = AccessTools.FieldRefAccess<Thing>(type, "currentWeapon");
        weaponCustomizationCurrentWeaponTraitField = AccessTools.FieldRefAccess<WeaponTraitDef>(type, "currentWeaponTrait");
        weaponCustomizationCurrentPsycastField = AccessTools.FieldRefAccess<Def>(type, "currentPsycast");
        weaponCustomizationChoiceLetterField = AccessTools.FieldRefAccess<ChoiceLetter>(type, "choiceLetter");
        // Accept customization
        MpCompat.RegisterLambdaMethod(type, nameof(Window.DoWindowContents), 3);
    }

    #endregion

    #region Sync Workers

    [MpCompatSyncWorker("VanillaPersonaWeaponsExpanded.Dialog_ChoosePersonaWeapon")]
    private static void SyncPersonaWeaponCustomizationDialog(SyncWorker sync, ref Window dialog)
    {
        ThingComp comp = null;

        if (sync.isWriting)
        {
            sync.Write(weaponCustomizationChoiceLetterField(dialog));

            // The weapon was created on client-side, and this
            // has client-side IDs. Send the def, so we'll re-make
            // the weapon after syncing it. Let's just hope that
            // creating the weapons client-side interface won't cause issues.
            sync.Write(weaponCustomizationCurrentWeaponField(dialog).def);
            sync.Write(weaponCustomizationCurrentWeaponTraitField(dialog));
            sync.Write(weaponCustomizationCurrentPsycastField(dialog));
        }
        else
        {
            // Skip constructor since it has a bunch of initialization we don't care about.
            dialog = (Window)FormatterServices.GetUninitializedObject(personaWeaponDialogType);

            var letter = sync.Read<ChoiceLetter>();
            weaponCustomizationChoiceLetterField(dialog) = letter;

            var def = sync.Read<ThingDef>();

            // Don't bother with making the weapon if the letter was archived,
            // at this point the dialog doesn't matter at all.
            if (letter is { ArchivedOnly: false })
            {
                var weapon = ThingMaker.MakeThing(def, GenStuff.DefaultStuffFor(def));

                weaponCustomizationCurrentWeaponField(dialog) = weapon;
                if (weapon is ThingWithComps twc)
                {
                    foreach (var c in twc.AllComps)
                    {
                        if (compGraphicCustomizationType.IsInstanceOfType(c))
                            comp = VanillaExpandedFramework.graphicCustomizationCompField(dialog) = c;
                    }
                }
            }

            weaponCustomizationCurrentWeaponTraitField(dialog) = sync.Read<WeaponTraitDef>();
            weaponCustomizationCurrentPsycastField(dialog) = sync.Read<Def>();
        }

        VanillaExpandedFramework.SyncGraphicCustomizationDialog(sync, ref dialog, comp);
    }

    #endregion

    #region Dialog archived letter handling

    // Close the dialog if the letter is archived.
    [MpCompatPrefix("VanillaPersonaWeaponsExpanded.Dialog_ChoosePersonaWeapon", nameof(Window.DoWindowContents))]
    private static void CloseDialogIfLetterArchived(Window __instance, ChoiceLetter ___choiceLetter)
    {
        if (MP.IsInMultiplayer && (___choiceLetter == null || ___choiceLetter.ArchivedOnly))
            __instance.Close();
    }

    // Cancel if letter is archived or there's no map to deliver to.
    [MpCompatPrefix("VanillaPersonaWeaponsExpanded.Dialog_ChoosePersonaWeapon", nameof(Window.DoWindowContents), 3)]
    private static bool PreSyncAcceptPersonaWeapon(Window __instance, ChoiceLetter ___choiceLetter, Pawn ___pawn)
    {
        if (!MP.IsInMultiplayer)
            return true;
        // If the letter is archived, cancel execution. A weapon was selected,
        // the letter was postponed, the letter expired (was forcibly postponed).
        if (___choiceLetter is not { ArchivedOnly: false })
            return false;
        // If the pawn doesn't have a map and there's no home maps, cancel execution.
        // The call would fail due to no map to deliver to.
        if ((___pawn.MapHeld ?? Find.AnyPlayerHomeMap) == null)
            return false;

        // Cleanup the letter
        if (MP.IsExecutingSyncCommand)
            Find.LetterStack.RemoveLetter(___choiceLetter);

        return true;
    }

    #endregion

    #region Letter removing patches

    // The postpone button removes the dialog for 7 days. This is a bit disruptive
    // in MP if one of the players closes the letter while another one has the
    // customization dialog open. This will cause postpone to just hide the letter.
    [MpCompatPrefix("VanillaPersonaWeaponsExpanded.ChoiceLetter_ChoosePersonaWeapon", "Choices", 0, MethodType.Getter)]
    private static bool DontRemoveLetterOnPostpone() => !MP.IsInMultiplayer;

    private static void DontRemoveLetterInMp(LetterStack letterStack, Letter letter)
    {
        if (!MP.IsInMultiplayer)
            letterStack.RemoveLetter(letter);
    }

    [MpCompatTranspiler("VanillaPersonaWeaponsExpanded.ChoiceLetter_ChoosePersonaWeapon", "OpenChooseDialog")]
    private static IEnumerable<CodeInstruction> ReplaceLetterRemoval(IEnumerable<CodeInstruction> instr, MethodBase baseMethod)
    {
        var target = AccessTools.DeclaredMethod(typeof(LetterStack), nameof(LetterStack.RemoveLetter));
        var replacement = MpMethodUtil.MethodOf(DontRemoveLetterInMp);
        var replacedCount = 0;

        foreach (var ci in instr)
        {
            if (ci.Calls(target))
            {
                ci.opcode = OpCodes.Call;
                ci.operand = replacement;

                replacedCount++;
            }

            yield return ci;
        }

        const int expected = 1;
        if (replacedCount != expected)
        {
            var name = (baseMethod.DeclaringType?.Namespace).NullOrEmpty() ? baseMethod.Name : $"{baseMethod.DeclaringType!.Name}:{baseMethod.Name}";
            Log.Warning($"Patched incorrect number of Find.LetterStack.RemoveLetter calls (patched {replacedCount}, expected {expected}) for method {name}");
        }
    }

    #endregion

    #region Sync selection if no customization options

    [MpCompatPrefix("VanillaPersonaWeaponsExpanded.ChoiceLetter_ChoosePersonaWeapon", "OpenChooseDialog")]
    private static bool SyncSelectionIfNoCustomizationOptions(ChoiceLetter __instance, Pawn ___pawn, ThingDef weaponDef)
    {
        if (!MP.IsInMultiplayer)
            return true;
        // If the weapon has props whose type is a subclass of CompGraphicCustomization
        // we let it run as normal, as it'll open up a cancelable dialog.
        if (weaponDef.comps.Any(props => compGraphicCustomizationType.IsAssignableFrom(props.compClass)))
            return true;

        // If the weapon is missing that comp it's selected
        // immediately, so we need to properly sync that.
        SyncedChooseNoCustomizationWeapon(__instance, ___pawn, weaponDef);
        return false;
    }

    [MpCompatSyncMethod]
    private static void SyncedChooseNoCustomizationWeapon(ChoiceLetter letter, Pawn pawn, ThingDef weaponDef)
    {
        if (letter == null || letter.ArchivedOnly || pawn == null || (pawn.MapHeld ?? Find.AnyPlayerHomeMap) == null)
            return;

        var weapon = ThingMaker.MakeThing(weaponDef, GenStuff.DefaultStuffFor(weaponDef));
        Find.LetterStack.RemoveLetter(letter);
        sendWeaponMethod(null, pawn, weapon.TryGetComp<CompBladelinkWeapon>(), weapon);
    }

    #endregion
}