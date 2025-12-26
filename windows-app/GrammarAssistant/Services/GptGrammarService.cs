using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using GrammarAssistant.Models;

namespace GrammarAssistant.Services;

public class GptGrammarService
{
    private static readonly HttpClient HttpClient = new();
    private const string OpenRouterApiUrl = "https://openrouter.ai/api/v1/chat/completions";
    private readonly string? _apiKey;

    public GptGrammarService()
    {
        _apiKey = LoadApiKey();
        if (!string.IsNullOrEmpty(_apiKey))
        {
            HttpClient.DefaultRequestHeaders.Clear();
            HttpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
            HttpClient.DefaultRequestHeaders.Add("HTTP-Referer", "https://github.com/grammar-assistant");
            HttpClient.DefaultRequestHeaders.Add("X-Title", "Grammar Assistant");
        }
    }

    public bool IsConfigured => !string.IsNullOrEmpty(_apiKey);

    private static string? LoadApiKey()
    {
        try
        {
            var keyFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "api-key.txt");
            if (File.Exists(keyFile))
            {
                var key = File.ReadAllText(keyFile).Trim();
                return string.IsNullOrWhiteSpace(key) ? null : key;
            }
        }
        catch
        {
            // Ignore errors
        }
        return null;
    }

    public async Task<IEnumerable<Suggestion>> CheckWithGptAsync(string text)
    {
        var suggestions = new List<Suggestion>();

        if (!IsConfigured)
        {
            suggestions.Add(new Suggestion(
                "AI not configured",
                "Create an 'api-key.txt' file with your OpenRouter API key to enable AI-powered suggestions\nGet your key at: https://openrouter.ai/keys"));
            return suggestions;
        }

        if (string.IsNullOrWhiteSpace(text))
            return suggestions;

        // Use GPT-4o-mini - extremely cheap and reliable (~$0.15 per 1M tokens)
        // A typical paragraph costs less than $0.001 to check
        try
        {
            var result = await TryCheckWithModel(text, "openai/gpt-4o-mini");
            if (result.Any())
            {
                // Check if we got a corrected text
                var hasCorrectedText = result.Any(s => !string.IsNullOrEmpty(s.CorrectedText));
                
                if (!hasCorrectedText && result.Count > 1)
                {
                    // If we have suggestions but no corrected text, request it
                    var correctedText = await GetCorrectedTextOnly(text);
                    if (!string.IsNullOrEmpty(correctedText))
                    {
                        result.Insert(0, new Suggestion(
                            "✓ Corrected Version Available",
                            "Click 'Apply Corrections' to replace your text",
                            correctedText));
                    }
                }
                
                result.Insert(0, new Suggestion("✓ AI Grammar Check Complete", "Powered by GPT-4o-mini (cost: ~$0.0001 per check)"));
                return result;
            }
        }
        catch (HttpRequestException ex)
        {
            suggestions.Add(new Suggestion(
                "Could not connect to AI service",
                $"Error: {ex.Message}\n\n" +
                "Make sure you have credits in your OpenRouter account.\n" +
                "Add credits at: https://openrouter.ai/credits\n\n" +
                "GPT-4o-mini costs about $0.15 per million tokens (~$0.0001 per paragraph check)."));
        }
        catch (Exception ex)
        {
            suggestions.Add(new Suggestion("Error", $"Failed to check grammar: {ex.Message}"));
        }

        return suggestions;
    }

    private async Task<string?> GetCorrectedTextOnly(string text)
    {
        try
        {
            var request = new
            {
                model = "openai/gpt-4o-mini",
                messages = new[]
                {
                    new
                    {
                        role = "system",
                        content = "You are a grammar correction assistant. Return ONLY the corrected version of the text with all grammar, spelling, and punctuation errors fixed. Do not add explanations or comments."
                    },
                    new
                    {
                        role = "user",
                        content = text
                    }
                },
                temperature = 0.3,
                max_tokens = 1000
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await HttpClient.PostAsync(OpenRouterApiUrl, content);

            if (response.IsSuccessStatusCode)
            {
                var responseJson = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<OpenAiResponse>(responseJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return result?.Choices?[0]?.Message?.Content?.Trim();
            }
        }
        catch
        {
            // Ignore errors in fallback
        }

        return null;
    }

    public async Task<string?> RewriteTextAsync(string text, string style)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(text))
            return null;

        try
        {
            var stylePrompt = style.ToLower() switch
            {
                "professional" => "Rewrite this text in a professional, business-appropriate tone. Keep it clear and polished.",
                "casual" => "Rewrite this text in a casual, conversational tone. Make it friendly and approachable.",
                "formal" => "Rewrite this text in a formal, academic tone. Use proper language and structure.",
                "concise" => "Rewrite this text to be more concise. Remove unnecessary words while keeping the meaning.",
                "elaborate" => "Rewrite this text with more detail and elaboration. Expand on the ideas presented.",
                "friendly" => "Rewrite this text in a warm, friendly tone. Make it personable and engaging.",
                "academic" => "Rewrite this text in an academic style. Use scholarly language and proper citations format if applicable.",
                _ => "Rewrite this text to improve its clarity and flow."
            };

            var request = new
            {
                model = "openai/gpt-4o-mini",
                messages = new[]
                {
                    new
                    {
                        role = "system",
                        content = $"{stylePrompt} Return ONLY the rewritten text without any explanations, comments, or quotation marks around it."
                    },
                    new
                    {
                        role = "user",
                        content = text
                    }
                },
                temperature = 0.7,
                max_tokens = 2000
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await HttpClient.PostAsync(OpenRouterApiUrl, content);

            if (response.IsSuccessStatusCode)
            {
                var responseJson = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<OpenAiResponse>(responseJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                var rewrittenText = result?.Choices?[0]?.Message?.Content?.Trim();
                
                // Remove surrounding quotes if present
                if (rewrittenText != null && rewrittenText.StartsWith("\"") && rewrittenText.EndsWith("\""))
                {
                    rewrittenText = rewrittenText[1..^1];
                }
                
                return rewrittenText;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Rewrite error: {ex.Message}");
        }

        return null;
    }

    private async Task<List<Suggestion>> TryCheckWithModel(string text, string model)
    {
        var suggestions = new List<Suggestion>();

        try
        {
            var request = new
            {
                model = model,
                messages = new[]
                {
                    new
                    {
                        role = "system",
                        content = @"You are a professional grammar and writing assistant. Analyze the provided text and provide corrections.

CRITICAL: You MUST include a 'corrected_text' field with the fully corrected version of the entire input text.

Format your response as JSON with this EXACT structure:
{
  ""corrected_text"": ""The complete corrected version of the entire input text goes here"",
  ""issues"": [
    {
      ""issue"": ""brief description"",
      ""explanation"": ""detailed explanation"",
      ""original"": ""the exact text that needs to be changed"",
      ""replacement"": ""the corrected text to replace it with""
    }
  ]
}

Example for input 'I has a apple':
{
  ""corrected_text"": ""I have an apple"",
  ""issues"": [
    {""issue"": ""Subject-verb agreement"", ""explanation"": ""'I' requires 'have' not 'has'"", ""original"": ""has"", ""replacement"": ""have""},
    {""issue"": ""Article error"", ""explanation"": ""Use 'an' before vowel sounds"", ""original"": ""a apple"", ""replacement"": ""an apple""}
  ]
}

IMPORTANT: The 'original' field must contain the EXACT text from the input that needs changing. The 'replacement' field must contain the corrected version.

If the text is perfect: {""corrected_text"": ""[exact original text]"", ""issues"": []}"
                    },
                    new
                    {
                        role = "user",
                        content = $"Please check this text:\n\n{text}"
                    }
                },
                temperature = 0.3,
                max_tokens = 1000
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await HttpClient.PostAsync(OpenRouterApiUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                
                // Check if it's a rate limit error
                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests || 
                    error.Contains("rate") || error.Contains("limit"))
                {
                    throw new HttpRequestException("Rate limited");
                }
                
                suggestions.Add(new Suggestion(
                    $"API Error: {response.StatusCode}",
                    $"Failed to get AI suggestions.\n{error}"));
                return suggestions;
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<OpenAiResponse>(responseJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (result?.Choices != null && result.Choices.Length > 0)
            {
                var messageContent = result.Choices[0].Message?.Content;
                if (!string.IsNullOrEmpty(messageContent))
                {
                    suggestions.AddRange(ParseGptResponse(messageContent, text));
                    
                    // Don't add model info here - it's added in the calling function
                }
            }
        }
        catch (HttpRequestException)
        {
            throw; // Re-throw to try next model
        }
        catch (Exception ex)
        {
            suggestions.Add(new Suggestion("Error", $"Failed to process AI response: {ex.Message}"));
        }

        return suggestions;
    }

    private static List<Suggestion> ParseGptResponse(string content, string originalText = "")
    {
        var suggestions = new List<Suggestion>();

        try
        {
            // Try to extract JSON from the response (AI might wrap it in markdown)
            var jsonStart = content.IndexOf('{');
            var jsonEnd = content.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonContent = content.Substring(jsonStart, jsonEnd - jsonStart + 1);
                
                try
                {
                    var response = JsonSerializer.Deserialize<GptResponse>(jsonContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (response != null)
                    {
                        // Add the corrected text as the first suggestion
                        if (!string.IsNullOrEmpty(response.CorrectedText))
                        {
                            suggestions.Add(new Suggestion(
                                "✓ Corrected Version Available",
                                "Click 'Apply Corrections' to replace your text with the corrected version",
                                response.CorrectedText));
                        }

                        // Add individual issues with position info
                        if (response.Issues != null && response.Issues.Length > 0)
                        {
                            foreach (var issue in response.Issues)
                            {
                                var message = issue.Issue ?? "Grammar/Style issue";
                                var suggestionText = issue.Explanation ?? "";
                                if (!string.IsNullOrEmpty(issue.Original) && !string.IsNullOrEmpty(issue.Replacement))
                                {
                                    suggestionText += $"\n\n'{issue.Original}' → '{issue.Replacement}'";
                                }
                                
                                // Try to find position in original text
                                int startIndex = -1;
                                int length = 0;
                                string? replacementText = null;
                                
                                if (!string.IsNullOrEmpty(originalText) && !string.IsNullOrEmpty(issue.Original))
                                {
                                    startIndex = originalText.IndexOf(issue.Original, StringComparison.OrdinalIgnoreCase);
                                    if (startIndex >= 0)
                                    {
                                        length = issue.Original.Length;
                                        replacementText = issue.Replacement;
                                    }
                                }
                                
                                var suggestion = new Suggestion(
                                    $"AI: {message}", 
                                    suggestionText,
                                    null, // CorrectedText
                                    startIndex,
                                    length,
                                    replacementText);
                                
                                suggestions.Add(suggestion);
                            }
                        }
                        else if (string.IsNullOrEmpty(response.CorrectedText))
                        {
                            // No issues and no corrected text means perfect text
                            suggestions.Add(new Suggestion("✓ No issues found", "Your text is grammatically correct!"));
                        }
                    }
                }
                catch (JsonException)
                {
                    // If structured parsing fails, try to extract corrected text manually
                    if (content.Contains("corrected_text"))
                    {
                        var correctedStart = content.IndexOf("\"corrected_text\"");
                        if (correctedStart >= 0)
                        {
                            var valueStart = content.IndexOf(":", correctedStart) + 1;
                            var valueEnd = content.IndexOf(",", valueStart);
                            if (valueEnd < 0) valueEnd = content.IndexOf("}", valueStart);
                            
                            if (valueStart > 0 && valueEnd > valueStart)
                            {
                                var correctedText = content.Substring(valueStart, valueEnd - valueStart)
                                    .Trim()
                                    .Trim('"')
                                    .Replace("\\n", "\n")
                                    .Replace("\\\"", "\"");
                                
                                if (!string.IsNullOrEmpty(correctedText))
                                {
                                    suggestions.Add(new Suggestion(
                                        "✓ Corrected Version Available",
                                        "Click 'Apply Corrections' to replace your text",
                                        correctedText));
                                }
                            }
                        }
                    }
                    
                    // Add the raw response as fallback
                    suggestions.Add(new Suggestion("AI Analysis", content));
                }
            }
            else
            {
                // If no JSON object found, treat the whole response as a single suggestion
                suggestions.Add(new Suggestion("AI Analysis", content));
            }
        }
        catch (Exception ex)
        {
            // If parsing fails, return the raw content with error info
            suggestions.Add(new Suggestion("Parsing Error", $"Could not parse AI response: {ex.Message}\n\nRaw response:\n{content}"));
        }

        return suggestions;
    }
}

// OpenRouter API response models (compatible with OpenAI format)
public class OpenAiResponse
{
    public Choice[]? Choices { get; set; }
}

public class Choice
{
    public Message? Message { get; set; }
}

public class Message
{
    public string? Content { get; set; }
}

// GPT response format
public class GptResponse
{
    public string? CorrectedText { get; set; }
    public GptIssue[]? Issues { get; set; }
}

// AI issue format
public class GptIssue
{
    public string? Issue { get; set; }
    public string? Explanation { get; set; }
    public string? Suggestion { get; set; }
    public string? Original { get; set; }
    public string? Replacement { get; set; }
}
