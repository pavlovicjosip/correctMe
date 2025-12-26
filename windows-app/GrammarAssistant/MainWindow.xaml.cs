using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using GrammarAssistant.Models;
using GrammarAssistant.Services;
using Microsoft.Win32;
using Wpf.Ui.Controls;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using MessageBoxResult = System.Windows.MessageBoxResult;

namespace GrammarAssistant
{
    /// <summary>
    /// Converts null/empty string to Collapsed, non-null to Visible
    /// </summary>
    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public partial class MainWindow : FluentWindow
{
    public ObservableCollection<Suggestion> Suggestions { get; } = new();

    private readonly GrammarChecker _grammarChecker = new();
    private readonly GptGrammarService _gptService = new();
    private readonly ClipboardService _clipboardService = new();
    private readonly CacheService _cacheService = new();
    private readonly HistoryService _historyService = new();
    private readonly StatisticsService _statisticsService = new();
    
    private readonly DispatcherTimer _realtimeCheckTimer;
    private bool _isDarkMode = false;
    private string _currentLanguage = "en-US";
    private string _currentStyle = "casual";
    private List<Suggestion> _currentSuggestions = new();
    private Suggestion? _currentTooltipSuggestion = null;
    private bool _isTooltipLocked = false;
    private int _lastTooltipErrorIndex = -1;
    private string? _currentRewriteText = null;

    public MainWindow()
    {
        InitializeComponent();
        SuggestionsList.ItemsSource = Suggestions;
        
        // Setup real-time checking timer
        _realtimeCheckTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _realtimeCheckTimer.Tick += OnRealtimeCheckTick;
        
        // Enable real-time checking by default
        RealtimeCheckBox.IsChecked = true;
        
        // Apply dark mode by default
        _isDarkMode = true;
        ApplyTheme();
        
        // Update AI button text if not configured
        if (!_gptService.IsConfigured)
        {
            CheckWithAiButton.Content = "AI Check (Setup)";
            CheckWithAiButton.ToolTip = "Create api-key.txt file with your OpenRouter API key";
        }

        // Setup keyboard shortcuts
        SetupKeyboardShortcuts();
        
        // Setup text input mouse tracking for custom tooltip
        InputText.MouseMove += OnInputTextMouseMove;
        InputText.MouseLeave += OnInputTextMouseLeave;
        
        // Initialize with placeholder text
        var paragraph = new Paragraph(new Run("Type or paste your text here..."))
        {
            Foreground = new SolidColorBrush(Colors.Gray)
        };
        InputText.Document.Blocks.Clear();
        InputText.Document.Blocks.Add(paragraph);
        InputText.GotFocus += OnInputTextGotFocus;
    }

    private void OnInputTextMouseLeave(object sender, MouseEventArgs e)
    {
        // Don't hide tooltip when mouse leaves text area - let user move to tooltip
        // Tooltip will close when mouse leaves the tooltip border
    }

    private void OnInputTextGotFocus(object sender, RoutedEventArgs e)
    {
        // Clear placeholder on first focus
        var text = new TextRange(InputText.Document.ContentStart, InputText.Document.ContentEnd).Text;
        if (text.Trim() == "Type or paste your text here...")
        {
            InputText.Document.Blocks.Clear();
            InputText.Document.Blocks.Add(new Paragraph());
        }
        
        InputText.GotFocus -= OnInputTextGotFocus;
    }

    private void OnInputTextMouseMove(object sender, MouseEventArgs e)
    {
        // If tooltip is locked (mouse is over it), don't do anything
        if (_isTooltipLocked)
        {
            return;
        }
        
        if (_currentSuggestions.Count == 0)
        {
            TooltipPopup.IsOpen = false;
            return;
        }

        try
        {
            // Get cursor position
            var position = InputText.GetPositionFromPoint(e.GetPosition(InputText), true);
            if (position == null)
            {
                // Don't close - mouse might be moving to tooltip
                return;
            }
            
            // Check if we're actually over text content
            var rect = position.GetCharacterRect(LogicalDirection.Forward);
            if (rect.IsEmpty || rect.Width == 0)
            {
                // Not over actual text
                if (!TooltipPopup.IsOpen)
                    return;
            }
            
            // Calculate character offset - use trimmed text range
            var textBeforeCursor = new TextRange(InputText.Document.ContentStart, position).Text;
            // Remove trailing \r\n if present
            if (textBeforeCursor.EndsWith("\r\n"))
                textBeforeCursor = textBeforeCursor.Substring(0, textBeforeCursor.Length - 2);
            var offset = textBeforeCursor.Length;
            
            // Get the full text to validate we're over actual content
            var fullText = GetPlainText();
            if (offset < 0 || offset > fullText.Length)
            {
                return;
            }
            
            // Check if we're over a word (not whitespace)
            bool isOverWord = false;
            if (offset > 0 && offset <= fullText.Length)
            {
                // Check character before cursor
                var charBefore = offset > 0 ? fullText[offset - 1] : ' ';
                // Check character at cursor (if not at end)
                var charAt = offset < fullText.Length ? fullText[offset] : ' ';
                isOverWord = !char.IsWhiteSpace(charBefore) || !char.IsWhiteSpace(charAt);
            }
            
            if (!isOverWord)
            {
                // Not over a word - keep tooltip open if already showing (for moving to tooltip)
                return;
            }
            
            // Find matching suggestion that has a replacement (can be fixed)
            var matchingSuggestion = _currentSuggestions.FirstOrDefault(s => 
                s.StartIndex >= 0 && 
                s.Length > 0 &&
                !string.IsNullOrEmpty(s.ReplacementText) && // Only show tooltip if we can fix it
                offset >= s.StartIndex && 
                offset <= s.StartIndex + s.Length);
            
            if (matchingSuggestion != null)
            {
                // Only update tooltip if it's a different error or tooltip is closed
                if (!TooltipPopup.IsOpen || _lastTooltipErrorIndex != matchingSuggestion.StartIndex)
                {
                    // Store current suggestion for apply button
                    _currentTooltipSuggestion = matchingSuggestion;
                    _lastTooltipErrorIndex = matchingSuggestion.StartIndex;
                    
                    // Show custom tooltip
                    TooltipTitle.Text = $"❌ {matchingSuggestion.Message}";
                    TooltipText.Text = $"Suggestion: {matchingSuggestion.ReplacementText}";
                    
                    // Always show buttons since we have a replacement
                    TooltipButtons.Visibility = Visibility.Visible;
                    
                    // Set position and open tooltip
                    var mousePos = e.GetPosition(InputText);
                    TooltipPopup.HorizontalOffset = mousePos.X + 20;
                    TooltipPopup.VerticalOffset = mousePos.Y - 80;
                    TooltipPopup.IsOpen = true;
                }
            }
            // If not over error but tooltip is open, keep it open so user can move to it
            // Tooltip will close when mouse leaves the tooltip border
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Tooltip error: {ex.Message}");
        }
    }

    private void OnApplySuggestionClick(object sender, RoutedEventArgs e)
    {
        if (_currentTooltipSuggestion == null || 
            _currentTooltipSuggestion.StartIndex < 0 || 
            _currentTooltipSuggestion.Length <= 0 ||
            string.IsNullOrEmpty(_currentTooltipSuggestion.ReplacementText))
        {
            return;
        }

        try
        {
            // Get the full text (trimmed)
            var fullText = GetPlainText();
            
            var startIndex = _currentTooltipSuggestion.StartIndex;
            var length = _currentTooltipSuggestion.Length;
            var replacement = _currentTooltipSuggestion.ReplacementText;
            
            // Validate indices
            if (startIndex < 0 || startIndex + length > fullText.Length)
            {
                System.Diagnostics.Debug.WriteLine($"Invalid indices: start={startIndex}, length={length}, textLength={fullText.Length}");
                return;
            }
            
            System.Diagnostics.Debug.WriteLine($"Applying: '{fullText.Substring(startIndex, length)}' -> '{replacement}'");
            
            var newText = fullText.Substring(0, startIndex) + 
                         replacement + 
                         fullText.Substring(startIndex + length);
            
            // Remove the fixed suggestion from the list
            _currentSuggestions.Remove(_currentTooltipSuggestion);
            
            // Update the text
            InputText.Document.Blocks.Clear();
            InputText.Document.Blocks.Add(new Paragraph(new Run(newText)));
            
            // Hide tooltip and reset
            TooltipPopup.IsOpen = false;
            _isTooltipLocked = false;
            _lastTooltipErrorIndex = -1;
            _currentTooltipSuggestion = null;
            
            // Don't re-run check - just close tooltip
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error applying suggestion: {ex.Message}");
        }
    }

    private void OnIgnoreSuggestionClick(object sender, RoutedEventArgs e)
    {
        // Just close the tooltip and unlock
        TooltipPopup.IsOpen = false;
        _currentTooltipSuggestion = null;
        _isTooltipLocked = false;
        _lastTooltipErrorIndex = -1;
    }

    private void OnSuggestionApplyClick(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button button && button.Tag is Suggestion suggestion)
        {
            if (suggestion.StartIndex < 0 || suggestion.Length <= 0 || string.IsNullOrEmpty(suggestion.ReplacementText))
            {
                return;
            }

            try
            {
                var fullText = GetPlainText();
                var startIndex = suggestion.StartIndex;
                var length = suggestion.Length;
                var replacement = suggestion.ReplacementText;

                // Validate indices
                if (startIndex < 0 || startIndex + length > fullText.Length)
                {
                    return;
                }

                var newText = fullText.Substring(0, startIndex) + 
                             replacement + 
                             fullText.Substring(startIndex + length);

                // Remove the fixed suggestion from the list
                _currentSuggestions.Remove(suggestion);
                Suggestions.Remove(suggestion);

                // Update the text
                InputText.Document.Blocks.Clear();
                InputText.Document.Blocks.Add(new Paragraph(new Run(newText)));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error applying suggestion: {ex.Message}");
            }
        }
    }

