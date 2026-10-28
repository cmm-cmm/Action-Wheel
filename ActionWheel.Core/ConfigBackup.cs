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
        public bool HasAiTasks { get; init; }
        public int ProfileCount { get; init; }
        public int IconCount { get; init; }

        /// <summary>
        /// Every program, file or URL the backup's buttons can start (see <see cref="LaunchSummary"/>),
        /// so the restore prompt can show it before anything is replaced.
        /// </summary>
        public IReadOnlyList<string> LaunchCommands { get; init; } = Array.Empty<string>();

        /// <summary>
        /// False when some of the backup's actions could not be read for the list above. They are
        /// too large or malformed, and the restore would then reject them anyway.
        /// </summary>
        public bool LaunchCommandsComplete { get; init; } = true;
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
        private const string AiTasksEntryName = AiTaskTemplates.FileName;
        private const string ProfilesEntryPrefix = "Profiles/";
        private const string IconsEntryPrefix = "Icons/";

        /// <summary>
        /// A real backup this app wrote has a handful of files: the four fixed ones, a few dozen
        /// profiles at most, and however many icons have been extracted from applications. None of
        /// these limits are close to that - they exist so an untrusted ZIP handed to
        /// <see cref="TryImport"/> cannot fill the staging directory (or the disk) before
        /// validation even gets a chance to reject it.
        /// </summary>
        private const int MaxEntryCount = 512;

        /// <summary>Bigger than any icon PNG or profile XML this app itself would ever write.</summary>
        private const long MaxEntryUncompressedBytes = 64L * 1024 * 1024;

        private const long MaxTotalUncompressedBytes = 512L * 1024 * 1024;

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

                    // The AI prompt templates are the user's own writing; the AI tool switches are
                    // not exported, because they describe what this machine has installed.
                    if (File.Exists(AiTaskTemplates.FilePath))
                        zip.CreateEntryFromFile(AiTaskTemplates.FilePath, AiTasksEntryName);

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

                var (launchCommands, launchCommandsComplete) = ReadLaunchCommands(zip);

                preview = new BackupPreview
                {
                    ManifestVersion = version,
                    ExportedAtUtc = exportedAt,
                    AppVersion = appVersion,
                    HasActions = zip.GetEntry(ActionsEntryName) != null,
                    HasPreferences = zip.GetEntry(PreferencesEntryName) != null,
                    HasActiveProfile = zip.GetEntry(ActiveProfileEntryName) != null,
                    HasAiTasks = zip.GetEntry(AiTasksEntryName) != null,
                    ProfileCount = zip.Entries.Count(e => IsUnder(e.FullName, ProfilesEntryPrefix) && e.Name.Length > 0),
                    IconCount = zip.Entries.Count(e => IsUnder(e.FullName, IconsEntryPrefix) && e.Name.Length > 0),
                    LaunchCommands = launchCommands,
                    LaunchCommandsComplete = launchCommandsComplete,
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
        /// Real actions.json and profile files are a few kilobytes. The preview reads them into
        /// memory, so it stops well short of the extraction limits TryImport applies.
        /// </summary>
        private const long MaxPreviewEntryBytes = 4L * 1024 * 1024;

        /// <summary>
        /// Reads actions.json and every profile straight out of the ZIP, in memory, and lists what
        /// their buttons launch. Nothing is extracted to disk: this runs before the user has agreed
        /// to anything. An entry that cannot be read only marks the list incomplete.
        /// </summary>
        private static (IReadOnlyList<string> Commands, bool Complete) ReadLaunchCommands(ZipArchive zip)
        {
            var actions = new List<ActionItem>();
            bool complete = true;

            var entries = zip.Entries
                .Where(e => e.FullName == ActionsEntryName
                    || (IsUnder(e.FullName, ProfilesEntryPrefix) && e.Name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)))
                .Take(MaxEntryCount);

            foreach (var entry in entries)
            {
                try
                {
                    byte[]? bytes = ReadBounded(entry, MaxPreviewEntryBytes);
                    if (bytes == null)
                    {
                        complete = false;
                        continue;
                    }

                    if (entry.FullName == ActionsEntryName)
                    {
                        actions.AddRange(ActionConfig.Parse(Encoding.UTF8.GetString(bytes)));
                    }
                    else
                    {
                        using var stream = new MemoryStream(bytes);
                        if (ProfileXml.TryLoad(stream, out var profileActions, out _))
                            actions.AddRange(profileActions);
                        else
                            complete = false;
                    }
                }
                catch (Exception)
                {
                    complete = false;
                }
            }

            return (LaunchSummary.Commands(actions), complete);
        }

        /// <summary>The entry's bytes, or null if it decompresses to more than <paramref name="maxBytes"/>.</summary>
        private static byte[]? ReadBounded(ZipArchiveEntry entry, long maxBytes)
        {
            if (entry.Length > maxBytes)
                return null;

            using var source = entry.Open();
            using var buffer = new MemoryStream();
            var chunk = new byte[81920];
            int read;
            while ((read = source.Read(chunk, 0, chunk.Length)) > 0)
            {
                if (buffer.Length + read > maxBytes)
                    return null;
                buffer.Write(chunk, 0, read);
            }

            return buffer.ToArray();
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
                    if (zip.Entries.Count > MaxEntryCount)
                    {
                        error = $"This backup has {zip.Entries.Count} entries, more than a real Action Wheel backup ever has.";
                        return false;
                    }

                    long totalUncompressed = 0;

                    foreach (var entry in zip.Entries)
                    {
                        if (entry.Name.Length == 0 || entry.FullName == ManifestEntryName)
                            continue; // A directory entry, or the manifest - already read by TryPreview.

                        if (!IsKnownBackupEntry(entry.FullName))
                        {
                            error = $"The backup contains an entry '{entry.FullName}' that is not part of the Action Wheel backup format.";
                            return false;
                        }

                        // entry.Length comes from the ZIP's central directory and is not itself
                        // trustworthy - it is exactly what a hand-crafted zip bomb would lie about.
                        // It is still worth checking up front (cheap, no decompression needed) so an
                        // honestly-labelled oversized entry is rejected without extracting a single
                        // byte of it; ExtractBounded below is what catches a header that lies.
                        if (entry.Length > MaxEntryUncompressedBytes)
                        {
                            error = $"The backup entry '{entry.FullName}' is larger than a real Action Wheel backup ever has.";
                            return false;
                        }

                        totalUncompressed += entry.Length;
                        if (totalUncompressed > MaxTotalUncompressedBytes)
                        {
                            error = "This backup's total uncompressed size is larger than a real Action Wheel backup ever has.";
                            return false;
                        }

                        if (!TryGetSafeDestination(staging, entry.FullName, out string destination))
                        {
                            error = $"The backup contains an unsafe entry path '{entry.FullName}'.";
                            return false;
                        }

                        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                        ExtractBounded(entry, destination, MaxEntryUncompressedBytes);
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

                if (preview.HasAiTasks)
                {
                    try { AiTaskTemplates.Parse(File.ReadAllText(Path.Combine(staging, AiTasksEntryName))); }
                    catch (Exception ex)
                    {
                        error = $"The backup's {AiTasksEntryName} is not valid: {ex.Message}";
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
                //
                // The four kinds of target here don't share one atomic-write primitive -
                // ActionConfig.SaveTo and AtomicFile.TryWriteText each keep their own .bak,
                // ProfileXml.Save keeps none - so without help, a failure partway through (say,
                // preferences.json after actions.json already landed) would leave the restore
                // half-applied with no way back, silently. The snapshot below, put back byte for
                // byte in the finally block unless every one of these commits succeeds, is what
                // makes the four behave as one operation instead of four independent ones.
                var targetPaths = new List<string>();
                if (actions != null)
                    targetPaths.Add(ActionConfig.ActiveConfigPath);
                if (preview.HasPreferences)
                    targetPaths.Add(PreferencesPath);
                if (preview.HasActiveProfile)
                    targetPaths.Add(ActiveProfilePath);
                if (preview.HasAiTasks)
                    targetPaths.Add(AiTaskTemplates.FilePath);
                foreach (var (name, _) in stagedProfiles)
                    targetPaths.Add(Path.Combine(ProfilesDirectory, SafeFileName(name) + ".xml"));

                var restorePoint = targetPaths
                    .Select(path => (Path: path, Bytes: File.Exists(path) ? File.ReadAllBytes(path) : null))
                    .ToList();

                bool committed = false;
                try
                {
                    if (actions != null && !ActionConfig.SaveTo(ActionConfig.ActiveConfigPath, actions, out error))
                        return false;

                    if (preview.HasPreferences
                        && !AtomicFile.TryWriteText(PreferencesPath, File.ReadAllText(Path.Combine(staging, PreferencesEntryName)), out error))
                        return false;

                    if (preview.HasActiveProfile
                        && !AtomicFile.TryWriteText(ActiveProfilePath, File.ReadAllText(Path.Combine(staging, ActiveProfileEntryName)), out error))
                        return false;

                    if (preview.HasAiTasks
                        && !AtomicFile.TryWriteText(AiTaskTemplates.FilePath, File.ReadAllText(Path.Combine(staging, AiTasksEntryName)), out error))
                        return false;

                    foreach (var (name, profileActions) in stagedProfiles)
                    {
                        string destination = Path.Combine(ProfilesDirectory, SafeFileName(name) + ".xml");
                        if (!ProfileXml.Save(destination, profileActions, out error))
                            return false;
                    }

                    committed = true;
                }
                finally
                {
                    if (!committed)
                    {
                        string rollbackError = RestorePreviousFiles(restorePoint);
                        if (!string.IsNullOrEmpty(rollbackError))
                            error = $"{error} It also could not fully undo the files it had already changed: {rollbackError}";
                    }
                }

                // Icons are regenerable (re-extracted from the source application the next time it is
                // browsed to) and purely cosmetic, so a copy failure here is not worth rejecting the
                // rest of an otherwise-valid restore over. Deliberately outside the try/finally above:
                // a rolled-back restore should not leave stray icons behind either.
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

        /// <summary>
        /// Puts every path in <paramref name="restorePoint"/> back to what it held before the
        /// commit started - deleted if it did not exist yet, overwritten byte for byte if it did.
        /// Best-effort and does not stop at the first failure: every path is attempted and every
        /// failure collected, since silently leaving some files rolled back and others not is
        /// exactly the half-applied state this method exists to avoid.
        /// </summary>
        private static string RestorePreviousFiles(IReadOnlyList<(string Path, byte[]? Bytes)> restorePoint)
        {
            var failures = new List<string>();

            foreach (var (path, bytes) in restorePoint)
            {
                try
                {
                    if (bytes == null)
                    {
                        if (File.Exists(path))
                            File.Delete(path);
                    }
                    else
                    {
                        File.WriteAllBytes(path, bytes);
                    }
                }
                catch (Exception ex)
                {
                    failures.Add($"{path}: {ex.Message}");
                }
            }

            return failures.Count == 0 ? string.Empty : string.Join("; ", failures);
        }

        /// <summary>
        /// Whether an entry name is one <see cref="TryExport"/> could actually have written - the
        /// four fixed files, a profile XML, or an extracted icon PNG. Everything else is rejected
        /// outright rather than silently extracted into the staging directory and then ignored,
        /// which is what an unrecognised entry name used to be.
        /// </summary>
        private static bool IsKnownBackupEntry(string entryFullName)
        {
            string normalized = entryFullName.Replace('\\', '/');

            if (normalized is ActionsEntryName or PreferencesEntryName or ActiveProfileEntryName or AiTasksEntryName)
                return true;

            if (IsUnder(normalized, ProfilesEntryPrefix) && normalized.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                return true;

            if (IsUnder(normalized, IconsEntryPrefix) && normalized.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        /// <summary>
        /// <see cref="ZipArchiveEntry.ExtractToFile(string, bool)"/> writes however many bytes the
        /// compressed stream actually decompresses to, regardless of what <c>entry.Length</c>
        /// claimed in the central directory - a crafted entry can under-report its own size. This
        /// copies by hand and aborts the moment the real byte count passes <paramref name="maxBytes"/>,
        /// so a lying header cannot buy its way past the size check already done against the
        /// (possibly false) declared length.
        /// </summary>
        private static void ExtractBounded(ZipArchiveEntry entry, string destination, long maxBytes)
        {
            using var source = entry.Open();
            using var destinationStream = new FileStream(destination, FileMode.Create, FileAccess.Write);

            var buffer = new byte[81920];
            long total = 0;
            int read;
            while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
            {
                total += read;
                if (total > maxBytes)
                    throw new InvalidDataException($"Entry '{entry.FullName}' decompressed past its declared size.");

                destinationStream.Write(buffer, 0, read);
            }
        }

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
