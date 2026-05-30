# Wincy 🪟

**Lightweight clipboard manager for Windows** — inspired by [Maccy](https://github.com/p0deje/Maccy) for macOS.

Wincy keeps a history of everything you copy and lets you quickly navigate, search, and reuse previous clipboard contents. It lives in your system tray, staying out of your way until you need it.

## Features

- 🔍 **Instant search** — type to filter clipboard history
- ⌨️ **Keyboard-first** — summon with `Ctrl+Shift+V`, navigate with arrow keys
- 📌 **Pin items** — keep frequently used snippets on top
- 🗑️ **Clear history** — delete individual items or clear all
- 📋 **Copy or Paste** — `Enter` to copy, `Alt+Enter` to paste directly
- 🔒 **Privacy-first** — all data stored locally, no network access
- 💾 **Persistent storage** — SQLite database, history survives reboots
- 🪶 **Lightweight** — runs in system tray, minimal resource usage

## Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| `Ctrl+Shift+V` | Show/hide Wincy |
| `Enter` | Copy selected item to clipboard |
| `Alt+Enter` | Paste selected item to foreground app |
| `Alt+Delete` | Delete selected item |
| `Alt+P` | Pin/unpin selected item |
| `Ctrl+1`~`Ctrl+0` | Quick-select items 1-10 |
| `Esc` | Hide Wincy |

## System Requirements

- Windows 10 or later
- .NET 9.0 Runtime

## Build from Source

```bash
# Clone the repository
git clone https://github.com/1409114679/Wincy.git
cd Wincy

# Build
dotnet build

# Run
dotnet run
```

## Tech Stack

- **C#** / **.NET 9.0**
- **WPF** for the search popup UI
- **Windows Forms** for system tray integration
- **SQLite** via `Microsoft.Data.Sqlite` for local storage
- **Win32 API** for clipboard monitoring and global hotkeys

## License

MIT License - see [LICENSE](LICENSE) file for details.

## Acknowledgments

Inspired by [Maccy](https://github.com/p0deje/Maccy) — the excellent clipboard manager for macOS.