    private void OnTooltipMouseEnter(object sender, MouseEventArgs e)
    {
        // Keep tooltip locked when mouse is over it
        _isTooltipLocked = true;
        System.Diagnostics.Debug.WriteLine("Tooltip MouseEnter - Locked");
    }

    private void OnTooltipMouseLeave(object sender, MouseEventArgs e)
    {
        // Unlock and close tooltip when mouse leaves
        _isTooltipLocked = false;
        TooltipPopup.IsOpen = false;
        _currentTooltipSuggestion = null;
        _lastTooltipErrorIndex = -1;
        System.Diagnostics.Debug.WriteLine("Tooltip MouseLeave - Closed");
    }

    private void OnTooltipBorderMouseEnter(object sender, MouseEventArgs e)
    {
        // Keep tooltip locked when mouse is over the border
        _isTooltipLocked = true;
        System.Diagnostics.Debug.WriteLine("Border MouseEnter - Locked");
    }

    private void OnTooltipBorderMouseLeave(object sender, MouseEventArgs e)
    {
        // Unlock and close tooltip when mouse leaves the border
        _isTooltipLocked = false;
        TooltipPopup.IsOpen = false;
        _currentTooltipSuggestion = null;
        _lastTooltipErrorIndex = -1;
        System.Diagnostics.Debug.WriteLine("Border MouseLeave - Closed");
    }

