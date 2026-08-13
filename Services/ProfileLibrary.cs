using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Action_Wheel.Services
{
    /// <summary>Owns the saved-profile directory and all fallible filesystem operations on it.</summary>
    public sealed class ProfileLibrary
    {
        public string DirectoryPath { get; } = Path.Combine(AppDataPaths.DirectoryPath, "Profiles");

        public bool TryList(out IReadOnlyList<string> names, out string error)
        {
            try
            {
                Directory.CreateDirectory(DirectoryPath);
                names = Directory.EnumerateFiles(DirectoryPath, "*.xml")
                    .Select(path => Path.GetFileNameWithoutExtension(path) ?? string.Empty)
                    .Where(name => name.Length > 0)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                error = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                names = Array.Empty<string>();
                error = ex.Message;
                return false;
            }
        }

        public string PathFor(string name) => Path.Combine(DirectoryPath, SafeName(name) + ".xml");

        /// <summary>
        /// Profile names ordered most-recently-activated first, for the tray icon's quick-switch
        /// submenu - "recent" without a separate history file, since <see cref="TryTouch"/> already
        /// bumps a profile's own write time to now every time it becomes the active one.
        /// </summary>
        public bool TryListByRecency(int max, out IReadOnlyList<string> names, out string error)
        {
            try
            {
                Directory.CreateDirectory(DirectoryPath);
                names = new DirectoryInfo(DirectoryPath).EnumerateFiles("*.xml")
                    .OrderByDescending(file => file.LastWriteTimeUtc)
                    .Take(max)
                    .Select(file => Path.GetFileNameWithoutExtension(file.Name))
                    .Where(name => name.Length > 0)
                    .ToArray();
                error = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                names = Array.Empty<string>();
                error = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Marks a profile as just activated by bumping its write time to now, without touching its
        /// content. Best-effort and silent: this is metadata for a menu ordering, not something
        /// worth failing a profile switch over.
        /// </summary>
        public void TryTouch(string name)
        {
            try { File.SetLastWriteTimeUtc(PathFor(name), DateTime.UtcNow); }
            catch (Exception) { }
        }

        public bool TryLoad(string name, out IReadOnlyList<ActionItem> actions, out string error) =>
            ProfileXml.TryLoad(PathFor(name), out actions, out error);

        public bool TryImport(string path, out IReadOnlyList<ActionItem> actions, out string error) =>
            ProfileXml.TryLoad(path, out actions, out error);

        public bool TryExport(string path, IReadOnlyList<ActionItem> actions, out string error) =>
            ProfileXml.Save(path, actions, out error);

        public static string NameFromPath(string path) => Path.GetFileNameWithoutExtension(path);

        public bool TrySave(string name, IReadOnlyList<ActionItem> actions, out string error)
        {
            string safe = SafeName(name);
            if (string.IsNullOrWhiteSpace(safe))
            {
                error = "Enter a valid profile name.";
                return false;
            }
            return ProfileXml.Save(PathFor(safe), actions, out error);
        }

        public bool TryDelete(string name, out string error)
        {
            try { File.Delete(PathFor(name)); error = string.Empty; return true; }
            catch (Exception ex) { error = ex.Message; return false; }
        }

        public bool TryRename(string oldName, string newName, out string error)
        {
            string safe = SafeName(newName);
            if (string.IsNullOrWhiteSpace(safe))
            {
                error = "Enter a valid new profile name.";
                return false;
            }

            try
            {
                string destination = PathFor(safe);
                if (File.Exists(destination))
                {
                    error = "A profile with that name already exists.";
                    return false;
                }
                File.Move(PathFor(oldName), destination);
                error = string.Empty;
                return true;
            }
            catch (Exception ex) { error = ex.Message; return false; }
        }

        public static string SafeName(string value) => string.Join("_",
            (value ?? string.Empty).Trim().Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
    }
}
