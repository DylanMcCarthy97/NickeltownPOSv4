using System;
using System.Threading.Tasks;
using NickeltownPOSV4.Services.Tabs;

namespace NickeltownPOSV4.Services;

public sealed class TabWorkspaceUndoStack : ITabWorkspaceUndoStack
{
    private string? _description;

    private TabUndoPreview? _preview;

    private Func<Task<bool>>? _undo;

    public event EventHandler? Changed;

    public bool CanUndo => _undo is not null;

    public string? UndoDescription => _description;

    public TabUndoPreview? Preview => _preview;

    public void Clear()
    {
        _undo = null;
        _description = null;
        _preview = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void PushUndo(string description, Func<Task<bool>> undoAsync)
    {
        var desc = string.IsNullOrWhiteSpace(description) ? "Undo last tab action" : description.Trim();
        PushCore(desc, TabUndoPreview.FromDescription(desc), undoAsync);
    }

    public void PushUndo(TabUndoPreview preview, Func<Task<bool>> undoAsync)
    {
        ArgumentNullException.ThrowIfNull(preview);
        var desc = string.IsNullOrWhiteSpace(preview.Description) ? preview.Headline : preview.Description.Trim();
        PushCore(desc, preview, undoAsync);
    }

    public async Task<bool> TryUndoAsync()
    {
        if (_undo is null)
        {
            return false;
        }

        var fn = _undo;
        bool ok;
        try
        {
            ok = await fn().ConfigureAwait(true);
        }
        catch
        {
            ok = false;
        }

        if (ok)
        {
            _undo = null;
            _description = null;
            _preview = null;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        return ok;
    }

    private void PushCore(string description, TabUndoPreview preview, Func<Task<bool>> undoAsync)
    {
        _description = description;
        _preview = preview;
        _undo = undoAsync;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
