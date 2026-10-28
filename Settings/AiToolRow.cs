using System;
using Action_Wheel.Services;
using Action_Wheel.ViewModels;

namespace Action_Wheel.Settings
{
    /// <summary>One AI tool in the settings window's AI assistants section.</summary>
    public sealed class AiToolRow : ObservableObject
    {
        private readonly Func<bool, string?> _applyEnabled;
        private bool _isEnabled;

        /// <param name="applyEnabled">Saves the switch. Returns null on success, or the error.</param>
        public AiToolRow(AiToolDetection detection, bool isEnabled, Func<bool, string?> applyEnabled)
        {
            Detection = detection;
            _isEnabled = isEnabled;
            _applyEnabled = applyEnabled;
        }

        public AiToolDetection Detection { get; }

        public string Name => Detection.Tool.Name;

        public string KindText => Detection.Tool.Kind == AiToolKind.Cli
            ? "Command-line tool, opens in a terminal"
            : Detection.Tool.OpensFolder ? "Editor, opens the current folder" : "Desktop app";

        public bool IsInstalled => Detection.IsInstalled;

        public string StatusText => Detection.State switch
        {
            AiToolState.Installed => "Installed",
            AiToolState.Stale => "Registered, but its program is missing",
            AiToolState.LauncherOnly => "Not installed, only a launcher that would install it",
            _ => "Not found",
        };

        /// <summary>Check, warning triangle or cross, from the shipped icon font.</summary>
        public string StatusGlyph => Detection.State switch
        {
            AiToolState.Installed => "",
            AiToolState.Stale or AiToolState.LauncherOnly => "",
            _ => "",
        };

        public string DetailText => Detection.State switch
        {
            AiToolState.Installed => $"{Detection.Path} (found through {Detection.Source})",
            AiToolState.Stale => $"A {Detection.Source} still points at {Detection.Path}, which no longer exists. Reinstall it, or ignore this.",
            AiToolState.LauncherOnly => $"{Detection.Path} is a launcher that installs {Detection.Tool.Name} the first time it runs, so it is not offered. Install {Detection.Tool.Name}, then press Scan again.",
            _ => "Install it, then press Scan again.",
        };

        /// <summary>
        /// Bound two-way to the row's switch. Saved immediately, like Start with Windows: it says
        /// which tools this machine offers, not what a button does, so it is not part of Save/Undo.
        /// </summary>
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled == value)
                    return;

                if (_applyEnabled(value) is string error && error.Length > 0)
                {
                    // Raised without changing the field, so the switch snaps back to what is saved.
                    Raise(nameof(IsEnabled));
                    return;
                }

                Set(ref _isEnabled, value);
            }
        }
    }
}
