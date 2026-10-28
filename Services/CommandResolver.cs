using System;
using System.IO;
using Microsoft.Win32;

namespace Action_Wheel.Services
{
    /// <summary>
    /// Finds the file a bare command such as "notepad.exe" or "chrome.exe" starts, the way
    /// ShellExecute finds it.
    /// </summary>
    /// <remarks>
    /// PATH alone is not enough. Chrome, Edge, Office and VS Code register themselves under
    /// "App Paths" instead, and ShellExecute checks that too. The settings window's target checker
    /// and the app-icon extractor each used to walk PATH on their own. So "chrome.exe" launched
    /// fine, but Settings called it missing and could not take its icon.
    /// </remarks>
    public static class CommandResolver
    {
        private const string AppPathsKey = @"Software\Microsoft\Windows\CurrentVersion\App Paths";

        /// <summary>
        /// The full path <paramref name="command"/> resolves to, or null. Only bare names are
        /// resolved; anything with a directory in it is the caller's to check directly.
        /// </summary>
        public static string? Resolve(string command)
        {
            if (string.IsNullOrWhiteSpace(command)
                || Path.IsPathRooted(command) || command.Contains(Path.DirectorySeparatorChar))
                return null;

            return OnPath(command) ?? InAppPaths(command);
        }

        private static string? OnPath(string command)
        {
            var directories = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(directories))
                return null;

            // A bare name may omit the extension, exactly as it may on a command line.
            var extensions = string.IsNullOrEmpty(Path.GetExtension(command))
                ? new[] { ".exe", ".com", ".bat", ".cmd" }
                : new[] { string.Empty };

            foreach (var directory in directories.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                foreach (var extension in extensions)
                {
                    try
                    {
                        string candidate = Path.Combine(directory.Trim(), command + extension);
                        if (File.Exists(candidate))
                            return candidate;
                    }
                    catch (ArgumentException)
                    {
                        // A malformed PATH entry - skip it rather than give up on the rest.
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// The per-user entry wins over the machine-wide one, the same order the shell uses.
        /// Entries are keyed by file name with its extension, so a bare "chrome" is looked up as
        /// "chrome.exe".
        /// </summary>
        private static string? InAppPaths(string command)
        {
            string name = string.IsNullOrEmpty(Path.GetExtension(command)) ? command + ".exe" : command;

            foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
            {
                try
                {
                    using var key = hive.OpenSubKey($@"{AppPathsKey}\{name}");
                    if (key?.GetValue(null) is not string value || string.IsNullOrWhiteSpace(value))
                        continue;

                    string path = Environment.ExpandEnvironmentVariables(value.Trim().Trim('"'));
                    if (File.Exists(path))
                        return path;
                }
                catch (Exception)
                {
                    // Policy can lock the key down; an unreadable entry is just "not found here".
                }
            }

            return null;
        }
    }
}