    private void UpdateTextHighlighting()
    {
        // Disabled for now - RichTextBox position calculation is complex
        // Tooltips will still work on hover
    }

    private void SetupKeyboardShortcuts()
    {
        var undoGesture = new KeyGesture(Key.Z, ModifierKeys.Control);
        var redoGesture = new KeyGesture(Key.Y, ModifierKeys.Control);
        var newGesture = new KeyGesture(Key.N, ModifierKeys.Control);
        var openGesture = new KeyGesture(Key.O, ModifierKeys.Control);
        var saveGesture = new KeyGesture(Key.S, ModifierKeys.Control);
        var copyGesture = new KeyGesture(Key.C, ModifierKeys.Control | ModifierKeys.Shift);

        InputBindings.Add(new KeyBinding(new RelayCommand(OnUndoClick), undoGesture));
        InputBindings.Add(new KeyBinding(new RelayCommand(OnRedoClick), redoGesture));
        InputBindings.Add(new KeyBinding(new RelayCommand(OnNewClick), newGesture));
        InputBindings.Add(new KeyBinding(new RelayCommand(OnOpenClick), openGesture));
        InputBindings.Add(new KeyBinding(new RelayCommand(OnSaveClick), saveGesture));
        InputBindings.Add(new KeyBinding(new RelayCommand(OnCopyCorrectedClick), copyGesture));
    }

