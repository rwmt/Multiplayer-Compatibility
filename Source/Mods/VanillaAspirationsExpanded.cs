using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using Verse;

namespace Multiplayer.Compat;

/// <summary>Vanilla Aspirations Expanded by Oskar Potocki, Legodude17, Sarg Bjornson</summary>
[MpCompatFor("VanillaExpanded.VanillaAspirationsExpanded")]
public class VanillaAspirationsExpanded
{
    #region Fields
    
    // MP Compat
    private static bool isDrawingDialog = false;

    // List<AspirationDef>
    private static Type aspirationDefListType;

    // SyncDelegates
    private static FastInvokeHandler pickRandomTraitAndPassionsMethod;

    // ChoiceLetter_GrowthMoment_Aspirations
    private static AccessTools.FieldRef<ChoiceLetter_GrowthMoment, IList> aspirationChoicesField;
    private static AccessTools.FieldRef<ChoiceLetter_GrowthMoment, int> aspirationGainsCountField;
    private static FastInvokeHandler trySetAspirationChoicesMethod;
    private static FastInvokeHandler makeAspirationChoicesMethod;

    #endregion

    #region Main Patch

    public VanillaAspirationsExpanded(ModContentPack mod)
    {
        // Run the patches
        MpCompatPatchLoader.LoadPatch(this);

        // Setup list type with a generic we can't reference
        var type = AccessTools.TypeByName("VAspirE.AspirationDef");
        aspirationDefListType = typeof(List<>).MakeGenericType(type);

        // Access MP method
        type = AccessTools.TypeByName("Multiplayer.Client.SyncDelegates");
        var method = AccessTools.DeclaredMethod(type, "PickRandomTraitAndPassions");
        pickRandomTraitAndPassionsMethod = MethodInvoker.GetHandler(method);

        // Handle aspirations fulfilled letter/dialog
        type = AccessTools.TypeByName("VAspirE.ChoiceLetter_AspirationsFulfilled");
        // This letter/dialog have different type, but are otherwise mostly the
        // same as vanilla growth moment ones. MP should handle those already,
        // and we only need to register the default choice for the letter.
        MP.RegisterDefaultLetterChoice(method, type);

        // Handle growth moment with aspirations letter/dialog
        type = AccessTools.TypeByName("VAspirE.ChoiceLetter_GrowthMoment_Aspirations");
        // Setup fields
        aspirationChoicesField = AccessTools.FieldRefAccess<IList>(type, "aspirationChoices");
        aspirationGainsCountField = AccessTools.FieldRefAccess<int>(type, "aspirationGainsCount");
        // Setup methods
        trySetAspirationChoicesMethod = MethodInvoker.GetHandler(AccessTools.DeclaredMethod(type, "TrySetAspirationChoices"));
        var makeAspirationChoices = AccessTools.DeclaredMethod(type, "MakeAspirationChoices");
        makeAspirationChoicesMethod = MethodInvoker.GetHandler(makeAspirationChoices);
        // Sync methods
        MP.RegisterSyncMethod(makeAspirationChoices);
        // Letter timeout handling
        MP.RegisterDefaultLetterChoice(MpMethodUtil.MethodOf(PickRandomAspirationsTraitPassions), type);
    }

    #endregion

    #region Choice letter expiry handling

    private static void PickRandomAspirationsTraitPassions(ChoiceLetter_GrowthMoment letter)
    {
        // Make sure the aspirations are prepared
        trySetAspirationChoicesMethod(letter);

        var aspirationChoices = aspirationChoicesField(letter);
        var aspirationCount = aspirationGainsCountField(letter);

        // If there's any aspirations to pick from, pick some random ones.
        if (aspirationCount > 0 && aspirationChoices != null)
            makeAspirationChoicesMethod(letter,
                Activator.CreateInstance(aspirationDefListType, aspirationChoices.Cast<Def>().InRandomOrder().Take(aspirationCount)));
        // This is technically no-op due to null check, but include it just in case
        // the mod decides to do something with it at some point (or a different mod uses it).
        else
            makeAspirationChoicesMethod(letter, null);

        // MP method to pick traits and passions, which handles normal, vanilla growth moment.
        // This will also handle closing the letter.
        pickRandomTraitAndPassionsMethod(letter);
    }

    #endregion

    #region Seed aspiration generation RNG

    // The letter tries to generate the options when opened. Make the picks seeded, so all players will get the same ones.
    [MpCompatPrefix("VAspirE.ChoiceLetter_GrowthMoment_Aspirations", "TrySetAspirationChoices")]
    private static void PreTrySetAspirationChoices(ChoiceLetter_GrowthMoment __instance)
        => Rand.PushState(Gen.HashCombineInt(__instance.pawn.thingIDNumber, __instance.arrivalTick));

    [MpCompatPostfix("VAspirE.ChoiceLetter_GrowthMoment_Aspirations", "TrySetAspirationChoices")]
    private static void PostTrySetAspirationChoices()
        => Rand.PopState();

    #endregion

    #region Don't close dialog when drawing it

    // Patches to not remove the letter if it's in the process of being drawn.
    // Rather than prefixing LetterStack.RemoveLetter we could instead change
    // Multiplayer.IsDrawingGrowthMomentDialog.isDrawing to true/false, and let
    // MP handle this itself. However, it's safer to do a new patch for this.

    [MpCompatPrefix("VAspirE.Dialog_GrowthMomentChoices_Aspirations", nameof(Dialog_GrowthMomentChoices.DoWindowContents))]
    private static void PreDoWindowContents() => isDrawingDialog = true;

    [MpCompatFinalizer("VAspirE.Dialog_GrowthMomentChoices_Aspirations", nameof(Dialog_GrowthMomentChoices.DoWindowContents))]
    private static void PostDoWindowContents() => isDrawingDialog = false;

    [MpCompatPrefix(typeof(LetterStack), nameof(LetterStack.RemoveLetter))]
    private static bool DontRemoveChoiceLetter() => !MP.IsInMultiplayer || !isDrawingDialog;

    #endregion
}