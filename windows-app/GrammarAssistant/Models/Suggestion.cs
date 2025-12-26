namespace GrammarAssistant.Models;

public record Suggestion(
    string Message, 
    string? SuggestionText = null, 
    string? CorrectedText = null,
    int StartIndex = -1,
    int Length = 0,
    string? ReplacementText = null);
