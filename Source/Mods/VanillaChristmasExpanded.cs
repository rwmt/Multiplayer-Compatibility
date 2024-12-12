using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat;

/// <summary>Vanilla Christmas Expanded by Oskar Potocki, Sarg Bjornson, Taranchuk</summary>
/// <see href="https://github.com/Vanilla-Expanded/VanillaChristmasExpanded"/>
/// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3380695249"/>
[MpCompatFor("VE.VanillaChristmasExpanded")]
public class VanillaChristmasExpanded
{
    public VanillaChristmasExpanded(ModContentPack mod)
    {
        LongEventHandler.ExecuteWhenFinished(LatePatch);

        // Change graphic
        MpCompat.RegisterLambdaMethod("VanillaChristmasExpanded.GrandFestiveTree", nameof(Building.GetGizmos), 0);
        // Deactivate (0)/activate (1) the snow spewer.
        MpCompat.RegisterLambdaMethod("VanillaChristmasExpanded.CompSnowSpewer", nameof(Building.GetGizmos), 0, 1);
    }

    private static void LatePatch()
    {
        MpCompatPatchLoader.LoadPatch<VanillaChristmasExpanded>();
    }

    private static int ModifyScreenWidthValue(int screenWidth)
    {
        // Constant values taken directly from MP's code.
        const int mpBtnMargin = 8;
        const int mpBtnWidth = 80;
        // If we don't offset it further the text will be covered by the
        // indicator rectangle (its width isn't stored as a constant).
        // Slightly winging it here with a value that doesn't fully match
        // MP size but results in the overlay looking alright (as in, it
        // doesn't overlap and there isn't too much of a gap between it
        // and the MP UI).
        const int extraOffset = 20;

        if (MP.IsInMultiplayer)
            return screenWidth - (mpBtnWidth + 2 * mpBtnMargin + extraOffset);
        return screenWidth;
    }

    [MpCompatTranspiler("VanillaChristmasExpanded.FestiveFavorManager", "DrawUI")]
    private static IEnumerable<CodeInstruction> MoveFestiveOverlayPosition(IEnumerable<CodeInstruction> instr, MethodBase baseMethod)
    {
        // Same issue as Vanilla Factions Insectoid 2. The overlay overlaps MP buttons. Since
        // both overlays depend on a specific storyteller, they won't overlap with themselves.
        // Since this mod requires a bit more offset (as it's overlapping with the rectangle
        // "ticks behind" indicator) the same method cannot be reused as for VFEI2.

        var target = AccessTools.DeclaredField(typeof(UI), nameof(UI.screenWidth));
        var replacement = MpMethodUtil.MethodOf(ModifyScreenWidthValue);
        var replacedCount = 0;

        foreach (var ci in instr)
        {
            yield return ci;

            if (ci.LoadsField(target))
            {
                yield return new CodeInstruction(OpCodes.Call, replacement);

                replacedCount++;
            }
        }

        const int expected = 1;
        if (replacedCount != expected)
        {
            var name = (baseMethod.DeclaringType?.Namespace).NullOrEmpty() ? baseMethod.Name : $"{baseMethod.DeclaringType!.Name}:{baseMethod.Name}";
            Log.Warning($"Patched incorrect number of Find.CameraDriver.MapPosition calls (patched {replacedCount}, expected {expected}) for method {name}");
        }
    }
}