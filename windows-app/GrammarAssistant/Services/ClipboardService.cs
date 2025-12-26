using System.Windows;

namespace GrammarAssistant.Services;

public class ClipboardService
{
    public string? ReadText()
    {
        if (!Clipboard.ContainsText())
        {
            return null;
        }

        return Clipboard.GetText(TextDataFormat.UnicodeText);
    }
}
