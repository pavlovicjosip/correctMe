using System;
using System.Collections.Generic;
using System.Linq;

namespace GrammarAssistant.Services;

public class HistoryService
{
    private readonly Stack<HistoryEntry> _undoStack = new();
    private readonly Stack<HistoryEntry> _redoStack = new();
    private const int MaxHistorySize = 50;

    public void AddEntry(string originalText, string correctedText)
    {
        _undoStack.Push(new HistoryEntry
        {
            OriginalText = originalText,
            CorrectedText = correctedText,
            Timestamp = DateTime.Now
        });

        // Clear redo stack when new action is performed
        _redoStack.Clear();

        // Limit history size
        if (_undoStack.Count > MaxHistorySize)
        {
            var items = _undoStack.Take(MaxHistorySize).ToList();
            _undoStack.Clear();
            items.Reverse();
            foreach (var item in items)
            {
                _undoStack.Push(item);
            }
        }
    }

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public HistoryEntry? Undo()
    {
        if (_undoStack.Count > 0)
        {
            var entry = _undoStack.Pop();
            _redoStack.Push(entry);
            return entry;
        }
        return null;
    }

    public HistoryEntry? Redo()
    {
        if (_redoStack.Count > 0)
        {
            var entry = _redoStack.Pop();
            _undoStack.Push(entry);
            return entry;
        }
        return null;
    }

    public IEnumerable<HistoryEntry> GetHistory()
    {
        return _undoStack.ToList();
    }
}

public class HistoryEntry
{
    public string OriginalText { get; set; } = "";
    public string CorrectedText { get; set; } = "";
    public DateTime Timestamp { get; set; }
}
