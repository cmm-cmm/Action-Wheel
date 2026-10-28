using System;
using System.Collections.Generic;

namespace Action_Wheel.Core
{
    /// <summary>
    /// Lists what a set of actions can start: every program, file or URL a button launches, as its
    /// primary or hold action, a group child included.
    /// </summary>
    /// <remarks>
    /// Shown before a profile or backup from somewhere else replaces anything. A launch target
    /// can be any command line, such as powershell.exe with a script in its arguments, and one
    /// click on the ring runs it. A count of profiles and icons said nothing about that.
    /// </remarks>
    public static class LaunchSummary
    {
        /// <summary>Each distinct "target arguments" line, in the order the buttons list them.</summary>
        public static IReadOnlyList<string> Commands(IEnumerable<ActionItem> actions)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var commands = new List<string>();

            void Add(ActionKind kind, string value, string arguments)
            {
                if (kind != ActionKind.Launch || string.IsNullOrWhiteSpace(value))
                    return;

                string line = string.IsNullOrWhiteSpace(arguments)
                    ? value.Trim()
                    : $"{value.Trim()} {arguments.Trim()}";
                if (seen.Add(line))
                    commands.Add(line);
            }

            void Visit(IEnumerable<ActionItem> items)
            {
                foreach (var action in items)
                {
                    Add(action.Kind, action.Value, action.Arguments);
                    Add(action.HoldKind, action.HoldValue, action.HoldArguments);
                    Visit(action.GroupChildren);
                }
            }

            Visit(actions);
            return commands;
        }
    }
}
