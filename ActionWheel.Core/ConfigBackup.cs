using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Action_Wheel.Core
{
    /// <summary>What <see cref="ConfigBackup.TryPreview"/> found in a backup, without extracting it.</summary>
    public sealed class BackupPreview
    {
        public required int ManifestVersion { get; init; }
        public required DateTime ExportedAtUtc { get; init; }
        public string AppVersion { get; init; } = string.Empty;
        public bool HasActions { get; init; }
        public bool HasPreferences { get; init; }
        public bool HasActiveProfile { get; init; }
        public int ProfileCount { get; init; }
        public int IconCount { get; init; }
    }

    /// <summary>
    /// Exports and imports everything that makes up a person's setup - not just the nine buttons - as
    /// one ZIP: the active <c>actions.json</c>, <c>preferences.json</c> (trigger and ring appearance),
    /// <c>active-profile.txt</c>, every saved profile, and every icon extracted from an application.
    /// </summary>
    /// <remarks>
    /// A single ZIP with a version-tagged manifest rather than copying the whole
    /// <c>%LOCALAPPDATA%\ActionWheel</c> folder: a folder copy carries no statement of what it is or
    /// whether this build can read it back, and restoring one by overwriting files in place gives no
    /// chance to look before leaping. <see cref="TryPreview"/> exists so the settings window can show
    /// what a backup contains - how many profiles, how many icons, when it was made - before
    /// <see cref="TryImport"/> touches anything real.
    /// </remarks>
    public static class ConfigBackup
    {
        private const string ManifestEntryName = "manifest.json";
        private const string ActionsEntryName = "actions.json";
        private const string PreferencesEntryName = "preferences.json";
        private const string ActiveProfileEntryName = "active-profile.txt";
        private const string ProfilesEntryPrefix = "Profiles/";
        private const string IconsEntryPrefix = "Icons/";

        /// <summary>
        /// The manifest shape a reader has to understand. Bumped only if a future change removes or
        /// repurposes a field an older reader would misinterpret - adding an optional field, the way
        /// every other config format in this app evolves, does not need a bump.
        /// </summary>
        private const int ManifestVersion = 1;

        private static string PreferencesPath => Path.Combine(AppDataPaths.DirectoryPath, "preferences.json");
        private static string ActiveProfilePath => Path.Combine(AppDataPaths.DirectoryPath, "active-profile.txt");
        private static string ProfilesDirectory => Path.Combine(AppDataPaths.DirectoryPath, "Profiles");

        public static bool TryExport(string zipPath, string appVersion, out string error)
        {
            error = string.Empty;
            string? temporaryPath = null;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(zipPath))!);
                temporaryPath = zipPath + "." + Guid.NewGuid().ToString("N") + ".tmp";

                using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write))
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
                {
                    WriteManifest(zip, appVersion);

                    string actionsPath = ActionConfig.ActiveConfigPath;
                    if (File.Exists(actionsPath))
                        zip.CreateEntryFromFile(actionsPath, ActionsEntryName);

                    if (File.Exists(PreferencesPath))
                        zip.CreateEntryFromFile(PreferencesPath, PreferencesEntryName);

                    if (File.Exists(ActiveProfilePath))
                        zip.CreateEntryFromFile(ActiveProfilePath, ActiveProfileEntryName);

                    if (Directory.Exists(ProfilesDirectory))
                    {
                        foreach (var file in Directory.EnumerateFiles(ProfilesDirectory, "*.xml"))
                            zip.CreateEntryFromFile(file, ProfilesEntryPrefix + Path.GetFileName(file));
                    }

                    if (Directory.Exists(AppDataPaths.IconsDirectoryPath))
                    {
                        foreach (var file in Directory.EnumerateFiles(AppDataPaths.IconsDirectoryPath, "*.png"))
                            zip.CreateEntryFromFile(file, IconsEntryPrefix + Path.GetFileName(file));
                    }
                }

                File.Move(temporaryPath, zipPath, overwrite: true);
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
                    try { File.Delete(temporaryPath); } catch { }
                }
            }
        }

        private static void WriteManifest(ZipArchive zip, string appVersion)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();
                writer.WriteNumber("manifestVersion", ManifestVersion);
                writer.WriteString("exportedAtUtc", DateTime.UtcNow.ToString("O"));
                writer.WriteString("appVersion", appVersion ?? string.Empty);
                writer.WriteEndObject();
            }

            var entry = zip.CreateEntry(ManifestEntryName);
            using var entryStream = entry.Open();
            stream.Position = 0;
            stream.CopyTo(entryStream);
        }

        public static bool TryPreview(string zipPath, out BackupPreview? preview, out string error)
        {
            preview = null;
            error = string.Empty;

            try
            {
                using var stream = new FileStream(zipPath, FileMode.Open, FileAccess.Read);
                using var zip = new ZipArchive(stream, ZipArchiveMode.Read);

                var manifestEntry = zip.GetEntry(ManifestEntryName);
                if (manifestEntry == null)
                {
                    error = "This file has no Action Wheel backup manifest.";
                    return false;
                }

                using var manifestStream = manifestEntry.Open();
                using var document = JsonDocument.Parse(manifestStream);
                var root = document.RootElement;

                int version = root.TryGetProperty("manifestVersion", out var versionProp) && versionProp.TryGetInt32(out int v) ? v : 0;
                if (version > ManifestVersion)
                {
                    error = $"This backup was made by a newer version of Action Wheel (manifest version {version}). Update the app before restoring it.";
                    return false;
                }

                DateTime exportedAt = root.TryGetProperty("exportedAtUtc", out var dateProp)
                    && DateTime.TryParse(dateProp.GetString(), null,
                        System.Globalization.DateTimeStyles.RoundtripKind, out var parsed)
                    ? parsed : DateTime.MinValue;

                string appVersion = root.TryGetProperty("appVersion", out var appVersionProp)
                    ? appVersionProp.GetString() ?? string.Empty : string.Empty;

                preview = new BackupPreview
                {
                    ManifestVersion = version,
                    ExportedAtUtc = exportedAt,
                    AppVersion = appVersion,
                    HasActions = zip.GetEntry(ActionsEntryName) != null,
                    HasPreferences = zip.GetEntry(PreferencesEntryName) != null,
                    HasActiveProfile = zip.GetEntry(ActiveProfileEntryName) != null,
                    ProfileCount = zip.Entries.Count(e => IsUnder(e.FullName, ProfilesEntryPrefix) && e.Name.Length > 0),
                    IconCount = zip.Entries.Count(e => IsUnder(e.FullName, IconsEntryPrefix) && e.Name.Length > 0),
                };
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Restores everything a backup contains. Every file is validated in a temp staging directory
        /// first - the same "parse and check before it can overwrite anything real" rule
        /// <see cref="ActionConfig.LoadDetailed"/> and <see cref="ProfileXml.TryLoad"/> already apply
        /// on their own - so a truncated download or a hand-edited backup is rejected with nothing on
        /// disk touched, rather than partially applied.
        /// </summary>
        public static bool TryImport(string zipPath, out string error)
        {
            if (!TryPreview(zipPath, out var preview, out error) || preview == null)
                return false;

            string staging = Path.Combine(Path.GetTempPath(), "ActionWheelRestore-" + Guid.NewGuid().ToString("N"));

            try
            {
                Directory.CreateDirectory(staging);

                using (var stream = new FileStream(zipPath, FileMode.Open, FileAccess.Read))
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
                {
                    foreach (var entry in zip.Entries)
                    {
                        if (entry.Name.Length == 0 || entry.FullName == ManifestEntryName)
                            continue; // A directory entry, or the manifest - already read by TryPreview.

                        if (!TryGetSafeDestination(staging, entry.FullName, out string destination))
                        {
                            error = $"The backup contains an unsafe entry path '{entry.FullName}'.";
                            return false;
                        }

                        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                        entry.ExtractToFile(destination, overwrite: true);
                    }
                }

                // Validate before committing anything - see the remarks above.
                IReadOnlyList<ActionItem>? actions = null;
                if (preview.HasActions)
                {
                    string actionsPath = Path.Combine(staging, ActionsEntryName);
                    var parsed = ActionConfig.Parse(File.ReadAllText(actionsPath));
                    if (!ActionConfig.TryValidate(parsed, out error))
                    {
                        error = $"The backup's actions.json is not valid: {error}";
                        return false;
                    }
                    actions = parsed;
                }

                if (preview.HasPreferences)
                {
                    try { JsonDocument.Parse(File.ReadAllText(Path.Combine(staging, PreferencesEntryName))).Dispose(); }
                    catch (Exception ex)
                    {
                        error = $"The backup's preferences.json is not valid: {ex.Message}";
                        return false;
                    }
                }

                var stagedProfiles = new List<(string Name, IReadOnlyList<ActionItem> Actions)>();
                string stagedProfilesDir = Path.Combine(staging, "Profiles");
                if (Directory.Exists(stagedProfilesDir))
                {
                    foreach (var file in Directory.EnumerateFiles(stagedProfilesDir, "*.xml"))
                    {
                        if (!ProfileXml.TryLoad(file, out var profileActions, out error))
                        {
                            error = $"The backup's profile '{Path.GetFileNameWithoutExtension(file)}' is not valid: {error}";
                            return false;
                        }
                        stagedProfiles.Add((Path.GetFileNameWithoutExtension(file), profileActions));
                    }
                }

                // Everything validated - now commit, each through the same write path the app already
                // trusts for that file, so a restore is indistinguishable from the user having set
                // everything up by hand.
                if (actions != null && !ActionConfig.SaveTo(ActionConfig.ActiveConfigPath, actions, out error))
                    return false;

                if (preview.HasPreferences
                    && !AtomicFile.TryWriteText(PreferencesPath, File.ReadAllText(Path.Combine(staging, PreferencesEntryName)), out error))
                    return false;

                if (preview.HasActiveProfile
                    && !AtomicFile.TryWriteText(ActiveProfilePath, File.ReadAllText(Path.Combine(staging, ActiveProfileEntryName)), out error))
                    return false;

                foreach (var (name, profileActions) in stagedProfiles)
                {
                    string destination = Path.Combine(ProfilesDirectory, SafeFileName(name) + ".xml");
                    if (!ProfileXml.Save(destination, profileActions, out error))
                        return false;
                }

                // Icons are regenerable (re-extracted from the source application the next time it is
                // browsed to) and purely cosmetic, so a copy failure here is not worth rejecting the
                // rest of an otherwise-valid restore over.
                string stagedIconsDir = Path.Combine(staging, "Icons");
                if (Directory.Exists(stagedIconsDir))
                {
                    Directory.CreateDirectory(AppDataPaths.IconsDirectoryPath);
                    foreach (var file in Directory.EnumerateFiles(stagedIconsDir, "*.png"))
                    {
                        try { File.Copy(file, Path.Combine(AppDataPaths.IconsDirectoryPath, Path.GetFileName(file)), overwrite: true); }
                        catch (Exception) { }
                    }
                }

                error = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
            finally
            {
                try { Directory.Delete(staging, recursive: true); } catch { }
            }
        }

        /// <summary>
        /// Mirrors <c>Services.ProfileLibrary.SafeName</c> (the app project, which this one cannot
        /// reference). Both exist because a profile name is free text the settings window lets
        /// someone type, and it becomes a file name either way - here, on the way back in.
        /// </summary>
        private static string SafeFileName(string value) => string.Join("_",
            (value ?? string.Empty).Trim().Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));

        private static bool IsUnder(string entryFullName, string prefix) =>
            entryFullName.Replace('\\', '/').StartsWith(prefix, StringComparison.Ordinal);

        /// <summary>
        /// Resolves a ZIP entry's path against <paramref name="root"/> and rejects it - "zip slip" -
        /// if the result would land outside that directory. <see cref="ZipArchiveEntry.ExtractToFile"/>
        /// does none of this itself; a crafted entry named e.g. <c>..\..\..\AppData\...</c> would
        /// otherwise write wherever its author chose, not into the staging directory this method call
        /// is supposed to be confined to.
        /// </summary>
        private static bool TryGetSafeDestination(string root, string entryFullName, out string destination)
        {
            string normalizedRoot = Path.GetFullPath(root);
            string candidate = Path.GetFullPath(Path.Combine(normalizedRoot, entryFullName.Replace('/', Path.DirectorySeparatorChar)));

            if (!candidate.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                destination = string.Empty;
                return false;
            }

            destination = candidate;
            return true;
        }
    }
}
