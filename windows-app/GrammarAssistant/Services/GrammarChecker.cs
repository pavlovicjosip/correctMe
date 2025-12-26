using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using GrammarAssistant.Models;
using WeCantSpell.Hunspell;

namespace GrammarAssistant.Services;

public class GrammarChecker : IDisposable
{
    private readonly WordList? _dictionary;
    private bool _disposed;

    // Common grammar error patterns with replacement functions
    private static readonly (Regex Pattern, string Message, string Suggestion, Func<string, string>? GetReplacement)[] GrammarRules =
    [
        // Subject-verb agreement
        (new Regex(@"\b(I)\s+is\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "Subject-verb agreement error", "Use 'I am' instead of 'I is'",
            m => m.Replace(" is", " am", StringComparison.OrdinalIgnoreCase)),
        (new Regex(@"\b(he|she|it)\s+are\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "Subject-verb agreement error", "Use 'is' instead of 'are'",
            m => m.Replace(" are", " is", StringComparison.OrdinalIgnoreCase)),
        (new Regex(@"\b(I|you|we|they)\s+(is|was|has)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "Subject-verb agreement error", "Use 'am/are/were/have' with this subject",
            null), // Complex - no auto-fix
        (new Regex(@"\b(he|she|it)\s+(are|were|have)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "Subject-verb agreement error", "Use 'is/was/has' with this subject",
            null), // Complex - no auto-fix
            
        // Article errors
        (new Regex(@"\ba\s+([aeiou]\w+)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "Article error", "Use 'an' before words starting with a vowel sound",
            m => "an " + m.Substring(2)),
        (new Regex(@"\ban\s+([^aeiou\s]\w+)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "Article error", "Use 'a' before words starting with a consonant sound",
            m => "a " + m.Substring(3)),
            
        // Double negatives
        (new Regex(@"\b(don't|doesn't|didn't|won't|wouldn't|can't|couldn't)\s+\w*\s*(no|nothing|nobody|nowhere|never)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "Double negative", "Avoid using two negatives together",
            null), // Complex - no auto-fix
            
        // Common mistakes
        (new Regex(@"\bshould of\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "Common error", "Use 'should have' instead of 'should of'",
            m => m.Replace(" of", " have", StringComparison.OrdinalIgnoreCase)),
        (new Regex(@"\bcould of\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "Common error", "Use 'could have' instead of 'could of'",
            m => m.Replace(" of", " have", StringComparison.OrdinalIgnoreCase)),
        (new Regex(@"\bwould of\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "Common error", "Use 'would have' instead of 'would of'",
            m => m.Replace(" of", " have", StringComparison.OrdinalIgnoreCase)),
        (new Regex(@"\bmust of\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "Common error", "Use 'must have' instead of 'must of'",
            m => m.Replace(" of", " have", StringComparison.OrdinalIgnoreCase)),
            
        // Their/there/they're
        (new Regex(@"\btheir\s+(is|are|was|were)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "Possible confusion", "Did you mean 'there is/are'?",
            m => m.Replace("their", "there", StringComparison.OrdinalIgnoreCase)),
        (new Regex(@"\bthere\s+(car|house|book|dog|cat|friend|mother|father|child)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "Possible confusion", "Did you mean 'their' (possessive)?",
            m => m.Replace("there", "their", StringComparison.OrdinalIgnoreCase)),
            
        // Its/it's
        (new Regex(@"\bits\s+(a|the|very|really|so|quite)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "Possible confusion", "Did you mean 'it's' (it is)?",
            m => m.Replace("its", "it's", StringComparison.OrdinalIgnoreCase)),
            
        // Your/you're  
        (new Regex(@"\byour\s+(welcome|right|wrong|correct|going|coming|doing)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "Possible confusion", "Did you mean 'you're' (you are)?",
            m => m.Replace("your", "you're", StringComparison.OrdinalIgnoreCase)),
            
        // Then/than
        (new Regex(@"\b(more|less|better|worse|bigger|smaller|faster|slower)\s+then\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "Then/than confusion", "Use 'than' for comparisons",
            m => m.Replace("then", "than", StringComparison.OrdinalIgnoreCase)),
            
        // Repeated words
        (new Regex(@"\b(\w+)\s+\1\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "Repeated word", "Remove the duplicate word",
            m => m.Split(' ')[0]),
            
        // Missing space after punctuation - no tooltip (whitespace issues with RichTextBox)
        (new Regex(@"[.!?][A-Z]", RegexOptions.Compiled),
            "Missing space", "Add a space after punctuation",
            null),
            
        // Multiple spaces - no tooltip (whitespace issues with RichTextBox)
        (new Regex(@"\s{2,}", RegexOptions.Compiled),
            "Multiple spaces", "Use a single space",
            null),
            
        // Affect/effect common misuse
        (new Regex(@"\bthe\s+affect\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "Affect/effect confusion", "Did you mean 'the effect' (noun)?",
            m => m.Replace("affect", "effect", StringComparison.OrdinalIgnoreCase)),
        (new Regex(@"\bwill\s+effect\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "Affect/effect confusion", "Did you mean 'will affect' (verb)?",
            m => m.Replace("effect", "affect", StringComparison.OrdinalIgnoreCase)),
    ];

    public GrammarChecker()
    {
        var dictionaryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Dictionaries");
        var dicFile = Path.Combine(dictionaryPath, "en_US.dic");
        var affFile = Path.Combine(dictionaryPath, "en_US.aff");

        if (File.Exists(dicFile) && File.Exists(affFile))
        {
            _dictionary = WordList.CreateFromFiles(dicFile, affFile);
        }
    }

    public IEnumerable<Suggestion> Check(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            yield break;

        // Spell checking
        foreach (var suggestion in CheckSpelling(text))
            yield return suggestion;

        // Grammar rules
        foreach (var suggestion in CheckGrammar(text))
            yield return suggestion;

        // Capitalization
        foreach (var suggestion in CheckCapitalization(text))
            yield return suggestion;
    }

    private IEnumerable<Suggestion> CheckSpelling(string text)
    {
        if (_dictionary == null)
        {
            yield return new Suggestion("Dictionary not loaded", "Place en_US.dic and en_US.aff in the Dictionaries folder");
            yield break;
        }

        var wordPattern = new Regex(@"\b[a-zA-Z']+\b");
        var checkedWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in wordPattern.Matches(text))
        {
            var word = match.Value;
            
            // Skip very short words, contractions, and already checked
            if (word.Length < 2 || checkedWords.Contains(word))
                continue;

            checkedWords.Add(word);

            if (!_dictionary.Check(word))
            {
                var suggestions = _dictionary.Suggest(word).Take(5).ToList();
                var suggestionText = suggestions.Count > 0
                    ? $"Did you mean: {string.Join(", ", suggestions)}"
                    : "No suggestions available";
                
                // Use first suggestion as replacement
                var replacement = suggestions.Count > 0 ? suggestions[0] : null;

                yield return new Suggestion(
                    $"Spelling: '{word}' may be misspelled", 
                    suggestionText,
                    null,
                    match.Index,
                    match.Length,
                    replacement);
            }
        }
    }

    private IEnumerable<Suggestion> CheckGrammar(string text)
    {
        foreach (var (pattern, message, suggestion, getReplacement) in GrammarRules)
        {
            foreach (Match match in pattern.Matches(text))
            {
                var context = GetContext(text, match.Index, match.Length);
                var replacement = getReplacement?.Invoke(match.Value);
                
                yield return new Suggestion(
                    $"{message}: '{match.Value}'", 
                    $"{suggestion}\nContext: {context}",
                    null,
                    match.Index,
                    match.Length,
                    replacement);
            }
        }
    }

    private IEnumerable<Suggestion> CheckCapitalization(string text)
    {
        // Check sentence starts
        var sentencePattern = new Regex(@"(?:^|[.!?]\s+)([a-z])");
        foreach (Match match in sentencePattern.Matches(text))
        {
            var context = GetContext(text, match.Index, match.Length);
            var replacement = match.Value.Length > 0 
                ? char.ToUpper(match.Value[^1]) + "" 
                : null;
            
            yield return new Suggestion(
                "Sentence should start with a capital letter", 
                $"Capitalize the first letter\nContext: {context}",
                null,
                match.Index,
                match.Length,
                replacement != null ? match.Value[..^1] + replacement : null);
        }

        // Check 'i' should be 'I'
        var lonelyI = new Regex(@"(?<![a-zA-Z])[i](?![a-zA-Z])");
        foreach (Match match in lonelyI.Matches(text))
        {
            yield return new Suggestion(
                "The pronoun 'I' should be capitalized", 
                "Use 'I' instead of 'i'",
                null,
                match.Index,
                match.Length,
                "I");
        }
    }

    private static string GetContext(string text, int index, int length)
    {
        var start = Math.Max(0, index - 20);
        var end = Math.Min(text.Length, index + length + 20);
        
        var before = text[start..index];
        var match = text.Substring(index, length);
        var after = text[(index + length)..end];

        var prefix = start > 0 ? "..." : "";
        var suffix = end < text.Length ? "..." : "";

        return $"{prefix}{before}[{match}]{after}{suffix}";
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}
