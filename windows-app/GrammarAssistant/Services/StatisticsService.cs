using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace GrammarAssistant.Services;

public class StatisticsService
{
    private readonly Dictionary<string, int> _errorCounts = new();
    private int _totalChecks = 0;
    private int _totalErrors = 0;

    public void RecordError(string errorType)
    {
        if (_errorCounts.ContainsKey(errorType))
        {
            _errorCounts[errorType]++;
        }
        else
        {
            _errorCounts[errorType] = 1;
        }
        _totalErrors++;
    }

    public void RecordCheck()
    {
        _totalChecks++;
    }

    public Dictionary<string, int> GetTopErrors(int count = 10)
    {
        return _errorCounts
            .OrderByDescending(x => x.Value)
            .Take(count)
            .ToDictionary(x => x.Key, x => x.Value);
    }

    public int TotalChecks => _totalChecks;
    public int TotalErrors => _totalErrors;
    public double AverageErrorsPerCheck => _totalChecks > 0 ? (double)_totalErrors / _totalChecks : 0;

    public static ReadabilityScore CalculateReadability(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new ReadabilityScore();

        var sentences = Regex.Split(text, @"[.!?]+").Where(s => !string.IsNullOrWhiteSpace(s)).Count();
        var words = Regex.Matches(text, @"\b\w+\b").Count;
        var syllables = CountSyllables(text);

        if (sentences == 0 || words == 0)
            return new ReadabilityScore();

        // Flesch Reading Ease
        var fleschScore = 206.835 - 1.015 * (words / (double)sentences) - 84.6 * (syllables / (double)words);
        fleschScore = Math.Max(0, Math.Min(100, fleschScore));

        // Flesch-Kincaid Grade Level
        var gradeLevel = 0.39 * (words / (double)sentences) + 11.8 * (syllables / (double)words) - 15.59;
        gradeLevel = Math.Max(0, gradeLevel);

        return new ReadabilityScore
        {
            FleschReadingEase = fleschScore,
            FleschKincaidGrade = gradeLevel,
            WordCount = words,
            SentenceCount = sentences,
            AverageWordsPerSentence = words / (double)sentences
        };
    }

    private static int CountSyllables(string text)
    {
        var words = Regex.Matches(text, @"\b\w+\b");
        var totalSyllables = 0;

        foreach (Match word in words)
        {
            totalSyllables += CountSyllablesInWord(word.Value.ToLower());
        }

        return Math.Max(totalSyllables, 1);
    }

    private static int CountSyllablesInWord(string word)
    {
        word = word.ToLower().Trim();
        if (word.Length <= 3) return 1;

        word = Regex.Replace(word, @"(?:[^laeiouy]es|ed|[^laeiouy]e)$", "");
        word = Regex.Replace(word, @"^y", "");
        
        var syllables = Regex.Matches(word, @"[aeiouy]{1,2}").Count;
        return Math.Max(syllables, 1);
    }
}

public class ReadabilityScore
{
    public double FleschReadingEase { get; set; }
    public double FleschKincaidGrade { get; set; }
    public int WordCount { get; set; }
    public int SentenceCount { get; set; }
    public double AverageWordsPerSentence { get; set; }

    public string GetReadabilityLevel()
    {
        return FleschReadingEase switch
        {
            >= 90 => "Very Easy (5th grade)",
            >= 80 => "Easy (6th grade)",
            >= 70 => "Fairly Easy (7th grade)",
            >= 60 => "Standard (8th-9th grade)",
            >= 50 => "Fairly Difficult (10th-12th grade)",
            >= 30 => "Difficult (College)",
            _ => "Very Difficult (College graduate)"
        };
    }
}