    #region Menu and Settings Handlers

    private void OnNewClick(object? sender = null, RoutedEventArgs? e = null)
    {
        var text = new TextRange(InputText.Document.ContentStart, InputText.Document.ContentEnd).Text;
        if (!string.IsNullOrWhiteSpace(text))
        {
            var result = MessageBox.Show("Clear current text?", "New Document", 
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;
        }

        InputText.Document.Blocks.Clear();
        InputText.Document.Blocks.Add(new Paragraph());
        Suggestions.Clear();
        _currentSuggestions.Clear();
        ApplyCorrectionsButton.IsEnabled = false;
        CopyCorrectedButton.IsEnabled = false;
    }

    private void OnOpenClick(object? sender = null, RoutedEventArgs? e = null)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
            Title = "Open Text File"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var fileText = File.ReadAllText(dialog.FileName);
                InputText.Document.Blocks.Clear();
                InputText.Document.Blocks.Add(new Paragraph(new Run(fileText)));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening file: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void OnSaveClick(object? sender = null, RoutedEventArgs? e = null)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
            Title = "Save Text File",
            DefaultExt = ".txt"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var text = new TextRange(InputText.Document.ContentStart, InputText.Document.ContentEnd).Text;
                File.WriteAllText(dialog.FileName, text);
                MessageBox.Show("File saved successfully!", "Success", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving file: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    #endregion

    #region Text Input and Checking

    private void OnInputTextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateReadabilityStats();
        
        // Real-time checking: restart timer on every keystroke
        if (RealtimeCheckBox.IsChecked == true)
        {
            _realtimeCheckTimer.Stop();
            _realtimeCheckTimer.Start();
        }
    }

    private void OnRealtimeCheckChanged(object sender, RoutedEventArgs e)
    {
        if (RealtimeCheckBox.IsChecked == true)
        {
            // Start timer when enabled
            var text = new TextRange(InputText.Document.ContentStart, InputText.Document.ContentEnd).Text;
            if (!string.IsNullOrWhiteSpace(text))
            {
                _realtimeCheckTimer.Start();
            }
        }
        else
        {
            // Stop timer when disabled
            _realtimeCheckTimer.Stop();
        }
    }

    private async void OnRealtimeCheckTick(object? sender, EventArgs e)
    {
        _realtimeCheckTimer.Stop();
        
        var text = GetPlainText();
        if (!string.IsNullOrWhiteSpace(text))
        {
            // Run AI Check in real-time for better suggestions
            await RunAiChecksAsync(text);
        }
    }
    
    /// <summary>
    /// Gets plain text from RichTextBox, trimming the trailing newline that RichTextBox adds
    /// </summary>
    private string GetPlainText()
    {
        var text = new TextRange(InputText.Document.ContentStart, InputText.Document.ContentEnd).Text;
        // RichTextBox always adds \r\n at the end, remove it for accurate position matching
        if (text.EndsWith("\r\n"))
            text = text.Substring(0, text.Length - 2);
        return text;
    }

    private void UpdateReadabilityStats()
    {
        var text = new TextRange(InputText.Document.ContentStart, InputText.Document.ContentEnd).Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            ReadabilityText.Text = "Words: 0 | Sentences: 0 | Readability: N/A";
            return;
        }

        var score = StatisticsService.CalculateReadability(text);
        ReadabilityText.Text = $"Words: {score.WordCount} | Sentences: {score.SentenceCount} | " +
                              $"Readability: {score.GetReadabilityLevel()} ({score.FleschReadingEase:F0})";
    }

