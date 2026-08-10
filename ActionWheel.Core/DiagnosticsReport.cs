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

        public DiagnosticsReport Add(string name, string? value)
        {
            _facts.Add((name, string.IsNullOrWhiteSpace(value) ? "(none)" : value.Trim()));
            return this;
        }

        public DiagnosticsReport Add(string name, bool value) => Add(name, value ? "yes" : "no");

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

            return text.ToString();
        }
    }
}
