using Action_Wheel.Core;

// A regression check for three ways the button configuration used to be lost or broken without
// anyone touching it:
//
// 1. A missing icon file rejected the whole of actions.json, so all nine buttons fell back to the
//    defaults over one picture. Startup's icon cleanup also skipped group children, so any icon
//    used only by a group child was deleted and then rejected the config on the next load.
// 2. Saving over a rejected actions.json copied the broken file over the last good .bak, which
//    left nothing to restore.
// 3. Renaming a profile left every "switch to profile" button pointing at the old name, and Undo
//    brought the old name back.
//
// Plus two checks on what a configuration does once it loads:
//
// 4. "Ctrl++" passed validation and then sent Ctrl alone; punctuation keys had no names at all.
// 5. A profile or backup from somewhere else could launch any command line, unlisted.
// 6. AI function ids must be valid by the static catalog alone, so a button for a tool that is
//    not installed still loads (see Doc/ai-assistants.md).
// 7. AI tasks: template ids, prompt building, the template file, and the fixed command lines,
//    which must never carry a permission-widening flag.
//
// There is deliberately no test project (see CLAUDE.md); run this by hand after touching
// ActionsValidator, ActionConfig.SaveTo, WindowsFunctions.RenameProfileReferences,
// UndoHistory, ShortcutKeys or LaunchSummary, with:
//   dotnet run --project Tools\ConfigSaveCheck\ConfigSaveCheck.csproj

int failures = 0;

void Check(string label, bool passed)
{
    Console.WriteLine(passed ? $"  OK    {label}" : $"  FAIL  {label}");
    if (!passed)
        failures++;
}

Console.WriteLine("Config save regression check");
Console.WriteLine();

