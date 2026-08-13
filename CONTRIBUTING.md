# Contributing to Action Wheel

Thanks for taking the time to contribute. This document covers how to get a
working build, the conventions the codebase follows, and what a good pull
request looks like. See also [AGENTS.md](AGENTS.md) for the terse reference
version of these rules and the [README](README.md) for a feature overview.

## Code of Conduct

This project follows a [Code of Conduct](CODE_OF_CONDUCT.md). By
participating you are expected to uphold it.

## Requirements

- Windows 10 version 1809 (build 17763) or later
- .NET 10.0 SDK
- Windows App SDK 1.8
- Visual Studio 2022 with the "Windows application development" workload
  (for debugging with F5)

## Getting started

```powershell
git clone https://github.com/cmm-cmm/Action-Wheel.git
cd Action-Wheel
.\Build.ps1                # Debug build (the default)
```

`Build.ps1` is the single entry point for every build task — the same
commands run locally and in CI:

```powershell
.\Build.ps1                       # Debug build
.\Build.ps1 -Task Release
.\Build.ps1 -Task Publish         # self-contained single-file exe -> bin\Publish\win-x64
.\Build.ps1 -Task Package         # Publish, ZIP it, then launch the ZIP's exe as a smoke test
.\Build.ps1 -Task Installer       # Publish, then the Inno Setup installer
.\Build.ps1 -Task VerifyBindings  # checks every classic {Binding} path against its type
.\Build.ps1 -Task Clean
.\Build.ps1 -Task All
```

In Visual Studio, open the solution and press F5 — there is exactly one
launch profile, **"Action Wheel (Unpackaged)"**. `Assert-NotRunning` fires on
every build task: quit a running copy from its tray icon first, or the copy
step fails deep inside MSBuild with an error that never mentions the app
being open.

## Project layout

Two assemblies: `ActionWheel.Core` (plain `net10.0`, no WinUI or `user32`,
namespace `Action_Wheel.Core`) holds the business logic; `Action Wheel` is
the WinUI 3 app on top of it. Application startup and the informational
window live in `App.xaml(.cs)` / `MainWindow.xaml(.cs)`. Global hooks,
action execution, configuration, and tray behavior live in `Services/`. The
radial overlay is in `Overlay/`; settings UI and editable view models are in
`Settings/` and `ViewModels/`. Packaging images are in `Assets/`; historical
design/deployment notes are under `doc/`.

## Coding style

- Four-space indentation in C# and XAML.
- PascalCase for types and public members, camelCase for locals and
  parameters, `_camelCase` for private fields.
- Nullable reference types are enabled — resolve warnings rather than
  suppressing them.
- Keep XAML and its code-behind paired; place new classes under the
  `Action_Wheel.<Folder>` namespace matching their folder.
- Preserve explanatory comments around Win32 hooks, transparency,
  threading, and focus behavior — these areas have non-obvious constraints
  that are easy to silently break. If you touch code with a comment
  explaining *why* something is done a certain way, read it before changing
  the *what*.

## Testing your change

There is no automated test project yet (deliberately out of scope so far).
At minimum:

1. `dotnet build "Action Wheel.csproj"` must succeed with no new warnings.
2. Manually verify the Windows behavior your change affects: middle-click
   opening the menu, Esc/outside-click dismissal, shortcut or launch
   dispatch, tray commands, settings persistence.
3. If you're adding tests, create a separate test project and name files
   `<TypeName>Tests.cs` with behavior-focused test names.

## Commit and pull request guidelines

- Write short, imperative commit summaries (e.g. `Fix ring geometry clamp
  on secondary monitors`).
- Keep each commit focused; don't commit generated artifacts (`bin/`,
  `obj/`, publish/installer output — all covered by `.gitignore`).
- In the PR description: explain the user-visible behavior, list what you
  verified manually, link relevant issues, and include a screenshot or
  short recording for any overlay/settings UI change.
- Call out explicitly if your change touches hooks, manifests, publishing,
  or `actions.json` compatibility — these have the highest blast radius in
  this codebase.

## Reporting bugs and requesting features

Open a [GitHub issue](https://github.com/cmm-cmm/Action-Wheel/issues) with
steps to reproduce, what you expected, and what happened instead. For
security vulnerabilities, do **not** open a public issue — see
[SECURITY.md](SECURITY.md).
