namespace NickeltownPOSV4.Services.Tabs;

internal static class TabsUndoUiHelper
{
    public const string NothingToUndoMessage = "Nothing to undo on the tab board.";

    public static string FormatUndoResult(bool succeeded, string? undoDescription) =>
        succeeded
            ? (string.IsNullOrWhiteSpace(undoDescription) ? "Last action undone." : $"Undid: {undoDescription}")
            : "Could not undo that action (nothing was changed). You can try again.";
}
