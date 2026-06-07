# Wincy 🪟

**轻量级 Windows 剪贴板管理器** — 灵感来自 macOS 平台的 [Maccy](https://github.com/p0deje/Maccy)。

Wincy 会记录你复制过的所有内容，让你快速搜索、浏览并复用剪贴板历史。它安静地驻留在系统托盘中，随用随唤。

## 功能特性

- 🔍 **即时搜索** — 输入关键词即可筛选剪贴板历史
- ⌨️ **键盘优先** — 通过可配置的快捷键呼出，方向键浏览
- 📌 **置顶条目** — 将常用片段固定在列表顶部
- 🗑️ **清理历史** — 可单独删除条目或一键清空
- 📋 **复制或粘贴** — `Enter` 复制，`Alt + Enter` 直接粘贴到前台应用
- 🔒 **隐私优先** — 所有数据仅存储在本地，无网络连接
- 💾 **持久化存储** — 基于 SQLite 数据库，重启后历史不丢失
- 🪶 **轻量低耗** — 运行在系统托盘中，资源占用极低

## 下载（绿色免安装版）

下载最新的**自包含绿色版** — 无需安装 .NET 运行环境，下载后直接运行：

👉 **[下载 Wincy.exe](https://github.com/1409114679/Wincy/releases/latest/download/Wincy.exe)**

- ✅ 无需安装
- ✅ 无需额外安装 .NET 运行时（已全部内置）
- ✅ 支持 Windows 10 / 11（64 位）

下载后，直接双击 `Wincy.exe` 即可启动，程序图标会出现在系统托盘中。

> **Windows SmartScreen 提醒：** 首次运行时 Windows 可能会弹出安全警告，点击「更多信息」→「仍要运行」即可。

## 快捷键

| 快捷键 | 功能 |
|----------|--------|
| 可配置的快捷键 | 显示/隐藏 Wincy |
| `Enter` | 复制选中条目到剪贴板 |
| `Alt + Enter` | 将选中条目粘贴到前台应用 |
| `Alt + Delete` | 删除选中条目 |
| `Alt + P` | 置顶/取消置顶选中条目 |
| `Esc` | 隐藏 Wincy |

## 环境要求

- Windows 10 或更高版本（64 位）
- .NET 8.0 运行时 *(仅从源码构建时需要；绿色版已内置所有依赖)*

## 从源码构建

```bash
# 克隆仓库
git clone https://github.com/1409114679/Wincy.git
cd Wincy

# 构建（需要 .NET 8.0 SDK）
dotnet build

# 运行
dotnet run

# 发布绿色单文件版本
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish
```

## 技术栈

- **C#** / **.NET 8.0**
- **WPF** — 搜索弹出窗口界面
- **Windows Forms** — 系统托盘集成
- **SQLite**（通过 `System.Data.SQLite`）— 本地数据存储
- **Win32 API** — 剪贴板监控与全局快捷键

## 开源协议

MIT License — 详见 [LICENSE](LICENSE) 文件。

## 致谢

灵感来自 [Maccy](https://github.com/p0deje/Maccy) — macOS 平台优秀的剪贴板管理器。

---

*English version: [README.md](README.md)*