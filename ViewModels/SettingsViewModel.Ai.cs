using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Action_Wheel.Services;
using Action_Wheel.Settings;
using InfoBarSeverity = Action_Wheel.ViewModels.StatusLevel;

namespace Action_Wheel.ViewModels
{
    /// <summary>
    /// The AI assistants section: which AI tools this machine has, and which of them the function
    /// picker offers. See Doc/ai-assistants.md.
    /// </summary>
    public sealed partial class SettingsViewModel
    {
        private IReadOnlyList<AiToolDetection> _aiDetections = Array.Empty<AiToolDetection>();
        private HashSet<string> _aiDisabled = new(StringComparer.OrdinalIgnoreCase);
        private string _aiScanStatus = "Looking for AI tools…";
        private RelayCommand? _rescanAiToolsCommand;

        public ObservableCollection<AiToolRow> AiToolRows { get; } = new();

        public string AiScanStatus
        {
            get => _aiScanStatus;
            private set => Set(ref _aiScanStatus, value);
        }

        public RelayCommand RescanAiToolsCommand => _rescanAiToolsCommand ??= new RelayCommand(ScanAiTools);

        /// <summary>
        /// Looks for the tools off the UI thread, then fills the section and the function picker.
        /// Until it finishes, the picker simply has no AI entries; nothing waits on it.
        /// </summary>
        private async void ScanAiTools()
        {
            AiScanStatus = "Looking for AI tools…";
            try
            {
                var detections = await Task.Run(AiToolDetector.DetectAll);
                if (_disposed)
                    return;

                _aiDetections = detections;
                _aiDisabled = AiAssistantSettings.LoadDisabled();

                AiToolRows.Clear();
                foreach (var detection in detections)
                {
                    var tool = detection.Tool;
                    AiToolRows.Add(new AiToolRow(detection, !_aiDisabled.Contains(tool.Id),
                        enabled => SetAiToolEnabled(tool, enabled)));
                }

                int installed = detections.Count(d => d.IsInstalled);
                AiScanStatus = installed == 0
                    ? $"None of the {detections.Count} supported tools is installed. Scanned at {DateTime.Now:t}."
                    : $"{installed} of {detections.Count} supported tools found. Scanned at {DateTime.Now:t}.";

                RefreshAllFunctionChoices();
            }
            catch (Exception ex)
            {
                AiScanStatus = $"The scan failed: {ex.Message}";
            }
        }

        private string? SetAiToolEnabled(AiTool tool, bool enabled)
        {
            if (!AiAssistantSettings.SetEnabled(tool.Id, enabled, out string error))
            {
                ShowStatus(InfoBarSeverity.Error, $"Could not change {tool.Name}", error);
                return error;
            }

            if (enabled)
                _aiDisabled.Remove(tool.Id);
            else
                _aiDisabled.Add(tool.Id);

            RefreshAllFunctionChoices();
            return null;
        }

        private bool IsAiToolAvailable(AiTool tool) =>
            !_aiDisabled.Contains(tool.Id)
            && _aiDetections.Any(d => d.Tool.Id == tool.Id && d.IsInstalled);

        /// <summary>
        /// Every function the picker offers: the built-ins, one per saved profile, and one per AI
        /// tool that is installed and switched on. An AI tool that is not available is still listed,
        /// labelled "(not available)", when <paramref name="inUse"/> already refers to it. Otherwise
        /// that button's function box would go blank.
        /// </summary>
        private IEnumerable<WindowsFunction> FunctionChoices(IEnumerable<ActionItem> inUse)
        {
            var used = new HashSet<string>(AiToolIdsIn(inUse), StringComparer.OrdinalIgnoreCase);

            var ai = AiTools.All
                .Where(tool => IsAiToolAvailable(tool) || used.Contains(tool.Id))
                .Select(tool => AiTools.Choice(tool, IsAiToolAvailable(tool)));

            return WindowsFunctions.Choices
                .Concat(Profiles.Select(WindowsFunctions.ProfileChoice))
                .Concat(ai)
                .ToArray();
        }

        private void RefreshAllFunctionChoices()
        {
            var choices = FunctionChoices(Snapshot()).ToArray();
            foreach (var item in Items)
                item.RefreshFunctionChoices(choices);
        }

        /// <summary>The AI tool ids a set of actions refers to, primary or hold, group children included.</summary>
        private static IEnumerable<string> AiToolIdsIn(IEnumerable<ActionItem> actions)
        {
            foreach (var action in actions)
            {
                if (action.Kind == ActionKind.Function && AiTools.TryParse(action.Value, out string toolId, out _))
                    yield return toolId;
                if (action.HoldKind == ActionKind.Function && AiTools.TryParse(action.HoldValue, out string holdToolId, out _))
                    yield return holdToolId;

                foreach (string childToolId in AiToolIdsIn(action.GroupChildren))
                    yield return childToolId;
            }
        }

        /// <summary>
        /// Save warnings for buttons whose AI tool is missing or switched off. Legal, since a tool
        /// can be installed later, but the button reports an error until then. Skipped until the
        /// first scan has finished, so a save right after opening the window cannot report every
        /// AI button as missing.
        /// </summary>
        private IEnumerable<string> AiToolWarnings()
        {
            if (_aiDetections.Count == 0)
                yield break;

            foreach (string toolId in AiToolIdsIn(Snapshot()).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (AiTools.Find(toolId) is not AiTool tool || IsAiToolAvailable(tool))
                    continue;

                yield return _aiDisabled.Contains(tool.Id)
                    ? $"{tool.Name} is switched off under AI assistants, so its buttons report an error"
                    : $"{tool.Name} is not installed, so its buttons report an error";
            }
        }
    }
}
