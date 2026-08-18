using System.Collections.Generic;
using JetBrains.Annotations;
using Osu.Stubs.GameModes.Options;
using Osu.Stubs.Wrappers;

namespace Osu.Patcher.Hook.Patches.LivePerformance;

[UsedImplicitly]
internal class PerformanceOptions : PatchOptions
{
    public override IEnumerable<object> CreateOptions() =>
    [
        CreateInGameDisplayOption(),
        CreateLeaderboardDisplayOption(),
    ];

    public override void Load(Settings config)
    {
        ShowPerformanceInGame.Value = config.ShowPerformanceInGame;
        ShowPerformanceOnLeaderboard.Value = config.ShowPerformanceOnLeaderboard;
    }

    public override void Save(Settings config)
    {
        config.ShowPerformanceInGame = ShowPerformanceInGame.Value;
        config.ShowPerformanceOnLeaderboard = ShowPerformanceOnLeaderboard.Value;
    }

    #region Options Creation

    private static object CreateInGameDisplayOption() => OptionCheckbox.Constructor.Invoke([
        /* title: */ "Show PP during gameplay",
        /* tooltip: */ "A small PP counter display will be visible below the accuracy display.",
        /* binding: */ ShowPerformanceInGame.Bindable,
        /* onChange: */ null,
    ]);

    private static object CreateLeaderboardDisplayOption() => OptionCheckbox.Constructor.Invoke([
        /* title: */ "Show PP on local leaderboards",
        /* tooltip: */ "A small PP counter display will be visible on each local score.",
        /* binding: */ ShowPerformanceOnLeaderboard.Bindable,
        /* onChange: */ null,
    ]);

    #endregion

    #region Bindables

    public static readonly BindableWrapper<bool> ShowPerformanceInGame =
        new(BindableType.Bool, false, Settings.Default.ShowPerformanceInGame);

    public static readonly BindableWrapper<bool> ShowPerformanceOnLeaderboard =
        new(BindableType.Bool, false, Settings.Default.ShowPerformanceOnLeaderboard);

    #endregion
}