namespace NickeltownPOSV4.ViewModels;

public sealed class QwertyKeyModel(string display, string code, double widthHint = 34)
{
    public string Display { get; } = display;

    public string Code { get; } = code;

    public double WidthHint { get; } = widthHint;
}
