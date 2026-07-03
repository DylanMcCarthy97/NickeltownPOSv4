using System;
using System.Collections.Generic;
using NickeltownPOSV4.Themes;

namespace NickeltownPOSV4.Services;

public interface IPosThemeService
{
    UiThemeId CurrentThemeId { get; }

    event EventHandler? ThemeChanged;

    IReadOnlyList<UiThemeId> AllThemeIds { get; }

    IReadOnlyList<UiThemeId> RecommendedThemeIds { get; }

    IReadOnlyList<UiThemeId> MoreThemeIds { get; }

    string GetDisplayName(UiThemeId id);

    void Apply(UiThemeId themeId);

    void PushBrushesToApplicationResources();
}
