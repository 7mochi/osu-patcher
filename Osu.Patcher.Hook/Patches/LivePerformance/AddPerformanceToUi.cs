using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using HarmonyLib;
using JetBrains.Annotations;
using Osu.Stubs.GameModes.Play;
using Osu.Stubs.Graphics;
using Osu.Stubs.Graphics.Skinning;
using Osu.Stubs.Graphics.Sprites;
using Osu.Stubs.Wrappers;
using Osu.Stubs.XNA;

namespace Osu.Patcher.Hook.Patches.LivePerformance;

/// <summary>
///     Hooks the constructor of <c>ScoreDisplay</c> to add our own <c>pTextSprite</c> for displaying
///     the performance counter, positioned above the hit error bar.
///     To display "pp" this needs <c>score-p@2x.png</c>/<c>score-p.png</c> in your skin's defined score font.
/// </summary>
[OsuPatch]
[HarmonyPatch]
[UsedImplicitly]
internal static class AddPerformanceToUi
{
    [UsedImplicitly]
    [HarmonyTargetMethod]
    private static MethodBase Target() => ScoreDisplay.Constructor.Reference;

    [UsedImplicitly]
    [HarmonyPostfix]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    private static void After([HarmonyArgument(0)] object spriteManager) // SpriteManager
    {
        if (!PerformanceOptions.ShowPerformanceInGame.Value)
            return;

        Debug.WriteLine("Adding Performance Counter to ScoreDisplay", nameof(AddPerformanceToUi));

        var currentSkin = SkinManager.Current.Get();
        var scoreFont = SkinOsu.FontScore.Get(currentSkin);
        var scoreFontOverlap = SkinOsu.FontScoreOverlap.Get(currentSkin);

        var performanceSprite = pSpriteText.Constructor.Invoke(
        [
            /* text: */ "00.0pp",
            /* fontName: */ scoreFont,
            /* spacingOverlap: */ (float)scoreFontOverlap,
            /* fieldType: */ Fields.BottomCentre,
            /* origin: */ Origins.BottomCentre,
            /* clock: */ Clocks.Game,
            /* startPosition: */ Vector2.Constructor.Invoke([0f, 22f]),
            /* drawDepth: */ 0.95f,
            /* alwaysDraw: */ true,
            /* color: */ Color.White,
            /* precache: */ true,
            /* source: */ SkinSource.ExceptBeatmap,
        ]);

        pDrawable.Scale.Set(performanceSprite, 0.50f);
        pSpriteText.TextConstantSpacing.Set(performanceSprite, true);
        pSpriteText.MeasureText.Invoke(performanceSprite);

        SpriteManager.Add.Invoke(spriteManager, [performanceSprite]);
        PerformanceDisplay.SetPerformanceCounter(performanceSprite);

        Debug.WriteLine("Added Performance Counter to ScoreDisplay", nameof(AddPerformanceToUi));
    }
}
