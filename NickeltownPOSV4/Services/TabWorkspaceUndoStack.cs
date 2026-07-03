using System;
using System.Threading.Tasks;

namespace NickeltownPOSV4.Services;

public sealed class TabWorkspaceUndoStack : ITabWorkspaceUndoStack
{
    private string? _description;

    private Func<Task<bool>>? _undo;

    public event EventHandler? Changed;

    public bool CanUndo => _undo is not null;

    public string? UndoDescription => _description;

    public void Clear()
    {
        _undo = null;
        _description = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void PushUndo(string description, Func<Task<bool>> undoAsync)
    {
        _description = string.IsNullOrWhiteSpace(description) ? "Undo last tab action" : description.Trim();
        _undo = undoAsync;
        Changed?.Invoke(this, EventArgs.Empty);
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
            Changed?.Invoke(this, EventArgs.Empty);
        }

        return ok;
    }
}
