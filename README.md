<div align="center">

<img src="Assets/AppIcon.png" alt="Action Wheel icon" width="120" height="120">

# Action Wheel

**A radial "pie" menu for Windows — one middle click puts any shortcut or app right under your cursor.**

A WinUI 3 (.NET 10) desktop app that pops up a radial menu at the cursor whenever you press the middle mouse button, anywhere in Windows.

[![License: MIT](https://img.shields.io/github/license/cmm-cmm/Action-Wheel?color=blue)](LICENSE.txt)
[![Latest tag](https://img.shields.io/github/v/tag/cmm-cmm/Action-Wheel?label=version&color=success)](https://github.com/cmm-cmm/Action-Wheel/tags)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%20%2F%2011-0078D6?logo=windows&logoColor=white)](#requirements)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)](#requirements)
[![Windows App SDK](https://img.shields.io/badge/Windows%20App%20SDK-1.8-0078D6?logo=windowsxp&logoColor=white)](#requirements)
[![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen.svg)](CONTRIBUTING.md)

</div>

---

## Table of contents

- [Features](#features)
- [Requirements](#requirements)
- [Build & run](#build--run)
- [Using it](#using-it)
- [Configuring the buttons](#configuring-the-buttons)
- [How it works](#how-it-works)
- [Troubleshooting](#troubleshooting)
- [Contributing](#contributing)
- [License](#license)

## Features

### 🖱️ Interaction

- **Global trigger** — a low-level `WH_MOUSE_LL` hook watches for the middle mouse button system-wide. The click is *swallowed*, so the app underneath doesn't also get it (no stray browser tabs or autoscroll).
- **Opens at the cursor** — the menu centres itself on the pointer and is clamped to the current monitor's work area.
- **Pick by gesture** — keep the middle button held, flick towards a button and let go. Faster than aiming, and it is what a radial menu is for. Release without moving and the menu simply stays open to be clicked.
- **Dismiss any way you like** — click a button, click anywhere off the buttons, click elsewhere on screen, or press <kbd>Esc</kbd>.

### 🎨 Customization

- **Configurable buttons** — 8 outer buttons plus a centre button. Each can send a keyboard shortcut or launch a program/file/URL. Edit them in the built-in settings window, or by hand in `actions.json`.
- **Truly transparent** — per-pixel alpha, so only the buttons are drawn; the desktop shows through everywhere else.
- **Buttons carry a drop shadow** — they stay readable over a white window underneath, which matters because the overlay is genuinely transparent.
- **Picks up edits to `actions.json` on its own** — the file is watched in both locations it can live in.

### 🔒 Reliability & privacy

- **One instance only** — starting the app again asks the copy already running to open its settings instead of installing a second global hook.
- **Runs in the notification area** — closing the main window hides it; the app keeps listening in the background.
- **Says when it isn't working** — the main window reports whether the hooks installed and whether `actions.json` was usable.
- **Private by design** — no usage events, clicks, gestures, launched actions, profile changes or errors are written to a log file.
- **Starts with Windows** — optional, from the main window's toggle or the tray menu. Uses the per-user Run key, so it needs no elevation and shows up in Windows' own Startup Apps settings.
- **Stays out of the way** — `WS_EX_TOOLWINDOW` + `WS_EX_NOACTIVATE` keep the overlay out of Alt+Tab and the taskbar, and it never steals focus, so shortcuts land in the app you were actually using.

## Requirements

- Windows 10 version 1809 (build 17763) or later
- .NET 10.0
- Windows App SDK 1.8
- Visual Studio 2022 with the "Windows application development" workload (for building)

## Build & run

`Build.ps1` is the one entry point — the same commands developers and CI run.

```powershell
.\Build.ps1                       # Debug build (the default)
.\Build.ps1 -Task Release
.\Build.ps1 -Task Publish         # self-contained build (exe + dependencies) in bin\Publish\win-x64
.\Build.ps1 -Task Installer       # Publish, then the Inno Setup installer
.\Build.ps1 -Task Clean
.\Build.ps1 -Task Publish -Platform ARM64
```

Nothing in it prompts, so it works unattended. It refuses to build while a copy of Action Wheel is still running, because otherwise the exe is locked and MSBuild fails several hundred lines later with a copy error that never mentions the app being open.

Plain `dotnet build` also works: the project defaults `Platform` to `x64` and derives the matching runtime identifier, so no `-r win-x64` is needed. Pass `-p:Platform=ARM64` or `-r win-x86` to override.

In Visual Studio, press F5 — there is one launch profile, **"Action Wheel (Unpackaged)"**.

Self-contained publishes are large (roughly 250–300 MB across the exe and its dependencies). That is normal for a self-contained WinUI 3 / .NET 10 app. They are not bundled into a single exe: WinUI 3's native XAML engine does not survive being packed into .NET's single-file host and self-extracted at first run - it crashes on launch with exception code `0xc000027b` in `Microsoft.UI.Xaml.dll`. Don't add `-p:PublishSingleFile=true` back without actually launching the result, not just building it.

## Using it

1. Run the app. The main window reports whether the global hooks are actually installed, and whether `actions.json` was usable — the menu itself works whether or not the window is open.
2. Press the **middle mouse button** anywhere.
3. Click a button to run its action, or dismiss with <kbd>Esc</kbd> / a click elsewhere.
4. Right-click the tray icon to open the main window, open the settings window, open `actions.json` in an editor, reload the config, turn **Start with Windows** on or off, or quit.

## Configuring the buttons

### Settings window

**Open button settings** — from the main window, or **Settings…** in the tray icon's right-click menu. It shows all nine slots (centre plus the eight ring positions), each with:

- **Label**, **action type** and **value**
- a **record button** (keyboard icon) that captures a key press instead of making you type the shortcut — Ctrl, Alt and Shift combinations are captured; the Windows key never reaches the app because the shell takes it first, so `Win+…` has to be typed by hand. <kbd>Esc</kbd> cancels recording.
- a **file browser** for launch targets, and an **arguments** box for the parameters to pass them
- an **icon**. The preview button on the left opens a picker offering all three sources: any glyph in the bundled icon font (searchable by name or code, and read from the font itself so the list matches what the app can draw), your own **`.svg`, `.png` or `.ico` file**, and **the icon of the program the button launches**. Browsing for a launch target takes that program's icon automatically.
- **icon colour**, **button colour** and an icon **scale** from 0.25× to 3×; the preview button on the left shows exactly what the ring will draw
- a **tint** switch for file icons: on, an `.svg` is recoloured to the icon colour; off, it keeps the colours it was drawn with. A `.png` or `.ico` always keeps its own colours.
- a **reset** button that restores just that one button to its default
- a **live status marker** next to the value: green when a launch target was found (on disk, on PATH, or a well-formed URL), amber when it was not or when another button uses the same shortcut, red when the shortcut contains a key that cannot be sent

**Drag a row** to move that button around the ring — the first row is the centre button and the rest go clockwise from the top. A **live preview** on the right shows the ring as your unsaved edits will leave it, and **undo/redo** step through those edits (typing is coalesced, so undo does not go one keystroke at a time).

**Save** writes `actions.json` and tells the running service to reload, so the next menu that opens uses the new configuration; a menu that is already on screen keeps the one it was opened with.

Saving is refused — with the reason shown in the bar above the buttons — if a shortcut contains an unrecognised key name, an action has no value, or an icon code is not a usable code point. Those are exactly the mistakes that would otherwise produce a button that silently does nothing.

Duplicate shortcuts and launch targets that were not found are reported *after* saving rather than instead of it: both are legal. A program may be installed only on another machine, and two buttons may deliberately send the same keys.

### The file

Buttons are read from `actions.json`, searched in this order:

1. next to the executable (portable installs)
2. `%LOCALAPPDATA%\ActionWheel\actions.json`

If neither exists, the defaults are written to the `%LOCALAPPDATA%` copy on first run — the install directory is often read-only. The settings window saves to whichever of the two is actually in effect, so a portable copy is never silently shadowed.

Both locations are watched, so an edit made in a text editor takes effect on its own — the tray menu's **Reload** is still there but no longer necessary.

Saving never writes the file in place: the JSON goes to a temp file, that file is flushed to disk, the previous contents are copied to `actions.json.bak`, and only then does the temp file replace the original. An interrupted save therefore leaves either the old file or the new one, never half of one.

A file that cannot be read is reported directly in the main window. The built-in defaults take over so the menu keeps working, **your file is left exactly as it is**, and the main window says which file failed and why — with a **Restore the backup** button when a `.bak` is available. (Restoring keeps the broken file as `actions.json.invalid`; it is still the only copy of whatever you were in the middle of writing.)

```json
[
  { "tag": 0, "label": "Close menu", "type": "none", "value": "", "glyph": "F00D" },
  { "tag": 1, "label": "Copy", "type": "keys", "value": "Ctrl+C", "glyph": "F0C5" },
  {
    "tag": 2, "label": "Open notes", "type": "launch",
    "value": "notepad.exe", "arguments": "\"C:\\Notes\\today.txt\"",
    "iconPath": "C:\\icons\\notes.svg", "foreground": "#FFFFFFFF", "background": "#FF1B6FD4"
  }
]
```

| Field | Meaning |
|---|---|
| `tag` | `0` = centre button, `1`–`8` = outer buttons clockwise from the top |
| `type` | `keys` (send a shortcut), `launch` (run a program / open a path or URL), `none` (just close) |
| `value` | the shortcut (e.g. `Ctrl+Shift+S`, `Win+D`) or the target to launch |
| `arguments` | command-line arguments for a `launch` target; quote paths that contain spaces |
| `label` | tooltip text |
| `glyph` | Icon code point in hex, from the bundled [CaskaydiaCove Nerd Font](https://www.nerdfonts.com/). Five digits are allowed — two thirds of the font's icons sit above U+FFFF |
| `iconPath` | path to an `.svg`, `.png` or `.ico` file to draw instead of the glyph |
| `iconTint` | `true` (default) recolours an `.svg` to `foreground`; `false` keeps the colours in the file |
| `foreground` | icon colour, `#RRGGBB` or `#AARRGGBB` |
| `background` | button colour, same format |

Every field after `value` is optional; leaving one out uses the built-in default — a config written before `iconTint` existed therefore gets tinting, which is what a single-colour pictogram wants. Only a vector icon can be recoloured: a `.png` or `.ico` is drawn as-is whatever `iconTint` says, and `background` applies either way.

Icons copied from an application are written to `%LOCALAPPDATA%\ActionWheel\Icons` as PNGs, so a button keeps its picture after that application is updated, moved or uninstalled. On a later startup, generated PNGs no longer referenced by `actions.json` or any readable saved profile are removed automatically; user-selected files elsewhere are never touched.

**The icon font ships with the app** (`Assets/Fonts/`), it is not the system's. CaskaydiaCove Nerd Font Propo carries about ten thousand icons against Segoe Fluent Icons' two thousand, including the application and language logos a launcher wants. The two fonts overlap in `E700`–`E8EF` and disagree about every one of the 887 shared code points — `E711` is a close button in Segoe and the Apple logo here — so a `glyph` value only means anything against the font it was chosen from, and that font therefore travels with the app rather than being looked up on the machine. Licences and attribution are in `Assets/Fonts/Licenses/`. Every `FontIcon` in the application explicitly uses this bundled family.

Use the **Propo** cut, not the plain one. In the plain cut an icon keeps the monospace advance of 0.586 em while its ink runs to a full em, overflowing the cell through a negative right side bearing: measured, the ink's centre lands at 0.756 of the advance rather than 0.5, and every icon sits right of where it belongs. Propo widens the advance to match the ink. The Mono cut also centres, but by shrinking every icon into the 0.586 cell.

**There is no compatibility path from the old icon font.** A `glyph` the shipped font has no icon for draws nothing — no substitute, and no notdef box, which would read as a rendering fault rather than a setting to change. The settings window reports those rows as a warning after a save instead of blocking it, since a button with no picture still works.

Recognised key names: `Ctrl`, `Alt`, `Shift`, `Win`, `A`–`Z`, `0`–`9`, `F1`–`F24`, `Esc`, `Tab`, `Enter`, `Space`, `Backspace`, `Delete`, `Insert`, `Home`, `End`, `PageUp`, `PageDown`, arrow keys, `PrintScreen`, and the media keys (`PlayPause`, `MediaNext`, `MediaPrev`, `MediaStop`, `VolumeUp`, `VolumeDown`, `Mute`).

## How it works

The code is in two assemblies. **`ActionWheel.Core`** is the business logic and references nothing but the base class library — no WinUI, no `user32`. **`Action Wheel`** is the app: windows, view models, hooks, and the Win32 work the overlay needs. The split is a compiler-enforced version of the layering, so logic cannot quietly grow a dependency on a XAML type.

**`ActionWheel.Core`** — plain `net10.0`:

| File | Role |
|---|---|
| `ActionItem.cs` | The immutable action model |
| `ActionConfig.cs` | Reads, validates and writes `actions.json`; the atomic save, the backup, and `ConfigLoadResult` |
| `ConfigWatcher.cs` | `FileSystemWatcher` over both config locations, debounced |
| `ShortcutKeys.cs` | Turns "Ctrl+Shift+S" into virtual-key codes; reports the key it did not recognise |
| `RingGeometry.cs` | Menu size for a DPI, position clamped to a monitor's work area, and the gesture direction → tag |
| `ColorValue.cs` / `IconFile.cs` | Colour parsing and lighten/darken; which icon files are usable |
| `SvgTint.cs` | Rewrites an SVG's fills and strokes to one colour — the only way to recolour one, since the renderer draws it exactly as authored |
| `FontGlyphs.cs` | Reads a font file's character map, because GDI only sees installed fonts and cannot report anything above U+FFFF |
| `StartupCommand.cs` | The command line stored under the Run key |
| `AppDataPaths.cs` | The shared `%LOCALAPPDATA%\ActionWheel` configuration location; it stores settings, not activity history |
| `DiagnosticsReport.cs` | The text behind "Copy diagnostics" |

**`Action Wheel`** — the WinUI 3 app:

| File | Role |
|---|---|
| `Services/MouseHook.cs` | `WH_MOUSE_LL` hook: raises `MiddleClick`, swallows the middle button, and reports other clicks so the menu can be dismissed |
| `Services/KeyboardHook.cs` | `WH_KEYBOARD_LL` hook, only for <kbd>Esc</kbd> — the overlay never has keyboard focus |
| `Services/LauncherService.cs` | Owns the hooks, the watcher and the menu's lifetime; guarantees one menu at a time; reports hook status |
| `Services/ActionDispatcher.cs` | Synthesises shortcuts via `SendInput`, or starts a process; raises `ActionFailed` when either does not work |
| `Services/TrayIcon.cs` | `Shell_NotifyIcon` tray icon with a right-click menu, on a message-only window |
| `Services/AppIcon.cs` | Reads `icon.ico` back out of the exe's own resources for the window title bars and the tray |
| `Services/AppIconExtractor.cs` | Copies the icon Windows shows for a program into a 256px PNG, via the system image list |
| `Services/IconFont.cs` | The bundled icon font: the family string for a `FontIcon`, and which glyphs it has |
| `Services/StartupManager.cs` | The per-user Run key entry behind "Start with Windows" |
| `Services/ShellCommands.cs` | Opening a folder in Explorer and putting text on the clipboard |
| `ViewModels/MainViewModel.cs` | Everything the main window shows and does |
| `Overlay/ButtonShadow.cs` | The composition drop shadow behind each ring button |
| `Overlay/RadialMenu.xaml(.cs)` | The 400×400 overlay window: transparency, positioning, layout, hit-testing |
| `Settings/SettingsWindow.xaml(.cs)` | The settings window: edits the nine buttons, validates and saves |
| `Settings/ActionEditModel.cs` | Mutable, observable counterpart of the immutable `ActionItem`, used only while that window is open |
| `Settings/GlyphChoice.cs` | The icon picker's entries: a named, curated set plus every glyph the font reports |
| `MainWindow.xaml(.cs)` | Status window; closing it hides to the tray |

A few implementation details are easy to break and worth knowing about:

- **Transparency** needs *both* a custom `SystemBackdrop` holding a transparent composition brush *and* `DwmEnableBlurBehindWindow`. Drop either and the menu renders as a solid black square. The classic `WS_EX_LAYERED` colour-key trick does not work with WinUI 3 content.
- **Removing the window outline** is done entirely by `RemoveWindowFrame`, which strips `WS_DLGFRAME` and friends directly. It only works if the presenter was attached to the `AppWindow` first, otherwise the style changes are silently reverted.
- **`InitializeWindow` deliberately configures nothing on the presenter, and does not touch `AppWindow.TitleBar`.** Both blocks used to be there and between them cost 46 ms of the 52 ms it took to build the overlay — on every single click — while achieving nothing measurable: the TitleBar properties style a title bar this window does not have, and every presenter flag is superseded a few lines later by `RemoveWindowFrame` and `SetWindowPos(HWND_TOPMOST)`. Removing them left `GWL_EXSTYLE` byte-identical and `GWL_STYLE` different only in two bits that cannot draw without `WS_CAPTION`. Don't put them back.
- **The opening animation's timings are tuned.** The 0.012 s stagger and 0.09 s per button put the last one on screen 186 ms after the click; they were 0.03 s / 0.15 s, which meant 400 ms and was most of how slow the menu felt. Click-to-first-pixel is only ~15 ms, so the animation, not the code, is what the user is waiting for.
- **Code reachable from a hook callback must never perform disk I/O.** The mouse hook reads the cursor position from `lParam` rather than calling `GetCursorPos`, which is both cheaper and more accurate during fast movement.
- **Button styles use full `ControlTemplate`s** with every visual state written out. Setting `Background`/`Foreground` on a `Button` only styles the initial rest state, and the default template has nothing to restore it with, so hover colours would stick permanently once the pointer left.
- **The settings window's row templates use classic `{Binding}`**, not `{x:Bind}`, which means they resolve properties by reflection. That is fine while `PublishTrimmed` is `False`; turning trimming on later means converting those bindings to `x:Bind` first.

## Troubleshooting

The main window carries **Copy diagnostics**. It copies environment, configuration and hook status only when the user explicitly asks; it contains no activity history.

**Menu doesn't appear** — the main window says so directly, in red. It is often security software blocking `SetWindowsHookEx`.

Both hooks are installed together or not at all. If only one goes in, the other is rolled back deliberately: a mouse hook without a keyboard hook gives a menu that <kbd>Esc</kbd> silently will not close, and a keyboard hook without a mouse hook swallows <kbd>Esc</kbd> system-wide while nothing ever opens.

**An action does nothing** — it now says so in the main window. Unknown key names are reported with the offending key, a program that cannot be started is reported with the error, and a `SendInput` that Windows refused is reported as such (that one usually means the foreground window is running elevated and this app is not).

**The buttons went back to the defaults** — `actions.json` was rejected. The main window shows which file and why, and offers to restore the backup. Your file is never overwritten in that state.

**No tray icon** — the app falls back to exiting when the main window closes, so it can't become unquittable.

## Contributing

Bug reports, feature requests and pull requests are welcome — see [CONTRIBUTING.md](CONTRIBUTING.md) for the build/test workflow and coding conventions, and [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md) for the ground rules. Found a security issue? Please report it privately per [SECURITY.md](SECURITY.md) rather than opening a public issue.

## License

[MIT](LICENSE.txt) © 2026 Minh Pham
