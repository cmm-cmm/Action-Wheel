using Action_Wheel.Core;

// A regression check for three ways the button configuration used to be lost or broken without
// anyone touching it:
//
// 1. A missing icon file rejected the whole of actions.json, so all nine buttons fell back to the
//    defaults over one picture. Startup's icon cleanup also skipped group children, so any icon
//    used only by a group child was deleted and then rejected the config on the next load.
// 2. Saving over a rejected actions.json copied the broken file over the last good .bak, which
//    left nothing to restore.
// 3. Renaming a profile left every "switch to profile" button pointing at the old name.
//
// There is deliberately no test project (see CLAUDE.md); run this by hand after touching
// ActionsValidator, ActionConfig.SaveTo or WindowsFunctions.RenameProfileReferences, with:
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
