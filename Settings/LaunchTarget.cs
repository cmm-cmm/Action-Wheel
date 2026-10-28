using System;
using System.IO;

namespace Action_Wheel.Settings
{
    /// <summary>How a row's value looks to the settings window's live checker.</summary>
    public enum LaunchTargetStatus
    {
        /// <summary>Nothing to say - the field is empty or the action type has no target.</summary>
        None,
        /// <summary>The target was found, or is a well-formed URL.</summary>
        Resolved,
        /// <summary>Nothing found. Probably a typo, but not necessarily wrong.</summary>
        Unresolved,
        /// <summary>Another button is bound to the same shortcut.</summary>
        Duplicate,
        /// <summary>The shortcut contains a key name that cannot be sent.</summary>
        Invalid,
    }

    /// <summary>
    /// Decides whether a "launch" value points at something that exists.
    /// </summary>
    /// <remarks>
    /// Deliberately advisory, never blocking. A value can be perfectly valid and still fail every
    /// check here - a program installed only on another machine, an app execution alias, a target
    /// that appears later - so an unresolved result is shown as a warning and the config still saves.
    /// </remarks>
    public static class LaunchTarget
    {
        public static LaunchTargetStatus Check(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return LaunchTargetStatus.None;

            var target = value.Trim().Trim('"');

            try
            {
                if (File.Exists(target) || Directory.Exists(target))
                    return LaunchTargetStatus.Resolved;

                // http, https, mailto, ms-settings: and friends - anything ShellExecute can follow.
                if (Uri.TryCreate(target, UriKind.Absolute, out var uri) && !uri.IsFile)
                    return LaunchTargetStatus.Resolved;

                if (Services.CommandResolver.Resolve(target) != null)
                    return LaunchTargetStatus.Resolved;
            }
            catch (Exception)
            {
                // Malformed paths throw from File.Exists on some inputs; that is a "no", not a crash.
                return LaunchTargetStatus.Unresolved;
            }

            return LaunchTargetStatus.Unresolved;
        }
    }
}
