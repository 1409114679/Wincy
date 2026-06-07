# Wincy 🪟

**Lightweight clipboard manager for Windows** — inspired by [Maccy](https://github.com/p0deje/Maccy) for macOS.

Wincy keeps a history of everything you copy and lets you quickly navigate, search, and reuse previous clipboard contents. It lives in your system tray, staying out of your way until you need it.

## Features

- 🔍 **Instant search** — type to filter clipboard history
- ⌨️ **Keyboard-first** — summon with configurable hotkey, navigate with arrow keys
- 📌 **Pin items** — keep frequently used snippets on top
- 🗑️ **Clear history** — delete individual items or clear all
- 📋 **Copy or Paste** — `Enter` to copy, `Alt+Enter` to paste directly
- 🔒 **Privacy-first** — all data stored locally, no network access
- 💾 **Persistent storage** — SQLite database, history survives reboots
- 🪶 **Lightweight** — runs in system tray, minimal resource usage

## Download (Portable / 绿色免安装)

Download the latest **self-contained portable version** — no .NET runtime required, just download and run:

👉 **[Download Wincy.exe](https://github.com/1409114679/Wincy/releases/latest/download/Wincy.exe)**

- ✅ No installation needed
- ✅ No .NET runtime required (everything bundled)
- ✅ Works on Windows 10 / 11 (64-bit)

After downloading, just double-click `Wincy.exe` to start. Wincy will appear in your system tray.

> **Windows SmartScreen note:** On first run, Windows may show a warning. Click "More info" → "Run anyway" to continue.

## Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| Configurable hotkey | Show/hide Wincy |
| `Enter` | Copy selected item to clipboard |
| `Alt+Enter` | Paste selected item to foreground app |
| `Alt+Delete` | Delete selected item |
| `Alt+P` | Pin/unpin selected item |
| `Esc` | Hide Wincy |

## System Requirements

- Windows 10 or later (64-bit)
- .NET 8.0 Runtime *(only for building from source; the portable download includes everything)*

## Build from Source

```bash
# Clone the repository
git clone https://github.com/1409114679/Wincy.git
cd Wincy

# Build (requires .NET 8.0 SDK)
dotnet build

# Run
dotnet run

# Publish portable single-file executable
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish
```

## Tech Stack

- **C#** / **.NET 8.0**
- **WPF** for the search popup UI
- **Windows Forms** for system tray integration
- **SQLite** via `System.Data.SQLite` for local storage
- **Win32 API** for clipboard monitoring and global hotkeys

## License

MIT License - see [LICENSE](LICENSE) file for details.

## Acknowledgments

Inspired by [Maccy](https://github.com/p0deje/Maccy) — the excellent clipboard manager for macOS.