    private void OnCheckTextClick(object sender, RoutedEventArgs e)
    {
        var text = GetPlainText();
        RunChecks(text);
    }

    private async void OnCheckWithAiClick(object sender, RoutedEventArgs e)
    {
        var text = GetPlainText();
        await RunAiChecksAsync(text);
    }

    private void OnCheckClipboardClick(object sender, RoutedEventArgs e)
    {
        var text = _clipboardService.ReadText();
        if (!string.IsNullOrWhiteSpace(text))
        {
            InputText.Document.Blocks.Clear();
            InputText.Document.Blocks.Add(new Paragraph(new Run(text)));
            RunChecks(text);
            return;
        }

        MessageBox.Show("Clipboard is empty or does not contain text.", "No text", 
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OnApplyCorrectionsClick(object sender, RoutedEventArgs e)
    {
        var correctedSuggestion = Suggestions.FirstOrDefault(s => !string.IsNullOrEmpty(s.CorrectedText));
        
        if (correctedSuggestion != null && !string.IsNullOrEmpty(correctedSuggestion.CorrectedText))
        {
            var result = MessageBox.Show(
                "Replace your text with the AI-corrected version?",
                "Apply Corrections",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                var originalText = new TextRange(InputText.Document.ContentStart, InputText.Document.ContentEnd).Text;
                InputText.Document.Blocks.Clear();
                InputText.Document.Blocks.Add(new Paragraph(new Run(correctedSuggestion.CorrectedText)));
                
                // Add to history
                _historyService.AddEntry(originalText, correctedSuggestion.CorrectedText);
                UpdateUndoRedoButtons();
                
                Suggestions.Clear();
                _currentSuggestions.Clear();
                Suggestions.Add(new Suggestion("✓ Corrections Applied", 
                    "Your text has been updated. Use Ctrl+Z to undo."));
                ApplyCorrectionsButton.IsEnabled = false;
                CopyCorrectedButton.IsEnabled = false;
            }
        }
        else
        {
            MessageBox.Show("No corrected text available. Run an AI check first.", "No Corrections", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void RunChecks(string? text)
    {
        Suggestions.Clear();
        _currentSuggestions.Clear();
        ApplyCorrectionsButton.IsEnabled = false;
        CopyCorrectedButton.IsEnabled = false;

        if (string.IsNullOrWhiteSpace(text))
        {
            Suggestions.Add(new Suggestion("Nothing to check. Paste or type some text first."));
            return;
        }

        // Check cache first
        if (_cacheService.TryGetCached(text, out var cachedSuggestions) && cachedSuggestions != null)
        {
            Suggestions.Add(new Suggestion("✓ Results from cache", "Instant results!"));
            foreach (var suggestion in cachedSuggestions)
            {
                Suggestions.Add(suggestion);
                _currentSuggestions.Add(suggestion);
            }
            
            // Check if cached results have corrected text
            if (cachedSuggestions.Any(s => !string.IsNullOrEmpty(s.CorrectedText)))
            {
                ApplyCorrectionsButton.IsEnabled = true;
                CopyCorrectedButton.IsEnabled = true;
            }
            
            _statisticsService.RecordCheck();
            return;
        }

        var results = _grammarChecker.Check(text).ToList();
        
        System.Diagnostics.Debug.WriteLine($"Quick Check found {results.Count} issues");
        
        // Generate corrected text by applying all fixes with ReplacementText
        var fixableResults = results.Where(r => !string.IsNullOrEmpty(r.ReplacementText)).OrderByDescending(r => r.StartIndex).ToList();
        string? correctedText = null;
        
        if (fixableResults.Count > 0)
        {
            correctedText = text;
            foreach (var fix in fixableResults)
            {
                if (fix.StartIndex >= 0 && fix.Length > 0 && fix.StartIndex + fix.Length <= correctedText.Length)
                {
                    correctedText = correctedText.Substring(0, fix.StartIndex) + 
                                   fix.ReplacementText + 
                                   correctedText.Substring(fix.StartIndex + fix.Length);
                }
            }
            
            // Add corrected text as first suggestion
            if (correctedText != text)
            {
                Suggestions.Add(new Suggestion(
                    "✓ Corrected Version Available",
                    $"Found {fixableResults.Count} fixable issues. Click 'Apply All' to fix them.",
                    correctedText));
                ApplyCorrectionsButton.IsEnabled = true;
                CopyCorrectedButton.IsEnabled = true;
            }
        }
        
        foreach (var suggestion in results)
        {
            Suggestions.Add(suggestion);
            _currentSuggestions.Add(suggestion);
            
            System.Diagnostics.Debug.WriteLine($"  - {suggestion.Message} at position {suggestion.StartIndex}, length {suggestion.Length}, replacement: {suggestion.ReplacementText}");
            
            // Record error statistics
            if (suggestion.Message.StartsWith("Spelling:") || 
                suggestion.Message.StartsWith("AI:") ||
                suggestion.Message.Contains("error"))
            {
                _statisticsService.RecordError(suggestion.Message.Split(':')[0]);
            }
        }

        if (results.Count == 0)
        {
            Suggestions.Add(new Suggestion("No issues found!", "Your text looks good."));
        }

        // Cache results
        _cacheService.Cache(text, results);
        _statisticsService.RecordCheck();
    }

    private async Task RunAiChecksAsync(string? text)
    {
        Suggestions.Clear();
        _currentSuggestions.Clear();
        ApplyCorrectionsButton.IsEnabled = false;
        CopyCorrectedButton.IsEnabled = false;

        if (string.IsNullOrWhiteSpace(text))
        {
            Suggestions.Add(new Suggestion("Nothing to check. Paste or type some text first."));
            return;
        }

        // Check cache first
        if (_cacheService.TryGetCached("AI:" + text, out var cachedSuggestions) && cachedSuggestions != null)
        {
            Suggestions.Add(new Suggestion("✓ AI Results from cache", "Instant results!"));
            foreach (var suggestion in cachedSuggestions)
            {
                Suggestions.Add(suggestion);
                _currentSuggestions.Add(suggestion);
            }
            
            if (Suggestions.Any(s => !string.IsNullOrEmpty(s.CorrectedText)))
            {
                ApplyCorrectionsButton.IsEnabled = true;
                CopyCorrectedButton.IsEnabled = true;
            }
            
            _statisticsService.RecordCheck();
            return;
        }

        SetButtonsEnabled(false);
        Suggestions.Add(new Suggestion("Analyzing with AI...", "This may take a few seconds"));

        try
        {
            // Run Quick Check first to get position data for tooltips
            var quickCheckResults = _grammarChecker.Check(text).ToList();
            foreach (var suggestion in quickCheckResults)
            {
                _currentSuggestions.Add(suggestion);
            }
            
            System.Diagnostics.Debug.WriteLine($"AI Check: Added {quickCheckResults.Count} quick check results for tooltips");
            
            // Then run AI check for corrections
            var results = await _gptService.CheckWithGptAsync(text);
            
            Suggestions.Clear();
            
            foreach (var suggestion in results)
            {
                Suggestions.Add(suggestion);
                
                // Record error statistics
                if (suggestion.Message.StartsWith("AI:"))
                {
                    _statisticsService.RecordError(suggestion.Message);
                }
            }

            // Enable buttons if we have a corrected version
            if (Suggestions.Any(s => !string.IsNullOrEmpty(s.CorrectedText)))
            {
                ApplyCorrectionsButton.IsEnabled = true;
                CopyCorrectedButton.IsEnabled = true;
            }

            if (Suggestions.Count == 0)
            {
                Suggestions.Add(new Suggestion("No issues found!", "AI analysis complete - your text looks excellent."));
            }

            // Cache AI results
            _cacheService.Cache("AI:" + text, results.ToList());
            _statisticsService.RecordCheck();
        }
        catch (Exception ex)
        {
            Suggestions.Clear();
            _currentSuggestions.Clear();
            Suggestions.Add(new Suggestion("Error during AI check", ex.Message));
        }
        finally
        {
            SetButtonsEnabled(true);
        }
    }

    #endregion

    #region Helper Methods

    private void OnUndoClick(object? sender = null, RoutedEventArgs? e = null)
    {
        var entry = _historyService.Undo();
        if (entry != null)
        {
            InputText.Document.Blocks.Clear();
            InputText.Document.Blocks.Add(new Paragraph(new Run(entry.OriginalText)));
            UpdateUndoRedoButtons();
        }
    }

    private void OnRedoClick(object? sender = null, RoutedEventArgs? e = null)
    {
        var entry = _historyService.Redo();
        if (entry != null)
        {
            InputText.Document.Blocks.Clear();
            InputText.Document.Blocks.Add(new Paragraph(new Run(entry.CorrectedText)));
            UpdateUndoRedoButtons();
        }
    }

    private void OnCopyCorrectedClick(object? sender = null, RoutedEventArgs? e = null)
    {
        var correctedSuggestion = Suggestions.FirstOrDefault(s => !string.IsNullOrEmpty(s.CorrectedText));
        if (correctedSuggestion != null && !string.IsNullOrEmpty(correctedSuggestion.CorrectedText))
        {
            Clipboard.SetText(correctedSuggestion.CorrectedText);
            MessageBox.Show("Corrected text copied to clipboard!", "Success", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void OnShowStatsClick(object sender, RoutedEventArgs e)
    {
        var stats = $"Total Checks: {_statisticsService.TotalChecks}\n" +
                   $"Total Errors Found: {_statisticsService.TotalErrors}\n" +
                   $"Average Errors per Check: {_statisticsService.AverageErrorsPerCheck:F2}\n\n" +
                   "Top Errors:\n";

        var topErrors = _statisticsService.GetTopErrors(5);
        foreach (var error in topErrors)
        {
            stats += $"  • {error.Key}: {error.Value}\n";
        }

        MessageBox.Show(stats, "Statistics", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OnShowHistoryClick(object sender, RoutedEventArgs e)
    {
        var history = _historyService.GetHistory().Take(10).ToList();
        if (history.Count == 0)
        {
            MessageBox.Show("No history available.", "History", 
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var historyText = "Recent Corrections:\n\n";
        foreach (var entry in history)
        {
            var preview = entry.OriginalText.Length > 50 
                ? entry.OriginalText[..50] + "..." 
                : entry.OriginalText;
            historyText += $"[{entry.Timestamp:HH:mm:ss}] {preview}\n";
        }

        MessageBox.Show(historyText, "History", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        var settingsMessage = $"Current Settings:\n\n" +
                             $"Language: {_currentLanguage}\n" +
                             $"Style: {_currentStyle}\n\n" +
                             "Change settings:\n" +
                             "• Language: en-US, en-GB, es, fr, de\n" +
                             "• Style: formal, casual, academic, business";
        
        MessageBox.Show(settingsMessage, "Settings", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OnToggleDarkModeClick(object sender, RoutedEventArgs e)
    {
        _isDarkMode = !_isDarkMode;
        ApplyTheme();
    }

    private void ApplyTheme()
    {
        if (_isDarkMode)
        {
            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(Wpf.Ui.Appearance.ApplicationTheme.Dark);
            DarkModeButton.Content = "Light";
        }
        else
        {
            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(Wpf.Ui.Appearance.ApplicationTheme.Light);
            DarkModeButton.Content = "Dark";
        }
    }

    #endregion

    #region Helper Methods

    private void SetButtonsEnabled(bool enabled)
    {
        CheckTextButton.IsEnabled = enabled;
        CheckWithAiButton.IsEnabled = enabled;
        CheckClipboardButton.IsEnabled = enabled;
        RewriteButton.IsEnabled = enabled;
    }

    private void UpdateUndoRedoButtons()
    {
        UndoButton.IsEnabled = _historyService.CanUndo;
        RedoButton.IsEnabled = _historyService.CanRedo;
    }

    protected override void OnClosed(EventArgs e)
    {
        _grammarChecker.Dispose();
        _realtimeCheckTimer.Stop();
        base.OnClosed(e);
    }

    #endregion

    #region AI Rewrite

    private async void OnRewriteClick(object sender, RoutedEventArgs e)
    {
        var text = GetPlainText();
        if (string.IsNullOrWhiteSpace(text))
        {
            MessageBox.Show("Please enter some text to rewrite.", "No Text", 
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!_gptService.IsConfigured)
        {
            MessageBox.Show("AI not configured. Create an 'api-key.txt' file with your OpenRouter API key.", 
                "AI Not Configured", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        await GenerateRewrite(text);
    }

    private async Task GenerateRewrite(string text)
    {
        var selectedStyle = (RewriteStyleCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Professional";
        
        RewritePanel.Visibility = Visibility.Visible;
        RewritePreviewText.Text = "Generating rewrite...";
        RewriteButton.IsEnabled = false;
        RegenerateButton.IsEnabled = false;
        ApplyRewriteButton.IsEnabled = false;

        try
        {
            var rewrittenText = await _gptService.RewriteTextAsync(text, selectedStyle);
            
            if (!string.IsNullOrEmpty(rewrittenText))
            {
                _currentRewriteText = rewrittenText;
                RewritePreviewText.Text = rewrittenText;
                ApplyRewriteButton.IsEnabled = true;
            }
            else
            {
                RewritePreviewText.Text = "Failed to generate rewrite. Please try again.";
                _currentRewriteText = null;
            }
        }
        catch (Exception ex)
        {
            RewritePreviewText.Text = $"Error: {ex.Message}";
            _currentRewriteText = null;
        }
        finally
        {
            RewriteButton.IsEnabled = true;
            RegenerateButton.IsEnabled = true;
        }
    }

    private async void OnRegenerateRewrite(object sender, RoutedEventArgs e)
    {
        var text = GetPlainText();
        if (!string.IsNullOrWhiteSpace(text))
        {
            await GenerateRewrite(text);
        }
    }

    private void OnCloseRewritePanel(object sender, RoutedEventArgs e)
    {
        RewritePanel.Visibility = Visibility.Collapsed;
        _currentRewriteText = null;
    }

    private void OnCopyRewrite(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_currentRewriteText))
        {
            Clipboard.SetText(_currentRewriteText);
            MessageBox.Show("Rewritten text copied to clipboard!", "Copied", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void OnApplyRewrite(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_currentRewriteText))
        {
            var originalText = GetPlainText();
            
            // Add to history
            _historyService.AddEntry(originalText, _currentRewriteText);
            UpdateUndoRedoButtons();
            
            // Update the text
            InputText.Document.Blocks.Clear();
            InputText.Document.Blocks.Add(new Paragraph(new Run(_currentRewriteText)));
            
            // Close the panel
            RewritePanel.Visibility = Visibility.Collapsed;
            _currentRewriteText = null;
            
            // Clear suggestions
            Suggestions.Clear();
            _currentSuggestions.Clear();
            Suggestions.Add(new Suggestion("✓ Rewrite Applied", 
                "Your text has been rewritten. Use Ctrl+Z to undo."));
        }
    }

    #endregion
}

// Simple command implementation for keyboard shortcuts
public class RelayCommand : ICommand
{
    private readonly Action<object?, RoutedEventArgs?> _execute;

    public RelayCommand(Action<object?, RoutedEventArgs?> execute)
    {
        _execute = execute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter)
    {
        _execute(null, null);
    }
}

} // end namespace GrammarAssistant
