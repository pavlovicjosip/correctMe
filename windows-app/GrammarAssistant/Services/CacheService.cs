using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using GrammarAssistant.Models;

namespace GrammarAssistant.Services;

public class CacheService
{
    private readonly Dictionary<string, CachedResult> _cache = new();
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromHours(24);

    public bool TryGetCached(string text, out IEnumerable<Suggestion>? suggestions)
    {
        var hash = ComputeHash(text);
        
        if (_cache.TryGetValue(hash, out var cached))
        {
            if (DateTime.UtcNow - cached.Timestamp < _cacheExpiration)
            {
                suggestions = cached.Suggestions;
                return true;
            }
            
            // Expired, remove it
            _cache.Remove(hash);
        }

        suggestions = null;
        return false;
    }

    public void Cache(string text, IEnumerable<Suggestion> suggestions)
    {
        var hash = ComputeHash(text);
        _cache[hash] = new CachedResult
        {
            Suggestions = suggestions.ToList(),
            Timestamp = DateTime.UtcNow
        };

        // Limit cache size
        if (_cache.Count > 100)
        {
            var oldestKey = _cache.OrderBy(x => x.Value.Timestamp).First().Key;
            _cache.Remove(oldestKey);
        }
    }

    private static string ComputeHash(string text)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(text.Trim().ToLowerInvariant());
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    private class CachedResult
    {
        public List<Suggestion> Suggestions { get; set; } = new();
        public DateTime Timestamp { get; set; }
    }
}
