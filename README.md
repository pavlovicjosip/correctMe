# correctMe

## Desktop writing assistant blueprint

If you want a Grammarly-like experience as you type in any desktop app, pair a resident background service with lightweight on-device checks and optional cloud rewrites. Below is an implementation outline:

### 1. Architecture overview
- **Local agent (daemon):** Runs at startup, watches text input events, and provides low-latency grammar/style checks via local models or rule engines.
- **Optional cloud enhancer:** When users opt in, send anonymized snippets for heavier rewrites (clarity/tone). Gate behind feature flags and rate limits.
- **Shared protocol:** Use a local HTTP/Unix socket or gRPC endpoint so multiple UI surfaces (tray, editor overlays, browser extension) can request suggestions.

### 2. OS-level integration
- **Windows:** Text Services Framework (TSF) or Input Method Editor (IME) style integration for per-field hooks; accessibility APIs for fallbacks. Use a tray icon for status/controls.
- **macOS:** Accessibility API + Input Method Kit or an NSEvent-based key listener. Offer a menu bar item for toggles and profiles.
- **Linux:** X11/Wayland text input protocols or IBus plugin for broad toolkit coverage; provide a tray indicator via AppIndicator/StatusNotifier.
- **Privacy modes:** Respect secure fields (passwords, credit cards) by excluding masked inputs and honoring app allow/deny lists.

### 3. Suggestion flow
1. Capture the active text span (sentence/paragraph) and language ID.
2. Run fast local checks (rules + small transformer) for spelling, agreement, and punctuation. Return inline underlines and minimal diffs.
3. When user pauses or presses a shortcut (e.g., `Ctrl/Cmd+;`), request deeper rewrites from the cloud service if enabled; otherwise stay on-device.
4. Present suggestions in a small popover near the caret with accept/reject buttons; ensure keyboard navigation for accessibility.

### 4. Models and performance
- **Local:** Quantized spelling/grammar model (distilled T5/ByT5) plus rule-based checks; cache per-document to keep latency under ~50–80 ms.
- **Cloud:** Larger instruction-tuned models for style/tone. Batch requests and use streaming responses.
- **Speculative decoding:** For cloud rewrites, use a small draft model to reduce latency while keeping quality via verification.

### 5. Personalization and safety
- Custom dictionaries and per-domain style guides (ignore brand terms, prefer company tone).
- Telemetry opt-in with on-device redaction; never store full text by default.
- Safety classifiers to prevent inappropriate rewrites and to detect sensitive fields before sending off-device.

### 6. Release and updates
- Auto-update mechanism (Squirrel/ClickOnce on Windows, Sparkle on macOS, AppImage/Flatpak on Linux) with signed packages.
- Feature flags to roll out new models and UI surfaces gradually.

This outline should help you scope a cross-platform desktop writing assistant that runs continuously while you type.

## Windows sample app
A minimal WPF project is included under `windows-app/GrammarAssistant` to give you a working Windows desktop client. It lets you paste or pull text from the clipboard, run a few local checks, and view suggestions.

### Features
- Text area for typing/pasting content.
- One-click grammar pass plus a clipboard check button.
- Basic rules: double spaces, repeated words, sentence capitalization, and trailing whitespace.
- Suggestion list with short descriptions and remediation hints.

### Building and running
1. On Windows with the .NET 8 SDK installed, open a Developer PowerShell.
2. Navigate to the project directory:
   ```pwsh
   cd windows-app/GrammarAssistant
   ```
3. Restore and build:
   ```pwsh
   dotnet build
   ```
4. Launch the app:
   ```pwsh
   dotnet run
   ```

### Extending the app
- Replace `Services/GrammarChecker.cs` with calls to your grammar or LLM endpoint.
- Add a tray icon and a keyboard hook (e.g., `Ctrl+Shift+G`) to capture the active selection across apps.
- Persist user settings (custom dictionary, style goals) in a JSON file under `%AppData%/CorrectMe`.
- Integrate telemetry behind an explicit opt-in toggle and redact sensitive fields before sending any text off-device.
