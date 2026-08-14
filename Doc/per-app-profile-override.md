# Per-app automatic profile override

**Status: proposed, not yet implemented.** Design agreed with the user; deferred until a future session.

## Context

Today the radial menu always shows whichever profile is currently "active" (`actions.json`, switched manually via the tray menu or a `WindowsFunctions.ActionSwitchProfile` button). The idea: the menu should notice which app is in the foreground when it opens — e.g. Excel — and, if the user configured a mapping for that app, show that app's profile buttons **for that one menu only**, without disturbing the profile manually chosen as active. Closing the menu (or opening it over an unmapped app) reverts to normal behaviour with zero persisted state change.

This is a one-shot, read-only override — it must never write `actions.json` or `active-profile.txt`, and it must never block the menu from opening (unmapped app, missing profile, or any resolution failure all silently fall back to the current active profile).

## Decisions already made with the user

- **Adding a mapping is manual text entry only** (process name typed in, e.g. `EXCEL`) — no "capture the currently focused app" convenience button for v1. Opening Settings itself steals focus from whatever app the user would want to capture, so that button would need its own "remember the app focused right before Settings opened" mechanism; not worth the complexity for a first version.
- **UI lives inside the existing "Profiles & backup" section** of Settings, as a new card next to the current Profiles card — not a new top-level navigation item.
- **Fallback is silent.** No mapping, a resolution failure, or a profile that fails to load all just fall back to the current active profile with no error shown anywhere. The app's existing philosophy is "never let a config problem block the menu" (see `ActionConfig`'s Rejected-but-defaults-used handling) — this follows the same rule.

## Design

### New: `ActionWheel.Core/AppProfileRule.cs`
A plain record, alongside `ActionItem`/`RingAppearance`:
```csharp
public sealed record AppProfileRule(string ProcessName, string ProfileName);
```
`ProcessName` is compared case-insensitively against `System.Diagnostics.Process.ProcessName` (already extension-less, e.g. `"EXCEL"`, `"chrome"`, `"Code"` — no `.exe` to strip or normalize).

