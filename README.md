# 🎵 Audio Overlay

A lightweight Windows desktop utility for quick access to your audio files with a modern, always-on-top overlay interface.

## ✨ Features

- **Always-on-Top Overlay** - Discord-style dark theme that stays above all windows
- **Global Hotkey** - Toggle visibility with customizable keyboard shortcut (default: `Ctrl+Shift+Q`)
- **Real-Time Search** - Filter audio files instantly as you type
- **One-Click Copy** - Click any file to copy it to clipboard, then paste anywhere
- **In-App Audio Preview** - Play audio files directly in the overlay
- **Auto-Start** - Option to launch automatically with Windows
- **Wide Format Support** - MP3, WAV, OGG, FLAC, M4A, AAC, WMA, AIFF, ALAC, OPUS
- **Persistent Settings** - Remembers your folder and preferences

## 🚀 Download

**[Download Latest Release](https://github.com/malleu5/audio-overlay/releases/latest)**

Download `AudioOverlayApp.exe` - No installation required!

## 📖 How to Use

1. **Launch** the app
2. **Click "Select Folder"** and choose a folder containing audio files
3. **Press your hotkey** (`Ctrl+Shift+Q`) to show/hide the overlay
4. **Click a file name** to copy it to clipboard
5. **Click the ▶ button** to preview the audio
6. **Paste** (`Ctrl+V`) in Discord, Slack, or any app to upload

## ⚙️ Settings

Click the **⚙ Settings** icon to:
- Enable/disable auto-start with Windows
- Customize your global hotkey

## 🎹 Keyboard Shortcuts

- **Configurable Hotkey** (default `Ctrl+Shift+Q`) - Show/hide overlay
- **Esc** - Cancel hotkey configuration

## 🎨 Header Buttons

- **⚙ Settings** - Open settings panel
- **− Hide** - Hide overlay (use hotkey to bring back)
- **✕ Quit** - Exit the application completely

## 🎧 Supported Audio Formats

**Play in-app:**
- MP3, WAV, OGG, FLAC, M4A, AAC, WMA, AIFF, ALAC

**Opens externally:**
- OPUS (requires VLC or compatible player)

## 💻 System Requirements

- Windows 10/11 (64-bit)
- .NET 8 Runtime (included in self-contained build)

## 🛠️ Building from Source
```bash
# Clone the repository
git clone https://github.com/malleu5/audio-overlay.git
cd audio-overlay

# Build
dotnet build

# Run
dotnet run

# Publish standalone executable
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true
```

## 📦 Dependencies

- [NAudio](https://github.com/naudio/NAudio) - Audio playback
- [NAudio.Vorbis](https://github.com/naudio/Vorbis) - OGG Vorbis support

## 🔒 Privacy

- All data stays local on your computer
- No internet connection required
- No telemetry or tracking
- Settings stored in `%AppData%\AudioOverlay\`

## 📝 License

MIT License - See [LICENSE](LICENSE) file for details

## 🤝 Contributing

Contributions are welcome! Feel free to open issues or submit pull requests.

## 💡 Use Cases

- **Content Creators** - Quick access to sound effects and music
- **Discord/Gaming** - Instant soundboard for voice chats
- **Streamers** - Easy audio file management during streams
- **Developers** - Fast audio asset workflow

## 🙏 Acknowledgments

Built with love using .NET 8 and WPF

---

**Made with 💜 by malleu5**
```