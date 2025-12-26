using System.Collections.ObjectModel;
using System.Windows;
using GrammarAssistant.Models;
using GrammarAssistant.Services;

namespace GrammarAssistant;

public partial class MainWindow : Window
{
    public ObservableCollection<Suggestion> Suggestions { get; } = new();

    private readonly GrammarChecker _grammarChecker = new();
    private readonly ClipboardService _clipboardService = new();

    public MainWindow()
    {
        InitializeComponent();
        SuggestionsList.ItemsSource = Suggestions;
    }

    private void OnCheckTextClick(object sender, RoutedEventArgs e)
    {
        RunChecks(InputText.Text);
    }

    private void OnCheckClipboardClick(object sender, RoutedEventArgs e)
    {
        var text = _clipboardService.ReadText();
        if (!string.IsNullOrWhiteSpace(text))
        {
            InputText.Text = text;
            RunChecks(text);
            return;
        }

        MessageBox.Show("Clipboard is empty or does not contain text.", "No text", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void RunChecks(string? text)
    {
        Suggestions.Clear();

        if (string.IsNullOrWhiteSpace(text))
        {
            Suggestions.Add(new Suggestion("Nothing to check. Paste or type some text first."));
            return;
        }

        foreach (var suggestion in _grammarChecker.Check(text))
        {
            Suggestions.Add(suggestion);
        }

        if (Suggestions.Count == 0)
        {
            Suggestions.Add(new Suggestion("No issues found in this text."));
        }
    }
}