### New: `Services/ForegroundProcess.cs`
Static helper, one method:
```csharp
public static bool TryGetProcessName(IntPtr hwnd, out string name)
```
Implementation: `GetWindowThreadProcessId` (P/Invoke, add alongside the existing `[DllImport]` block in `ActionDispatcher.cs`'s style) → `Process.GetProcessById(pid).ProcessName`. Wrapped in try/catch returning `false` on any failure (process exited between capture and resolve, access denied for a protected/elevated process, invalid handle) — mirrors the "never throws" contract `ActionDispatcher.CaptureTargetWindow`/`Execute` already follow. Not performance-sensitive: it's called once per menu-open from the UI-thread dispatch block (see below), not from the raw mouse-hook callback, so there's no `LowLevelHooksTimeout` risk and no need for the raw-P/Invoke rigor `MouseHook.ReadEvent` uses for the per-move callback.

### New: `Services/AppProfileSettings.cs`
Static class, modeled directly on `TriggerSettings.LoadPreferences()`/`Save()` (`JsonDocument`/`Utf8JsonWriter` + `AtomicFile.TryWriteText`, not `JsonSerializer<T>`, per the project's no-reflection persistence convention):
```csharp
public static IReadOnlyList<AppProfileRule> Load()
public static bool Save(IReadOnlyList<AppProfileRule> rules, out string error)
```
Stored as its own file, `%LOCALAPPDATA%\ActionWheel\app-profiles.json` (via `AppDataPaths.DirectoryPath`) — a JSON array of `{ "process": "...", "profile": "..." }`, not folded into `preferences.json`, so `TriggerPreferences`/`TriggerSettings` stay focused on trigger+ring appearance as they are today. `Load()` never throws (empty list on any failure), matching `TriggerSettings.LoadPreferences()`'s try/catch shape.

### Modified: `Services/LauncherService.cs`
- New field `_appProfileRules` (`IReadOnlyList<AppProfileRule>`), loaded once in the constructor and refreshed inside `ReloadActions()` (same place `ApplyTriggerSettings()`/`_appearance` already refresh) — so saving the mapping list in Settings picks up immediately via the existing `settings.ActionsSaved → _launcherService.ReloadActions()` wiring in `App.xaml.cs`, no new plumbing needed there.
- In `OpenMenuAt`, **inside the existing `_dispatcherQueue.TryEnqueue(...)` lambda** (UI thread, after `CloseCurrentMenu()`, before `new RadialMenu(...)`) — deliberately not on the hook-callback thread where `CaptureTargetWindow()` itself still runs:
  ```csharp
  var menuActions = _actions;
  if (ForegroundProcess.TryGetProcessName(_targetWindow, out string processName))
  {
      var rule = _appProfileRules.FirstOrDefault(r =>
          string.Equals(r.ProcessName, processName, StringComparison.OrdinalIgnoreCase));
      if (rule != null && _profileLibrary.TryLoad(rule.ProfileName, out var overrideActions, out _))
          menuActions = overrideActions;
  }
  var menu = new RadialMenu(menuActions, _appearance);
  ```
  No mapping, resolution failure, or profile-load failure ever throws or blocks — `menuActions` just stays `_actions`. Nothing here calls `ActionConfig.Save`, `ActiveProfileSettings.Save`, or reassigns the `_actions` field itself, so the user's manually-active profile is untouched and the very next unmapped-app menu open is back to normal with no state to unwind.
- `LauncherService` needs a `ProfileLibrary` instance (`_profileLibrary`) — reuse the same class `SettingsViewModel` already uses (`Services/ProfileLibrary.cs`, `TryLoad(name, out IReadOnlyList<ActionItem>, out error)`), constructed once alongside the other fields.

### New: `Settings/AppProfileRuleRow.cs`
A small mutable row edit-model next to `ActionEditModel.cs`, following the same `ObservableObject` shape:
```csharp
public sealed class AppProfileRuleRow : ObservableObject
{
    public string ProcessName { get; set; }   // Set/Raise-backed
    public string ProfileName { get; set; }   // Set/Raise-backed, bound to a ComboBox using SettingsViewModel.Profiles
}
```

### Modified: `ViewModels/SettingsViewModel.Profiles.cs`
Following the exact pattern already there (`ObservableCollection<string> Profiles`, `RelayCommand`s, `Set`/`Raise`):
- `ObservableCollection<AppProfileRuleRow> AppProfileRules { get; } = new();`
- `AddAppProfileRuleCommand` (appends an empty row), `RemoveAppProfileRuleCommand` (parameterized by row, same `Tag`/`Click` code-behind pattern the button-list rows use rather than a generic `RelayCommand<T>`, since that's what this codebase's hand-rolled `RelayCommand` supports).
- Loaded from `AppProfileSettings.Load()` in `LoadFromDisk()`/`ReloadFromDisk()` (same place `TriggerSettings.LoadPreferences()` is read), saved via `AppProfileSettings.Save(...)` from the same place the main Save command already calls `TriggerSettings.Save(...)` — part of the existing single "Save" action, not a separate live-write.

### Modified: `Settings/SettingsWindow.xaml`
New `CardBorderStyle` card in the existing **Profiles & backup** section, placed between the current Profiles card and the Backup card: title "Automatic profile by app", one line of description ("Load a different profile automatically while a matching app is in the foreground — this doesn't change your active profile."), an `ItemsControl` bound to `AppProfileRules` with a `DataTemplate` row of `TextBox` (ProcessName, placeholder `"e.g. EXCEL"`) + `ComboBox` (ProfileName, `ItemsSource="{Binding Profiles}"` on the parent VM via `ElementName` binding, matching how the detail panel already binds outward to the parent's `DataContext`) + a small icon-only remove button (`AutomationProperties.Name="Remove this rule"`, per the accessibility pass already done on the other icon-only buttons) — and an "Add rule" button below the list bound to `AddAppProfileRuleCommand`.

### `Verify-Bindings.ps1`
Check its scope map after adding `AppProfileRuleRow` and the new XAML bindings — the `SettingsViewModel*.cs` wildcard already covers the new partial-file properties, but `AppProfileRuleRow` is a new row type (like `ActionEditModel`) that may need adding explicitly if the checker's type map isn't wildcarded for row classes. Run `Build.ps1 -Task VerifyBindings` and fix the scope map if it reports unresolved paths rather than assuming it's already covered.

## Verification (once implemented)
1. `dotnet build` (Debug) — 0 warnings/errors.
2. `Build.ps1 -Task VerifyBindings` — all paths resolve.
3. Manual smoke test: add a rule mapping a running app's actual process name (e.g. `notepad` → a test profile with different buttons) to `app-profiles.json` via the new UI, Save, focus that app, trigger the menu — confirm the mapped profile's buttons show; trigger the menu over an unmapped app — confirm the normal active profile shows unchanged; check `active-profile.txt`/`actions.json` are untouched by either.
4. Confirm a deliberately-broken rule (profile name that doesn't exist) falls back silently with no error shown and no crash.

## Research notes (from codebase exploration)

- `LauncherService.OpenMenuAt` (`Services/LauncherService.cs`) is the single construction site for `RadialMenu` for both the mouse trigger (`OnMiddleClick`) and the keyboard emergency trigger (`OnEmergencyTrigger`, Ctrl+Alt+Space) — both funnel through it, so the override applies identically to either.
- `ActionDispatcher.CaptureTargetWindow()` already calls `GetForegroundWindow()` synchronously before the UI-thread dispatch, giving the HWND this feature needs for free.
- No existing code in the repo resolves a HWND/PID to a process name (`GetWindowThreadProcessId`, `QueryFullProcessImageName`, `Process.GetProcessById` all have zero hits) — `ForegroundProcess.cs` is entirely new.
- `Services/ProfileLibrary.cs`'s `TryLoad(name, out IReadOnlyList<ActionItem>, out error)` is a read-only, no-side-effect way to get a named profile's 9 `ActionItem`s without touching `actions.json`/`active-profile.txt` — exactly what a one-shot override needs. `SwitchProfile()` (the existing "load a profile" path) is the wrong model to copy: it deliberately persists via `ActionConfig.Save` + `ActiveProfileSettings.Save`, which this feature must not do.
- `_actions` in `LauncherService` currently has exactly one producer, `ReloadActions()`, which always re-reads the global `actions.json` and overwrites the field for every future menu — there is no existing "swap actions for just this one open" mechanism; this feature introduces the first one, as a local variable in `OpenMenuAt` rather than a field mutation.