string directory = Path.Combine(Path.GetTempPath(), "ActionWheelConfigSaveCheck-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);

try
{
    var defaults = ActionConfig.Defaults().ToList();
    string missingIcon = Path.Combine(directory, "deleted-icon.png");

    // 1. A missing icon file is not a reason to reject the configuration.
    {
        var withMissingIcon = defaults.Select(a => a.Tag == 3 ? a with { IconPath = missingIcon } : a).ToList();
        Check("icons: a top-level button with a missing .png still validates",
            ActionConfig.TryValidate(withMissingIcon, out _));

        var child = new ActionItem
        {
            Tag = 0, Label = "Group child", Kind = ActionKind.Launch, Value = "notepad.exe",
            Glyph = "F0C5", IconPath = missingIcon,
        };
        var withGroup = defaults.Select(a => a.Tag == 4
            ? a with { Kind = ActionKind.Group, Value = string.Empty, GroupChildren = new List<ActionItem> { child } }
            : a).ToList();
        Check("icons: a group child with a missing icon still validates",
            ActionConfig.TryValidate(withGroup, out _));

        var unsupported = defaults.Select(a => a.Tag == 3 ? a with { IconPath = Path.Combine(directory, "icon.bmp") } : a).ToList();
        Check("icons: an unsupported icon type is still rejected",
            !ActionConfig.TryValidate(unsupported, out _));

        string path = Path.Combine(directory, "icons.json");
        Check("icons: a config with a missing icon can be saved",
            ActionConfig.SaveTo(path, withGroup, out _));
        Check("icons: and loads back as valid",
            ActionConfig.TryValidate(ActionConfig.Parse(File.ReadAllText(path)), out _));
    }

    // 2. Saving over a rejected file keeps the last good backup.
    {
        string path = Path.Combine(directory, "actions.json");
        string backup = ActionConfig.BackupPathFor(path);
        string invalid = path + ".invalid";

        ActionConfig.SaveTo(path, defaults, out _);
        Check("backup: a save over a valid file writes a .bak",
            ActionConfig.SaveTo(path, defaults, out _) && File.Exists(backup));
        string goodBackup = File.ReadAllText(backup);

        File.WriteAllText(path, "[ { \"tag\": 0, broken");
        Check("backup: a save over a rejected file succeeds",
            ActionConfig.SaveTo(path, defaults, out _));
        Check("backup: the good .bak is not overwritten by the rejected file",
            File.ReadAllText(backup) == goodBackup);
        Check("backup: the rejected file is kept as .invalid",
            File.Exists(invalid) && File.ReadAllText(invalid).Contains("broken"));

        var edited = defaults.Select(a => a.Tag == 1 ? a with { Label = "Copy (edited)" } : a).ToList();
        ActionConfig.SaveTo(path, edited, out _);
        ActionConfig.SaveTo(path, defaults, out _);
        Check("backup: a save over a valid file still refreshes .bak",
            File.ReadAllText(backup).Contains("Copy (edited)"));
    }

    // 3. Renaming a profile renames every reference to it, and nothing else.
    {
        string oldId = WindowsFunctions.ProfileChoice("Work").Id;
        string newId = WindowsFunctions.ProfileChoice("Office").Id;
        string otherId = WindowsFunctions.ProfileChoice("Home").Id;

        var child = new ActionItem
        {
            Tag = 0, Label = "Child switch", Kind = ActionKind.Function, Value = oldId, Glyph = "F0C5",
        };
        var actions = defaults.Select(a => a.Tag switch
        {
            1 => a with { Kind = ActionKind.Function, Value = oldId },
            2 => a with { HoldKind = ActionKind.Function, HoldValue = oldId },
            3 => a with { Kind = ActionKind.Function, Value = otherId },
            4 => a with { Kind = ActionKind.Group, Value = string.Empty, GroupChildren = new List<ActionItem> { child } },
            _ => a,
        }).ToList();

        var renamed = WindowsFunctions.RenameProfileReferences(actions, "work", "Office", out bool changed);
        Check("rename: reports a change", changed);
        Check("rename: a primary action is renamed (old name matched case-insensitively)", renamed[1].Value == newId);
        Check("rename: a hold action is renamed", renamed[2].HoldValue == newId);
        Check("rename: a group child is renamed", renamed[4].GroupChildren[0].Value == newId);
        Check("rename: a different profile is left alone", renamed[3].Value == otherId);
        Check("rename: an unrelated button is the same instance", ReferenceEquals(renamed[5], actions[5]));
        Check("rename: the result still validates", ActionConfig.TryValidate(renamed, out _));

        WindowsFunctions.RenameProfileReferences(defaults, "Work", "Office", out bool unchanged);
        Check("rename: no reference means no change", !unchanged);

        // Undo used to bring the old name back after a rename.
        var history = new UndoHistory<ActionItem>(actions);
        history.Capture(renamed);
        history.Map(snapshot => WindowsFunctions.RenameProfileReferences(snapshot, "Work", "Office", out _));
        history.TryUndo(out var undone);
        Check("rename: undo after a rename keeps the new name", undone[1].Value == newId);
        history.TryRedo(out var redone);
        Check("rename: redo still works after mapping the history", redone[1].Value == newId && redone.Count == actions.Count);
    }

    // 4. A shortcut never sends less than it says. "Ctrl++" used to validate and send Ctrl alone.
    {
        IReadOnlyList<ushort> Keys(string shortcut) => ShortcutKeys.Parse(shortcut);

        Check("shortcuts: Ctrl++ is Ctrl plus the Plus key", Keys("Ctrl++").SequenceEqual(new ushort[] { 0x11, 0xBB }));
        Check("shortcuts: Ctrl+Plus is the same chord", Keys("Ctrl+Plus").SequenceEqual(Keys("Ctrl++")));
        Check("shortcuts: Ctrl+= is the same chord", Keys("Ctrl+=").SequenceEqual(Keys("Ctrl++")));
        Check("shortcuts: Ctrl+Shift++ keeps both modifiers", Keys("Ctrl+Shift++").SequenceEqual(new ushort[] { 0x11, 0x10, 0xBB }));
        Check("shortcuts: Ctrl+- is Ctrl+Minus", Keys("Ctrl+-").SequenceEqual(Keys("Ctrl+Minus")) && Keys("Ctrl+-").Count == 2);
        Check("shortcuts: Ctrl+/ and Ctrl+` are recognised", ShortcutKeys.IsValid("Ctrl+/") && ShortcutKeys.IsValid("Ctrl+`"));
        Check("shortcuts: a trailing + is rejected", !ShortcutKeys.IsValid("Ctrl+"));
        Check("shortcuts: a leading + is rejected", !ShortcutKeys.IsValid("+A"));
        Check("shortcuts: a doubled + mid-chord is rejected", !ShortcutKeys.IsValid("Ctrl++S"));
        ShortcutKeys.Parse("Ctrl+", out string missing);
        Check("shortcuts: an empty part is reported as such", missing == ShortcutKeys.MissingKey);
        Check("shortcuts: an ordinary chord is unchanged", Keys("Ctrl+Shift+S").SequenceEqual(new ushort[] { 0x11, 0x10, 0x53 }));
    }

    // 5. Everything a profile can launch is listed before it is imported or restored.
    {
        var child = new ActionItem
        {
            Tag = 0, Label = "Child", Kind = ActionKind.Launch, Value = "powershell.exe",
            Arguments = "-File evil.ps1", Glyph = "F0C5",
        };
        var actions = defaults.Select(a => a.Tag switch
        {
            1 => a with { Kind = ActionKind.Launch, Value = "notepad.exe" },
            2 => a with { HoldKind = ActionKind.Launch, HoldValue = "cmd.exe", HoldArguments = "/c del x" },
            3 => a with { Kind = ActionKind.Launch, Value = "NOTEPAD.EXE" },
            4 => a with { Kind = ActionKind.Group, Value = string.Empty, GroupChildren = new List<ActionItem> { child } },
            _ => a,
        }).ToList();

        var commands = LaunchSummary.Commands(actions);
        Check("launches: a primary launch is listed", commands.Contains("notepad.exe"));
        Check("launches: a hold launch is listed with its arguments", commands.Contains("cmd.exe /c del x"));
        Check("launches: a group child's launch is listed", commands.Contains("powershell.exe -File evil.ps1"));
        Check("launches: duplicates are listed once", commands.Count == 3);
        Check("launches: built-in functions and shortcuts are not listed", LaunchSummary.Commands(defaults).Count == 0);
    }

    // 6. AI function ids are decided by the static catalog, never by what is installed.
    {
        string open = AiTools.FunctionId("claude");
        Check("ai: the id has the documented shape", open == "action-wheel.ai:claude/open");
        Check("ai: it parses back to its tool and task",
            AiTools.TryParse(open, out string toolId, out string task) && toolId == "claude" && task == AiTools.OpenTask);
        Check("ai: a catalog tool's open id is known", WindowsFunctions.IsKnown(open));
        Check("ai: an unknown tool is not known", !WindowsFunctions.IsKnown("action-wheel.ai:no-such-tool/open"));
        Check("ai: an unknown task is not known", !WindowsFunctions.IsKnown("action-wheel.ai:claude/explode"));
        Check("ai: malformed ids are not known",
            !WindowsFunctions.IsKnown("action-wheel.ai:claude") && !WindowsFunctions.IsKnown("action-wheel.ai:/open")
            && !WindowsFunctions.IsKnown("action-wheel.ai:claude/"));
        Check("ai: catalog ids are unique",
            AiTools.All.Select(t => t.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() == AiTools.All.Count);
        Check("ai: every catalog tool has a way to be found",
            AiTools.All.All(t => t.Commands.Count + t.KnownPaths.Count + t.Protocols.Count > 0));

        // The case the design exists for: a button for a tool that is not installed (Windsurf is
        // not on the development machine) must not reject the configuration.
        var withAi = defaults.Select(a => a.Tag switch
        {
            1 => a with { Kind = ActionKind.Function, Value = AiTools.FunctionId("windsurf") },
            2 => a with { HoldKind = ActionKind.Function, HoldValue = AiTools.FunctionId("antigravity") },
            _ => a,
        }).ToList();
        Check("ai: a config with AI buttons validates whatever is installed", ActionConfig.TryValidate(withAi, out _));

        var tool = AiTools.Find("CLAUDE")!;
        Check("ai: tools are found case-insensitively", tool.Id == "claude");
        Check("ai: an unavailable tool's picker entry says so", AiTools.Choice(tool, available: false).Name.Contains("(not available)"));
        Check("ai: picker entries fall in the AI category", AiTools.Choice(tool).Category == "AI");

        // VS Code puts a "GitHub Copilot CLI launcher" on PATH ahead of the real CLI. Detection must
        // skip it, or the real CLI is never found and a missing one looks installed.
        var copilot = AiTools.Find("copilot")!;
        Check("ai: VS Code's Copilot launcher is recognised",
            copilot.IsLauncher(@"C:\Program Files\Microsoft VS Code\bin\copilot-shim\copilot.exe"));
        Check("ai: the real Copilot CLI is not taken for a launcher",
            !copilot.IsLauncher(@"C:\Users\x\AppData\Local\Microsoft\WinGet\Links\copilot.exe"));
        Check("ai: tools without launcher markers have no launchers", !tool.IsLauncher(@"C:\anything\copilot-shim\claude.exe"));
        Check("ai: Copilot CLI cannot run tasks (its one-shot mode needs --allow-all-tools)",
            !copilot.SupportsTasks && !WindowsFunctions.IsKnown(AiTools.TaskFunctionId("copilot", "explain")));
    }

    // 7. AI tasks: template ids are static too, and the command lines stay locked down.
    {
        string task = AiTools.TaskFunctionId("claude", "explain");
        Check("tasks: the id has the documented shape", task == "action-wheel.ai:claude/task:explain");
        Check("tasks: a task id for a task-capable tool is known", WindowsFunctions.IsKnown(task));
        Check("tasks: a task id for a deleted template is still known", WindowsFunctions.IsKnown(AiTools.TaskFunctionId("codex", "no-such-template")));
        Check("tasks: OpenCode cannot run tasks", !WindowsFunctions.IsKnown(AiTools.TaskFunctionId("opencode", "explain")));
        Check("tasks: an invalid template id is not known", !WindowsFunctions.IsKnown("action-wheel.ai:claude/task:Bad Id"));

        foreach (var taskTool in AiTools.All.Where(t => t.SupportsTasks))
        {
            var arguments = AiTools.TaskArguments(taskTool, @"C:\temp\answer.txt");
            string joined = string.Join(" ", arguments);
            Check($"tasks: {taskTool.Name} never gets a permission-widening flag",
                !joined.Contains("dangerously", StringComparison.OrdinalIgnoreCase)
                && !joined.Contains("bypassPermissions", StringComparison.OrdinalIgnoreCase)
                && !joined.Contains("danger-full-access", StringComparison.OrdinalIgnoreCase)
                && !joined.Contains("full-auto", StringComparison.OrdinalIgnoreCase));
        }
        var claudeArgs = AiTools.TaskArguments(AiTools.Find("claude")!, "x");
        Check("tasks: Claude Code runs with every tool disabled",
            claudeArgs.Contains("-p") && claudeArgs.SkipWhile(a => a != "--tools").Skip(1).FirstOrDefault() == "");
        var codexArgs = AiTools.TaskArguments(AiTools.Find("codex")!, "x");
        Check("tasks: Codex runs in the read-only sandbox, prompt from stdin",
            codexArgs.SkipWhile(a => a != "-s").Skip(1).FirstOrDefault() == "read-only" && codexArgs[^1] == "-");

        var template = new AiTaskTemplate("t", "T", "Explain:\n\n{selection}\n\nThanks");
        Check("tasks: the placeholder is replaced", AiTaskTemplates.BuildPrompt(template, "HELLO") == "Explain:\n\nHELLO\n\nThanks");
        Check("tasks: without a placeholder the text is appended",
            AiTaskTemplates.BuildPrompt(template with { Prompt = "Explain this." }, "HELLO") == "Explain this.\n\nHELLO");

        Check("tasks: a new id is a clean slug", AiTaskTemplates.NewId("Dịch sang tiếng Anh!", Array.Empty<string>()) == "d-ch-sang-ti-ng-anh");
        Check("tasks: a new id never collides", AiTaskTemplates.NewId("Explain", new[] { "explain", "explain-2" }) == "explain-3");
        Check("tasks: an empty name still gets an id", AiTaskTemplates.IsValidId(AiTaskTemplates.NewId("???", Array.Empty<string>())));

        var defaultsBack = AiTaskTemplates.Parse(AiTaskTemplates.ToJson(AiTaskTemplates.Defaults()));
        Check("tasks: templates round-trip through JSON", defaultsBack.SequenceEqual(AiTaskTemplates.Defaults()));
        Check("tasks: built-in templates all ask before sending", AiTaskTemplates.Defaults().All(t => t.Confirm));
        Check("tasks: built-in template ids are valid", AiTaskTemplates.Defaults().All(t => AiTaskTemplates.IsValidId(t.Id)));

        bool rejected;
        try { AiTaskTemplates.Parse("{\"templates\":[{\"id\":\"a\"},{\"id\":\"A\"}]}"); rejected = false; }
        catch (FormatException) { rejected = true; }
        Check("tasks: duplicate template ids are rejected", rejected);
    }
}
finally
{
    try { Directory.Delete(directory, recursive: true); } catch { }
}

Console.WriteLine();
Console.WriteLine(failures == 0
    ? "All config save checks passed."
    : $"{failures} config save check(s) failed.");

return failures == 0 ? 0 : 1;
