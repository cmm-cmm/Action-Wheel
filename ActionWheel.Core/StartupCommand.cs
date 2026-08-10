using System;

namespace Action_Wheel.Core
{
    /// <summary>
    /// The command line Windows should run for "Start with Windows".
    /// </summary>
    /// <remarks>
    /// Separate from the registry code that stores it because the string is the part that can be
    /// wrong in a way nobody notices: an unquoted path containing a space makes Windows try to run
    /// "D:\03" with the rest as arguments, and the only symptom is that the app quietly does not
    /// start after a reboot.
    /// </remarks>
    public static class StartupCommand
    {
        /// <summary>The value to store: the executable path, always quoted.</summary>
        public static string For(string? executablePath) => $"\"{executablePath}\"";

        /// <summary>
        /// True when a stored Run entry refers to this executable. A stale entry from an older
        /// install location is worse than no entry - it silently starts a copy of the app the user
        /// has since moved or replaced - so anything else counts as "not enabled".
        /// </summary>
        public static bool Matches(string? storedCommand, string? executablePath)
        {
            if (string.IsNullOrWhiteSpace(storedCommand))
                return false;

            if (string.Equals(storedCommand, For(executablePath), StringComparison.OrdinalIgnoreCase))
                return true;

            // Tolerate an entry written without quotes, as long as the path itself has no space -
            // in that form it is still unambiguous and still starts the right executable.
            return !string.IsNullOrWhiteSpace(executablePath)
                && executablePath.IndexOf(' ') < 0
                && string.Equals(storedCommand.Trim(), executablePath, StringComparison.OrdinalIgnoreCase);
        }
    }
}
