using System;
using System.Threading.Tasks;
using NickeltownPOSV4.Services.Tabs;

namespace NickeltownPOSV4.Services;

public interface ITabWorkspaceUndoStack
{
    event EventHandler? Changed;

    bool CanUndo { get; }

    string? UndoDescription { get; }

    TabUndoPreview? Preview { get; }

    void Clear();

    void PushUndo(string description, Func<Task<bool>> undoAsync);

    void PushUndo(TabUndoPreview preview, Func<Task<bool>> undoAsync);

    Task<bool> TryUndoAsync();
}
