using System;
using System.Threading.Tasks;
using NickeltownPOSV4.Services;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace NickeltownPOSV4.Services.Migration;

public sealed class WinUIMigrationFolderPicker : IMigrationFolderPicker
{
    private readonly IWindowHandleProvider _window;

    public WinUIMigrationFolderPicker(IWindowHandleProvider window)
    {
        _window = window;
    }

    public Task<string?> PickV2DataFolderAsync()
    {
        var picker = new FolderPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
        };

        picker.FileTypeFilter.Add("*");

        var hwnd = _window.WindowHandle;
        if (hwnd != 0)
        {
            InitializeWithWindow.Initialize(picker, hwnd);
        }

        return WaitForStorageFolderAsync(picker.PickSingleFolderAsync());
    }

    private static Task<string?> WaitForStorageFolderAsync(IAsyncOperation<StorageFolder> operation)
    {
        var tcs = new TaskCompletionSource<string?>();

        operation.Completed = (op, status) =>
        {
            switch (status)
            {
                case AsyncStatus.Completed:
                    var folder = op.GetResults();
                    tcs.TrySetResult(folder?.Path);
                    break;
                case AsyncStatus.Canceled:
                    tcs.TrySetResult(null);
                    break;
                case AsyncStatus.Error:
                    tcs.TrySetException(op.ErrorCode as Exception ?? new InvalidOperationException("Folder picker failed."));
                    break;
                default:
                    tcs.TrySetResult(null);
                    break;
            }
        };

        return tcs.Task;
    }
}
