using System.Collections.Generic;
using System.Text.RegularExpressions;
using GrammarAssistant.Models;

namespace GrammarAssistant.Services;

public class GrammarChecker
{
    private static readonly Regex DoubleSpaceRegex = new("\\s{2,}", RegexOptions.Compiled);
    private static readonly Regex RepeatWordRegex = new("\\b(\\w+)(\\s+\\1\\b)+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public IEnumerable<Suggestion> Check(string text)
    {
        if (DoubleSpaceRegex.IsMatch(text))
        {
            yield return new Suggestion("Multiple spaces detected; replace with a single space.", "Replace double spaces with one space.");
        }

        foreach (Match match in RepeatWordRegex.Matches(text))
        {
            var word = match.Groups[1].Value;
            yield return new Suggestion($"Repeated word: '{word}'.", $"Use a single '{word}'.");
        }

        foreach (var suggestion in CheckSentenceStarts(text))
        {
            yield return suggestion;
        }

        foreach (var suggestion in CheckTrailingWhitespace(text))
        {
            yield return suggestion;
        }
    }

    private IEnumerable<Suggestion> CheckSentenceStarts(string text)
    {
        var sentences = Regex.Split(text, @"(?<=[.!?])");
        foreach (var raw in sentences)
        {
            var content = raw.Trim();
            if (string.IsNullOrEmpty(content))
            {
                continue;
            }

            var firstChar = content[0];
            if (char.IsLetter(firstChar) && !char.IsUpper(firstChar))
            {
                var preview = content.Length > 20 ? content[..20] + "…" : content;
                yield return new Suggestion("Sentence should start with a capital letter.", $"Capitalize '{preview}'");
            }
        }
    }

    private IEnumerable<Suggestion> CheckTrailingWhitespace(string text)
    {
        var lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].EndsWith(" "))
            {
                yield return new Suggestion($"Line {i + 1} has trailing spaces.", "Remove extra spaces at the end of the line.");
            }
        }
    }
}
