using System;
using System.IO;

namespace Action_Wheel.Services
{
    /// <summary>
    /// Persists whether the app starts with only its tray icon, leaving the main window hidden
    /// until the user opens it from the tray.
    /// </summary>
    /// <remarks>
    /// Its own file rather than a field in preferences.json: <see cref="TriggerSettings.Save"/>
    /// rewrites that whole file from what the settings window holds, and the settings window knows
    /// nothing about this, so a trigger save would quietly turn it back off. Like the Run key behind
    /// "Start with Windows", it describes this machine's startup, so it is not part of a backup.
    /// </remarks>
    public static class StartHiddenSettings
    {
        private static string FilePath => Path.Combine(AppDataPaths.DirectoryPath, "start-hidden.txt");

        /// <summary>False when the file is missing or unreadable: showing the window is the safe default.</summary>
        public static bool Load()
        {
            try
            {
                return File.Exists(FilePath)
                    && bool.TryParse(File.ReadAllText(FilePath).Trim(), out bool enabled)
                    && enabled;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool Save(bool enabled, out string error) =>
            AtomicFile.TryWriteText(FilePath, enabled ? "true" : "false", out error);
    }
}
