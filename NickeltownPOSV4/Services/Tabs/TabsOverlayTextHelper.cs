namespace NickeltownPOSV4.Services.Tabs;

internal static class TabsOverlayTextHelper
{
    public static string ArchiveConfirm(string displayName) =>
        $"“{displayName}” will leave the board, but its drinks and payment history stay on file.";

    public static string DeleteConfirm(string displayName) =>
        $"“{displayName}” and all of its drinks, payments, and history will be removed and cannot be recovered. To hide a tab without erasing its data, use Archive instead.";
}
