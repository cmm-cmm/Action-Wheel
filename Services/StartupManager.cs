using System;
using Microsoft.Win32;

namespace Action_Wheel.Services
{
    /// <summary>
    /// Starts the app automatically when the user signs in.
    /// </summary>
    /// <remarks>
    /// Uses the per-user Run key rather than a scheduled task or the all-users key: it needs no
    /// elevation, so the toggle works for anyone running the app, and Windows' own Startup Apps
    /// settings lists and can disable it like any other entry.
    ///
    /// Nothing here throws. The registry can be locked down by policy, and failing to toggle a
    /// convenience feature must not take the app down or block the settings UI.
    /// </remarks>
    public static class StartupManager
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "Action Wheel";

        /// <summary>True when the Run entry exists and points at this executable.</summary>
        public static bool IsEnabled
        {
            get
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(RunKey);
                    if (key?.GetValue(ValueName) is not string command)
                        return false;

                    return StartupCommand.Matches(command, Environment.ProcessPath);
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        /// <summary>Adds or removes the Run entry. Returns false if the registry refused.</summary>
        public static bool SetEnabled(bool enabled, out string error)
        {
            error = string.Empty;

            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
                if (key == null)
                {
                    error = "The startup registry key could not be opened.";
                    return false;
                }

                if (enabled)
                    key.SetValue(ValueName, CommandLine, RegistryValueKind.String);
                else
                    key.DeleteValue(ValueName, throwOnMissingValue: false);

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        /// <summary>The quoted path Windows should run. Quoted because the path may contain spaces.</summary>
        private static string CommandLine => StartupCommand.For(Environment.ProcessPath);
    }
}
