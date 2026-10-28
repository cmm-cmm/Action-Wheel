# AI assistants on the ring

**Status:** Phase 1 is implemented. Phases 2 and 3 are proposed and not implemented yet.

## Context

Ring buttons should reach the AI tools a developer already has: Claude Code, Codex, OpenCode, Antigravity and similar. Two requirements come from the user:

- **Only tools that are actually installed are offered.** A button for an absent tool must not be offered.
- **There is a Settings section for the AI integration.**

This document records what was found on a real machine, the design, and the plan in three phases.

## Research: what is on a real machine (2026-10-28)

Probed on the development machine. Only executables, the registry and `--help` output were read. No prompt was sent, and no tool's configuration or credential file was opened.

| Tool | Found by | Unattended one-shot mode |
|---|---|---|
| Claude Code 2.1.312 | PATH: `%USERPROFILE%\.local\bin\claude.exe` (native installer) | `claude -p`; `--tools ""` disables every tool; `--output-format text\|json` |
| Codex CLI 0.165.0 | PATH: `%LOCALAPPDATA%\Programs\OpenAI\Codex\bin\codex.exe` | `codex exec -s read-only --ephemeral --skip-git-repo-check -o <file>`; prompt from stdin |
| OpenCode 1.19.4 | PATH: npm shim `%APPDATA%\npm\opencode.cmd` | `opencode run`, **but the built-in agents `build` and `plan` both start with `"permission": "*", "action": "allow"`** |
| Antigravity | **Uninstalled.** `HKCR\antigravity\shell\open\command` still points at `%LOCALAPPDATA%\Programs\antigravity\Antigravity.exe`, which no longer exists | n/a (IDE) |
| GitHub Copilot CLI | PATH: shim shipped with VS Code (`...\Microsoft VS Code\bin\copilot-shim\copilot.exe`) | not evaluated |
| LM Studio | Uninstall entry, `lms` on PATH | local models only |

Findings that shape the design:

1. **Tools leave traces in different places.** Depending on the tool, that is PATH, an npm `.cmd` shim, App Paths, a URL protocol handler, a known install folder or an MSIX package. Detection has to try several sources per tool.
2. **A registry entry is not proof of installation.** Antigravity's protocol handler outlived the app. Every source has to resolve to an executable that exists **right now**, or the tool counts as missing. A tool with traces but no executable is shown as "registered, but its program is missing", never as installed.
3. **Configuration folders are not a signal.** `%USERPROFILE%\.gemini` exists on this machine, and it contains `oauth_creds.json`. Action Wheel never looks inside these folders and does not use them to decide anything.
4. **One-shot safety differs per tool.**
   - In print mode, Claude Code skips the workspace trust dialog, so it must run with tools disabled and in an empty folder.
   - Codex has an explicit `read-only` sandbox.
   - OpenCode has no verified read-only agent, so it is not used unattended.

## Design

### Function ids, not a new action type

An AI action is a **Function** action with a parameterised id, the same way profile switching works (`action-wheel.profile:<name>`):

```
action-wheel.ai:<tool>/<task>        e.g. action-wheel.ai:claude/open
```

- No new `ActionItem` field, so `actions.json` and profile XML need no schema change, and RoundTripCheck has nothing new to mirror.
- **The set of valid ids is static. It is the catalog in `ActionWheel.Core/AiTools.cs`, never the detection result.** If validation depended on detection, uninstalling a tool would reject the whole `actions.json`. That is the same failure class the missing-icon fix in 2.9.0 removed. An id for a tool that is absent or disabled stays valid. It fails when clicked, with a message, and Settings warns about it after saving.

### Phase 1: detect, configure, open (implemented)

New and changed files:

