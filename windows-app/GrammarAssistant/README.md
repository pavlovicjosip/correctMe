# Grammar Assistant - Professional Edition

A powerful Windows desktop application for comprehensive grammar, spelling, and writing style checking with AI integration.

## 🌟 Features

### Core Functionality
- **Quick Check**: Fast local grammar and spell checking using Hunspell dictionary
- **AI Check**: Advanced grammar, style, and clarity suggestions powered by GPT-4o-mini
- **Real-time Checking**: Optional live grammar checking as you type
- **Smart Caching**: Instant results for previously checked text
- **Multi-language Support**: English (US/UK), Spanish, French, German

### Writing Tools
- **Apply Corrections**: One-click to apply all AI-suggested corrections
- **Copy Corrected Text**: Quickly copy the corrected version to clipboard
- **Undo/Redo**: Full history with unlimited undo/redo (Ctrl+Z / Ctrl+Y)
- **Readability Analysis**: Real-time Flesch Reading Ease and grade level scores
- **Word/Sentence Count**: Live statistics as you type

### User Interface
- **Dark Mode**: Easy on the eyes for night-time writing
- **Resizable Panels**: Adjust layout to your preference with draggable splitter
- **Clean Modern Design**: Distraction-free writing environment
- **Keyboard Shortcuts**: Fast access to all features

### Writing Styles
Choose your writing context for better suggestions:
- **Formal**: Professional and polished
- **Casual**: Conversational and friendly
- **Academic**: Scholarly and precise
- **Business**: Clear and professional

### Advanced Features
- **Statistics Dashboard**: Track your most common errors and improvement over time
- **History Viewer**: Review recent corrections
- **File Operations**: Open and save text files
- **Clipboard Integration**: Check text from any application

## 🚀 Getting Started

### Installation

1. **Prerequisites**
   - .NET 8.0 or higher
   - Windows OS

2. **Build from Source**
   ```bash
   cd windows-app/GrammarAssistant
   dotnet restore
   dotnet build
   dotnet run
   ```

### AI Setup (Optional)

For AI-powered checking:

1. Get an OpenRouter API key from https://openrouter.ai/keys
2. Open `api-key.txt` in the application folder
3. Replace `YOUR_OPENROUTER_API_KEY_HERE` with your actual API key
4. Add credits to your OpenRouter account (https://openrouter.ai/credits)

**Cost**: GPT-4o-mini costs ~$0.15 per million tokens (~$0.0001 per paragraph check)

## 📖 How to Use

### Basic Workflow

1. **Type or paste** your text in the left panel
2. **Choose a check method**:
   - **Quick Check**: Fast local checking (free, instant)
   - **AI Check**: Comprehensive AI analysis (requires API key)
3. **Review suggestions** in the right panel
4. **Apply corrections** with one click or manually edit

### Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| `Ctrl+N` | New document |
| `Ctrl+O` | Open file |
| `Ctrl+S` | Save file |
| `Ctrl+Z` | Undo |
| `Ctrl+Y` | Redo |
| `Ctrl+Shift+C` | Copy corrected text |

### Menu Options

#### File Menu
- New, Open, Save text files
- Exit application

#### Edit Menu
- Undo/Redo corrections
- Copy corrected text

#### View Menu
- Toggle Dark Mode
- View Statistics
- View History

#### Settings Menu
- **Writing Style**: Formal, Casual, Academic, Business
- **Language**: English (US/UK), Spanish, French, German
- **Real-time Checking**: Enable/disable live checking

## 🎨 Features in Detail

### Real-time Checking
Enable from Settings → Real-time Checking. The app will automatically check your text 2 seconds after you stop typing.

### Readability Scores
- **Flesch Reading Ease**: 0-100 scale (higher = easier to read)
- **Grade Level**: US school grade level required to understand the text
- **Word/Sentence Count**: Live statistics

### Smart Caching
Previously checked text is cached for 24 hours, providing instant results and reducing API costs.

### Statistics
Track your writing improvement:
- Total checks performed
- Total errors found
- Average errors per check
- Top 10 most common errors

### History
- Unlimited undo/redo
- View recent corrections
- Restore previous versions

## 💡 Tips for Best Results

1. **Use AI Check for important writing**: Emails, reports, articles
2. **Use Quick Check for drafts**: Fast feedback while writing
3. **Enable Real-time Checking**: Catch errors as you type
4. **Choose the right style**: Match your writing context
5. **Review suggestions carefully**: AI is smart but not perfect

## 🔧 Technical Details

### Local Grammar Rules
- Subject-verb agreement
- Article errors (a/an)
- Common mistakes (should of → should have)
- Their/there/they're confusion
- Its/it's confusion
- Repeated words
- Capitalization issues
- Punctuation errors

### Spell Checking
- 140,000+ word English dictionary
- Suggestions for misspelled words
- Context-aware corrections

### AI Capabilities
- Deep grammar analysis
- Style improvements
- Clarity enhancements
- Context-aware suggestions
- Tone adjustments

## 📊 Performance

- **Quick Check**: < 100ms for typical paragraphs
- **AI Check**: 2-5 seconds (depends on text length)
- **Cached Results**: Instant
- **Memory Usage**: ~50-100 MB
- **Disk Space**: ~10 MB

## 🔒 Privacy

- All local checking happens on your computer
- AI checking sends text to OpenRouter/OpenAI servers
- No text is stored on external servers after processing
- Cache is stored locally only

## 🐛 Troubleshooting

### AI Check Not Working
1. Verify your API key in `api-key.txt`
2. Check you have credits in your OpenRouter account
3. Ensure internet connection is active

### Slow Performance
1. Disable real-time checking for large documents
2. Clear cache (restart application)
3. Check system resources

### Dictionary Not Loading
1. Ensure `Dictionaries/en_US.dic` and `en_US.aff` exist
2. Reinstall or rebuild the application

## 🚧 Future Enhancements

- [ ] Browser extension integration
- [ ] Mobile app version
- [ ] Custom dictionary additions
- [ ] Team collaboration features
- [ ] Writing templates
- [ ] Export to PDF/Word
- [ ] Voice dictation
- [ ] Multi-document tabs

## 📝 License

This project is provided as-is for educational and personal use.

## 🤝 Contributing

Suggestions and improvements are welcome! Feel free to:
- Report bugs
- Suggest features
- Submit pull requests

## 💬 Support

For issues or questions:
1. Check this README
2. Review error messages carefully
3. Verify API key and credits (for AI features)

---

**Made with ❤️ for better writing**
