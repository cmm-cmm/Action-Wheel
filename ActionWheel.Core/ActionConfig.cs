using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Action_Wheel.Core
{
    /// <summary>How <see cref="ActionConfig.Load"/> arrived at the actions it returned.</summary>
    public enum ConfigStatus
    {
        /// <summary>A configuration file was read and accepted.</summary>
        Loaded,

        /// <summary>No configuration file existed yet; the defaults were written out.</summary>
        WroteDefaults,

        /// <summary>
        /// A file exists but could not be used. The built-in defaults are in effect and the file was
        /// left untouched, so it can still be fixed by hand or replaced from the backup.
        /// </summary>
        Rejected,
    }

    /// <summary>
    /// The actions in effect, plus why. The caller needs the "why": silently running on defaults
    /// after a typo in actions.json looks exactly like the app having lost the user's settings.
    /// </summary>
    public sealed class ConfigLoadResult
    {
        public required IReadOnlyList<ActionItem> Actions { get; init; }
        public required ConfigStatus Status { get; init; }

        /// <summary>The file the actions came from, or the one that was rejected.</summary>
        public required string Path { get; init; }

        /// <summary>Why the file was rejected. Empty unless <see cref="Status"/> is Rejected.</summary>
        public string Error { get; init; } = string.Empty;

        /// <summary>True when a .bak sits next to the rejected file and could be restored.</summary>
        public bool BackupAvailable { get; init; }
    }

    /// <summary>
    /// Loads and saves the button configuration in actions.json.
    /// </summary>
    /// <remarks>
    /// Two locations are searched, in order: next to the executable (portable installs, where the
    /// file is read-only and hand-edited), then %LOCALAPPDATA%\ActionWheel. The first one that
    /// exists shadows the other completely - if it is invalid the app falls back to the built-in
    /// defaults for the session rather than reading the lower-priority file, because that is the
    /// same file <see cref="ActiveConfigPath"/> resolves to and the one Save would overwrite. If
    /// neither exists the defaults are written to the LOCALAPPDATA copy, because the install
    /// directory is often Program Files and not writable.
    ///
    /// Parsing goes through JsonDocument rather than JsonSerializer&lt;T&gt; on purpose: it needs no
    /// reflection, so enabling PublishTrimmed later can't silently break the config at runtime.
    /// </remarks>
    public static class ActionConfig
    {
        public const string FileName = "actions.json";

        public const string BackupExtension = ".bak";

        public static string UserConfigPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ActionWheel",
            FileName);

        public static string PortableConfigPath => Path.Combine(AppContext.BaseDirectory, FileName);

        /// <summary>
        /// The file that is actually in effect - the same one <see cref="Load"/> would read.
        /// The settings window writes here rather than to <see cref="UserConfigPath"/>: with a
        /// portable copy present next to the exe, saving to LOCALAPPDATA would appear to work but
        /// be shadowed on the next load.
        /// </summary>
        public static string ActiveConfigPath =>
            File.Exists(PortableConfigPath) ? PortableConfigPath : UserConfigPath;

        /// <summary>The backup written before each successful save.</summary>
        public static string BackupPathFor(string configPath) => configPath + BackupExtension;

        /// <summary>
        /// Returns the configured actions, falling back to the built-in defaults if no config file
        /// exists or the file cannot be read. Never throws - a broken config must not stop the
        /// menu from opening.
        /// </summary>
        public static IReadOnlyList<ActionItem> Load() => LoadDetailed().Actions;

        /// <summary>
        /// <see cref="Load"/>, plus what happened. Used by the UI so a rejected file can be reported
        /// and offered for restore instead of being papered over with the defaults.
        /// </summary>
        public static ConfigLoadResult LoadDetailed()
        {
            foreach (var path in new[] { PortableConfigPath, UserConfigPath })
            {
                if (!File.Exists(path))
                    continue;

                // The first existing path decides the outcome outright, valid or not - it never
                // falls through to a lower-priority path from here. That has to match
                // ActiveConfigPath, which resolves to this same first-existing path regardless of
                // whether it is valid. Falling through past a bad file here used to let the app run
                // happily off a good LOCALAPPDATA file while Save and "Open config file" kept
                // targeting the broken portable one beside the exe - the running config and the one
                // any edit would actually land in were two different files, silently.
                try
                {
                    var parsed = Parse(File.ReadAllText(path));
                    if (TryValidate(parsed, out string error))
                    {
                        return new ConfigLoadResult
                        {
                            Actions = parsed.OrderBy(action => action.Tag).ToList(),
                            Status = ConfigStatus.Loaded,
                            Path = path,
                        };
                    }

                    // Deliberately not overwritten with the defaults. The file is the user's work,
                    // and a typo on one line should not cost them the other eight buttons.
                    return new ConfigLoadResult
                    {
                        Actions = Defaults(),
                        Status = ConfigStatus.Rejected,
                        Path = path,
                        Error = error,
                        BackupAvailable = File.Exists(BackupPathFor(path)),
                    };
                }
                catch (Exception ex)
                {
                    return new ConfigLoadResult
                    {
                        Actions = Defaults(),
                        Status = ConfigStatus.Rejected,
                        Path = path,
                        Error = ex.Message,
                        BackupAvailable = File.Exists(BackupPathFor(path)),
                    };
                }
            }

            TryWriteDefaults();
            return new ConfigLoadResult
            {
                Actions = Defaults(),
                Status = ConfigStatus.WroteDefaults,
                Path = UserConfigPath,
            };
        }

        /// <summary>
        /// Puts the .bak back in place of a rejected config. The rejected file is kept as
        /// <c>.invalid</c> rather than deleted - it is still the only copy of whatever the user was
        /// in the middle of writing.
        /// </summary>
        public static bool RestoreBackup(string configPath, out string error)
        {
            error = string.Empty;

            try
            {
                var backup = BackupPathFor(configPath);
                if (!File.Exists(backup))
                {
                    error = "There is no backup to restore.";
                    return false;
                }

                var parsed = Parse(File.ReadAllText(backup));
                if (!TryValidate(parsed, out error))
                {
                    error = $"The backup is not usable either: {error}";
                    return false;
                }

                if (File.Exists(configPath))
                    File.Move(configPath, configPath + ".invalid", overwrite: true);

                File.Copy(backup, configPath, overwrite: true);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        /// <summary>Parses the file format. Throws <see cref="FormatException"/> on malformed input.</summary>
        public static List<ActionItem> Parse(string json)
        {
            var items = new List<ActionItem>();
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                throw new FormatException("The configuration root must be an array.");

            foreach (var element in doc.RootElement.EnumerateArray())
                items.Add(ParseAction(element));

            return items;
        }

        /// <summary>
        /// Parses one action object - a top-level button or a group child, which share every field
        /// except that only a child's own "group" array (if any - children cannot nest, but nothing
        /// stops a malformed file from having one) is ignored rather than recursed into further.
        /// </summary>
        private static ActionItem ParseAction(JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Object)
                throw new FormatException("Every configuration entry must be an object.");

            if (!element.TryGetProperty("tag", out var tagProp) || !tagProp.TryGetInt32(out int tag))
                throw new FormatException("Every configuration entry must have an integer tag.");

            var type = ReadString(element, "type");
            if (!ActionValueCodec.TryParseKind(type, out var kind))
                throw new FormatException($"Tag {tag} has unknown action type '{type}'.");

            var children = new List<ActionItem>();
            if (kind == ActionKind.Group && element.TryGetProperty("group", out var groupProp)
                && groupProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var childElement in groupProp.EnumerateArray())
                    children.Add(ParseAction(childElement));
            }

            return new ActionItem
            {
                Tag = tag,
                Label = ReadString(element, "label"),
                Kind = kind,
                Value = ReadString(element, "value"),
                Arguments = ReadString(element, "arguments"),
                Glyph = ReadString(element, "glyph"),
                IconPath = ReadString(element, "iconPath"),
                IconScale = ReadDouble(element, "iconScale", ActionItem.DefaultIconScale),
                IconOffsetX = ReadDouble(element, "iconOffsetX", 0),
                IconOffsetY = ReadDouble(element, "iconOffsetY", 0),
                IconTint = ReadBool(element, "iconTint", ActionItem.DefaultIconTint),
                Foreground = ReadString(element, "foreground"),
                Background = ReadString(element, "background"),
                Shadow = ReadString(element, "shadow"),
                ShadowEnabled = ReadBool(element, "shadowEnabled", ActionItem.DefaultShadowEnabled),
                ShadowOpacity = ReadDouble(element, "shadowOpacity", ActionItem.DefaultShadowOpacity),
                ShadowBlur = ReadDouble(element, "shadowBlur", ActionItem.DefaultShadowBlur),
                ShadowOffsetX = ReadDouble(element, "shadowOffsetX", ActionItem.DefaultShadowOffsetX),
                ShadowOffsetY = ReadDouble(element, "shadowOffsetY", ActionItem.DefaultShadowOffsetY),
                // Absent entirely on a config with no hold action configured, so an empty
                // "holdType" is not an error the way an empty "type" is for the primary action - it
                // just means TryParseKind fails and this button has none.
                HoldKind = ActionValueCodec.TryParseKind(ReadString(element, "holdType"), out var holdKind)
                    ? holdKind : ActionKind.None,
                HoldValue = ReadString(element, "holdValue"),
                HoldArguments = ReadString(element, "holdArguments"),
                GroupChildren = children,
            };
        }

        private static string ReadString(JsonElement element, string name) =>
            element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String
                ? prop.GetString() ?? string.Empty
                : string.Empty;

        private static double ReadDouble(JsonElement element, string name, double fallback) =>
            element.TryGetProperty(name, out var prop) && prop.TryGetDouble(out double value)
                ? value : fallback;

        private static bool ReadBool(JsonElement element, string name, bool fallback) =>
            element.TryGetProperty(name, out var prop) && prop.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? prop.GetBoolean() : fallback;

        /// <summary>
        /// Checks the set of actions as a whole: exactly nine buttons, tags 0-8 with no repeats, and
        /// nothing that would produce a button that silently does nothing when clicked.
        /// </summary>
        public static bool TryValidate(IReadOnlyList<ActionItem> actions, out string error)
        {
            var issue = ActionsValidator.Validate(actions).FirstOrDefault();
            error = issue?.Message ?? string.Empty;
            return issue == null;
        }

        /// <summary>
        /// Writes the configuration to <see cref="ActiveConfigPath"/>. Returns false (with the
        /// reason in <paramref name="error"/>) instead of throwing, so the settings window can
        /// tell the user what happened - a portable install under Program Files is read-only.
        /// </summary>
        public static bool Save(IEnumerable<ActionItem> actions, out string error) =>
            SaveTo(ActiveConfigPath, actions, out error);

        /// <summary>
        /// <see cref="Save"/>, to a named file.
        /// </summary>
        /// <remarks>
        /// Never writes the destination directly. The JSON goes to a temp file in the same
        /// directory, that file is flushed all the way to disk, the previous contents are copied to
        /// <c>.bak</c>, and only then is the temp file moved into place. A crash or a power cut
        /// mid-save therefore leaves either the old file or the new one, never a half-written one -
        /// which the app would reject on the next launch and fall back to defaults over.
        /// </remarks>
        public static bool SaveTo(string path, IEnumerable<ActionItem> actions, out string error)
        {
            error = string.Empty;
            string? temporaryPath = null;

            try
            {
                var actionList = actions.ToList();
                if (!TryValidate(actionList, out error))
                    return false;

                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";

                using (var stream = new FileStream(
                    temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                    bufferSize: 4096, FileOptions.WriteThrough))
                {
                    WriteJson(stream, actionList);
                    stream.Flush(flushToDisk: true);
                }

                if (File.Exists(path))
                {
                    File.Copy(path, BackupPathFor(path), overwrite: true);
                    File.Move(temporaryPath, path, overwrite: true);
                }
                else
                {
                    File.Move(temporaryPath, path);
                }
                temporaryPath = null;

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
            finally
            {
                if (temporaryPath != null)
                {
                    try
                    {
                        File.Delete(temporaryPath);
                    }
                    catch
                    {
                        // Preserve the original save error; an orphaned temp file is recoverable.
                    }
                }
            }
        }

        /// <summary>The file format, as text. Exposed so a save can be inspected without a disk.</summary>
        public static string ToJson(IEnumerable<ActionItem> actions)
        {
            using var stream = new MemoryStream();
            WriteJson(stream, actions);
            return Encoding.UTF8.GetString(stream.ToArray());
        }

        private static void WriteJson(Stream stream, IEnumerable<ActionItem> actions)
        {
            // UnsafeRelaxedJsonEscaping keeps non-ASCII labels readable in the file instead of
            // turning every accented character into a \uXXXX escape.
            using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
            {
                Indented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            });

            writer.WriteStartArray();
            foreach (var action in actions)
                WriteAction(writer, action);
            writer.WriteEndArray();
            writer.Flush();
        }

        /// <summary>Writes one action object - shared between a top-level button and a group child.</summary>
        private static void WriteAction(Utf8JsonWriter writer, ActionItem action)
        {
            writer.WriteStartObject();
            writer.WriteNumber("tag", action.Tag);
            writer.WriteString("label", action.Label);
            writer.WriteString("type", ActionValueCodec.KindToString(action.Kind));
            writer.WriteString("value", action.Value);

            // Optional fields are only written when set, so a hand-edited file stays as short
            // as what the user actually configured.
            WriteIfSet(writer, "arguments", action.Arguments);
            writer.WriteString("glyph", action.Glyph);
            WriteIfSet(writer, "iconPath", action.IconPath);
            if (action.IconScale != ActionItem.DefaultIconScale) writer.WriteNumber("iconScale", action.IconScale);
            if (action.IconOffsetX != 0) writer.WriteNumber("iconOffsetX", action.IconOffsetX);
            if (action.IconOffsetY != 0) writer.WriteNumber("iconOffsetY", action.IconOffsetY);
            if (action.IconTint != ActionItem.DefaultIconTint) writer.WriteBoolean("iconTint", action.IconTint);
            WriteIfSet(writer, "foreground", action.Foreground);
            WriteIfSet(writer, "background", action.Background);
            WriteIfSet(writer, "shadow", action.Shadow);
            if (action.ShadowEnabled != ActionItem.DefaultShadowEnabled) writer.WriteBoolean("shadowEnabled", action.ShadowEnabled);
            if (action.ShadowOpacity != ActionItem.DefaultShadowOpacity) writer.WriteNumber("shadowOpacity", action.ShadowOpacity);
            if (action.ShadowBlur != ActionItem.DefaultShadowBlur) writer.WriteNumber("shadowBlur", action.ShadowBlur);
            if (action.ShadowOffsetX != ActionItem.DefaultShadowOffsetX) writer.WriteNumber("shadowOffsetX", action.ShadowOffsetX);
            if (action.ShadowOffsetY != ActionItem.DefaultShadowOffsetY) writer.WriteNumber("shadowOffsetY", action.ShadowOffsetY);

            if (action.HoldKind != ActionKind.None)
            {
                writer.WriteString("holdType", ActionValueCodec.KindToString(action.HoldKind));
                writer.WriteString("holdValue", action.HoldValue);
                WriteIfSet(writer, "holdArguments", action.HoldArguments);
            }

            if (action.Kind == ActionKind.Group && action.GroupChildren.Count > 0)
            {
                writer.WriteStartArray("group");
                foreach (var child in action.GroupChildren)
                    WriteAction(writer, child);
                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }

        private static void WriteIfSet(Utf8JsonWriter writer, string name, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                writer.WriteString(name, value);
        }

        private static void TryWriteDefaults()
        {
            try
            {
                var path = UserConfigPath;
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                if (!File.Exists(path))
                    AtomicFile.TryWriteText(path, ToJson(Defaults()), out _);
            }
            catch (Exception) { }
        }

        /// <summary>Built-in configuration, used when no actions.json is present.</summary>
        public static IReadOnlyList<ActionItem> Defaults() => new[]
        {
            // Glyph codes belong to the icon font the app ships, not to the system's - the two
            // disagree about most of this range. All nine are taken from the same drawing style so
            // the ring reads as one set; see IconFont for why the font moved.
            new ActionItem { Tag = 0, Label = "Close menu",    Kind = ActionKind.None,   Value = "",             Glyph = "F00D" },
            new ActionItem { Tag = 1, Label = "Copy",          Kind = ActionKind.Keys,   Value = "Ctrl+C",       Glyph = "F0C5" },
            new ActionItem { Tag = 2, Label = "Paste",         Kind = ActionKind.Keys,   Value = "Ctrl+V",       Glyph = "F0EA" },
            new ActionItem { Tag = 3, Label = "Cut",           Kind = ActionKind.Keys,   Value = "Ctrl+X",       Glyph = "F0C4" },
            new ActionItem { Tag = 4, Label = "Screenshot",    Kind = ActionKind.Keys,   Value = "Win+Shift+S",  Glyph = "F030" },
            new ActionItem { Tag = 5, Label = "Setting",       Kind = ActionKind.Function, Value = WindowsFunctions.ActionSettings, Glyph = "F013" },
            new ActionItem { Tag = 6, Label = "Task View",     Kind = ActionKind.Function, Value = WindowsFunctions.TaskView, Glyph = "F009" },
            new ActionItem { Tag = 7, Label = "File Explorer", Kind = ActionKind.Function, Value = WindowsFunctions.FileExplorer, Glyph = "F114" },
            new ActionItem { Tag = 8, Label = "Lock screen",   Kind = ActionKind.Function, Value = WindowsFunctions.Lock, Glyph = "F023" },
        };

    }
}