| File | Role |
|---|---|
| `ActionWheel.Core/AiTools.cs` | The static catalog. For each tool: id, display name, kind (`Cli` or `App`), whether it opens a folder, command names, known install paths, protocol names. Also the ids: `Choice`, `TryParse`, `IsKnown`. No I/O. |
| `ActionWheel.Core/WindowsFunctions.cs` | `IsKnown` accepts AI ids. The function picker gets an "AI" category with its own glyph and description. |
| `Services/AiToolDetector.cs` | `Detect(tool)` and `DetectAll()`. Sources, in order: command names through `CommandResolver` (PATH, then App Paths), the known install paths, then each protocol's `shell\open\command`. Every candidate must be an existing file. The result is `Installed` (with path and source), `Missing`, or `Stale` (traces whose target is gone). Runs off the UI thread, never on a hook path. |
| `Services/AiAssistantSettings.cs` | Which tools the user switched off, in `ai-assistants.json` (`{"disabled":["opencode"]}`). Machine-level like Start hidden, so not part of a backup. Every tool starts enabled. |
| `Services/AiLauncher.cs` | Runs `open`. It re-detects the one tool on every click, so an install or uninstall since startup is picked up. Then it finds a working folder and starts the tool. |
| `Services/ExplorerFolder.cs` | The folder of the File Explorer window that was in front when the ring opened. Uses `Shell.Application` on a short-lived STA thread with a timeout. The desktop maps to the Desktop folder; anything else gives no folder. |
| `Services/ActionDispatcher.cs` | Routes `action-wheel.ai:` ids to `AiLauncher`, so they work for hold actions and group children too. |
| `ViewModels/SettingsViewModel.Ai.cs`, `Settings/AiToolRow.cs` | The **AI assistants** section: one row per catalog tool with status, path and how it was found, an enable switch, and **Scan again**. Installed and enabled tools appear in the function picker as "AI · Open <tool>". An existing button for an unavailable tool keeps its value and shows "(not available)" in the picker. Save warns about such buttons. |
| `Settings/SettingsWindow.xaml(.cs)` | The fourth navigation section and its template. `Verify-Bindings.ps1` maps the new template to `AiToolRow`. |

How `open` behaves:

- **CLI tools** start in a new Windows Terminal tab, `wt.exe -d <folder> <tool>`. Without Windows Terminal they start in a console window with that working folder. An npm `.cmd` shim runs through `cmd.exe /k`. The folder is the active Explorer window's folder, otherwise the user's profile folder.
- **IDE apps** (Antigravity, Cursor, Windsurf) get the Explorer folder as their argument when there is one. Otherwise they just start.
- **Desktop apps** (Claude desktop, LM Studio) just start.

Phase 1 sends no text anywhere. Everything the tool receives is typed by the user into its own window.

### Phase 2: tasks on the selected text (proposed)

Prompt templates such as "Explain", "Fix grammar", "Translate to …" and "Summarize" use a `{selection}` placeholder. They live in `ai-tasks.json` and are edited in the AI section. The id is `action-wheel.ai:<tool>/task:<templateId>`.

1. Copy the selection: send Ctrl+C to the target window and wait for `GetClipboardSequenceNumber` to change, with a timeout.
2. Pass the prompt **on stdin**, never on the command line. The command line is readable by other processes, is length-limited, and is open to quoting injection.
   - Claude: `claude -p --tools "" --output-format text`.
   - Codex: `codex exec -s read-only --ephemeral --skip-git-repo-check -o <tmp> -`.
   - Both run in a fresh empty temp folder.
3. Show the result in a small window near the cursor, with Copy, Replace selection and Continue in terminal. It shows a busy state and a Cancel button, and enforces a timeout.

Fixed safety rules:

- Never pass `bypassPermissions`, `danger-full-access` or any `--dangerously-*` flag.
- OpenCode is excluded until a read-only mode can be verified.
- Each template asks for confirmation before text leaves the machine. This is on by default and can be switched off per template.

Open decisions for the user: where results go (window, clipboard or replace), and the default for that confirmation.

### Phase 3: more tools (proposed)

- **LM Studio `lms`.** Models run locally, so nothing leaves the machine. This is the natural default for sensitive text.
- **GitHub Copilot CLI.** Evaluate its one-shot mode first.
- **OpenCode one-shot.** Only with a verified read-only agent.
- **MSIX apps** such as the ChatGPT desktop app, through `PackageManager.FindPackagesForUser` and `shell:AppsFolder\<AUMID>`.
- **Web fallback**: open `claude.ai/new?q=…` or `chatgpt.com/?q=…`. The text then lands in browser history and is length-limited, so it is labelled as such.

## Verification

- `Build.ps1 -Task VerifyConfigSave` covers the Core parts: id round trip, `IsKnown`, unknown tool and task rejected, and a config with an AI button for a missing tool still validating.
- Detection against the real machine was run through a scratch harness that compiles `AiToolDetector.cs` with its dependencies:
  - Claude Code, Codex and OpenCode are found.
  - Antigravity is reported as Stale.
- By hand:
  - Open a CLI tool from the ring while an Explorer window is in front, and check the terminal starts in that folder.
  - Do the same with no Explorer window, and check it starts in the profile folder.
  - Switch a tool off in Settings, and check it leaves the picker and its existing button reports it.
