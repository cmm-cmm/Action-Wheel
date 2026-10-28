using System;
using System.Collections.Generic;
using System.Text;

namespace Action_Wheel.Core
{
    /// <summary>
    /// Builds the text behind "Copy diagnostics": everything worth pasting into a bug report, in
    /// one clipboard's worth.
    /// </summary>
    /// <remarks>
    /// The facts are passed in rather than gathered here - the interesting ones (whether each hook
    /// installed, whether the tray icon was created) are only known to the app, and this assembly
    /// deliberately cannot reach user32 to find out.
    /// </remarks>
    public sealed class DiagnosticsReport
    {
        private readonly List<(string Name, string Value)> _facts = new();
        private readonly List<(string Title, string Body)> _sections = new();

        public DiagnosticsReport Add(string name, string? value)
        {
            _facts.Add((name, string.IsNullOrWhiteSpace(value) ? "(none)" : value.Trim()));
            return this;
        }

        public DiagnosticsReport Add(string name, bool value) => Add(name, value ? "yes" : "no");

        /// <summary>
        /// Adds a multi-line block after the one-line facts, such as a stack trace that would not
        /// survive being padded into the name/value column. Skipped when there is nothing to show.
        /// </summary>
        public DiagnosticsReport AddSection(string title, string? body)
        {
            if (!string.IsNullOrWhiteSpace(body))
                _sections.Add((title, body.Trim()));
            return this;
        }

        /// <summary>Adds the facts that are the same for every report.</summary>
        public DiagnosticsReport AddEnvironment()
        {
            var config = ActionConfig.LoadDetailed();

            return Add("Time", DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz"))
                .Add("OS", Environment.OSVersion.VersionString)
                .Add("Architecture", System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString())
                .Add("Process architecture", System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString())
                .Add(".NET", Environment.Version.ToString())
                .Add("Executable", Environment.ProcessPath)
                .Add("Config file", config.Path)
                .Add("Config status", config.Status.ToString())
                .Add("Config error", config.Error);
        }

        public string Build()
        {
            var text = new StringBuilder();
            text.AppendLine("Action Wheel diagnostics");
            text.AppendLine("=======================");

            int width = 0;
            foreach (var (name, _) in _facts)
                width = Math.Max(width, name.Length);

            foreach (var (name, value) in _facts)
                text.AppendLine($"{name.PadRight(width)} : {value}");

            foreach (var (title, body) in _sections)
            {
                text.AppendLine();
                text.AppendLine(title);
                text.AppendLine(new string('-', title.Length));
                text.AppendLine(body);
            }

            return text.ToString();
        }
    }
}
