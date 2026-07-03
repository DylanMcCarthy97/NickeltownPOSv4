using System;
using System.Threading.Tasks;

namespace NickeltownPOSV4.Services;

public interface ITabWorkspaceUndoStack
{
    event EventHandler? Changed;

    bool CanUndo { get; }

    string? UndoDescription { get; }

    void Clear();

    void PushUndo(string description, System.Func<Task<bool>> undoAsync);

    Task<bool> TryUndoAsync();
}
