using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Action_Wheel.Services;
using Action_Wheel.Settings;
using Microsoft.UI.Dispatching;
using InfoBarSeverity = Action_Wheel.ViewModels.StatusLevel;

namespace Action_Wheel.ViewModels
{
    /// <summary>
    /// The AI assistants section: which AI tools this machine has, the prompt templates, and which
    /// AI functions the picker offers. See Doc/ai-assistants.md.
    /// </summary>
    public sealed partial class SettingsViewModel
    {
        private IReadOnlyList<AiToolDetection> _aiDetections = Array.Empty<AiToolDetection>();
        private HashSet<string> _aiDisabled = new(StringComparer.OrdinalIgnoreCase);
        private string _aiScanStatus = "Looking for AI tools…";
        private RelayCommand? _rescanAiToolsCommand;
        private RelayCommand? _addAiTemplateCommand;
        private RelayCommand? _restoreAiTemplatesCommand;
        private DispatcherQueueTimer? _aiTemplateSaveTimer;

        public ObservableCollection<AiToolRow> AiToolRows { get; } = new();

        public ObservableCollection<AiTemplateRow> AiTemplateRows { get; } = new();

        public string AiScanStatus
        {
            get => _aiScanStatus;
            private set => Set(ref _aiScanStatus, value);
        }

        public RelayCommand RescanAiToolsCommand => _rescanAiToolsCommand ??= new RelayCommand(ScanAiTools);

        public RelayCommand AddAiTemplateCommand => _addAiTemplateCommand ??= new RelayCommand(AddAiTemplate);

        public RelayCommand RestoreAiTemplatesCommand => _restoreAiTemplatesCommand ??= new RelayCommand(RestoreAiTemplates);

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

        #region Prompt templates

        private IReadOnlyList<AiTaskTemplate> CurrentAiTemplates() =>
            AiTemplateRows.Select(row => row.ToTemplate()).ToArray();

        /// <summary>Loaded before the rows are built, so the picker can name a button's template from the start.</summary>
        private void LoadAiTemplates()
        {
            // A pending save of the old rows would otherwise overwrite what was just read, such as a
            // file a backup restore has put back.
            _aiTemplateSaveTimer?.Stop();
            AiTemplateRows.Clear();
            foreach (var template in AiTaskTemplates.Load())
                AiTemplateRows.Add(NewTemplateRow(template));
        }

        private AiTemplateRow NewTemplateRow(AiTaskTemplate template) =>
            new(template, ScheduleAiTemplateSave, DeleteAiTemplate);

        private void AddAiTemplate()
        {
            string id = AiTaskTemplates.NewId("New task", AiTemplateRows.Select(row => row.Id));
            AiTemplateRows.Add(NewTemplateRow(new AiTaskTemplate(id, "New task",
                "Do something useful with the following text.\n\n" + AiTaskTemplates.Placeholder)));
            ScheduleAiTemplateSave();
        }

        /// <summary>
        /// Deleting is allowed even when buttons use the template: those buttons keep their value, the
        /// picker shows them as "(not available)", and Save warns about them.
        /// </summary>
        private void DeleteAiTemplate(AiTemplateRow row)
        {
            AiTemplateRows.Remove(row);
            ScheduleAiTemplateSave();
        }

        /// <summary>Puts the four built-in templates back, keeping any of the user's own that have other ids.</summary>
        private void RestoreAiTemplates()
        {
            var own = AiTemplateRows.Select(row => row.ToTemplate())
                .Where(t => AiTaskTemplates.Find(AiTaskTemplates.Defaults(), t.Id) == null)
                .ToList();

            AiTemplateRows.Clear();
            foreach (var template in AiTaskTemplates.Defaults().Concat(own))
                AiTemplateRows.Add(NewTemplateRow(template));
            ScheduleAiTemplateSave();
        }

        /// <summary>
        /// Templates save on their own, a moment after the last keystroke, like the tool switches:
        /// they describe the AI functions on offer, not what a button does, so they are not part of
        /// Save and Undo.
        /// </summary>
        private void ScheduleAiTemplateSave()
        {
            if (_aiTemplateSaveTimer == null)
            {
                _aiTemplateSaveTimer = _dispatcher.CreateTimer();
                _aiTemplateSaveTimer.Interval = TimeSpan.FromMilliseconds(600);
                _aiTemplateSaveTimer.IsRepeating = false;
                _aiTemplateSaveTimer.Tick += (s, e) => SaveAiTemplates();
            }

            _aiTemplateSaveTimer.Stop();
            _aiTemplateSaveTimer.Start();
        }

        private void SaveAiTemplates()
        {
            if (_disposed)
                return;

            if (!AiTaskTemplates.Save(CurrentAiTemplates(), out string error))
            {
                ShowStatus(InfoBarSeverity.Error, "Could not save the prompt templates", error);
                return;
            }

            RefreshAllFunctionChoices();
        }

        /// <summary>Writes a pending template edit now. Called when the window closes.</summary>
        private void FlushAiTemplates()
        {
            if (_aiTemplateSaveTimer?.IsRunning == true)
            {
                _aiTemplateSaveTimer.Stop();
                SaveAiTemplates();
            }
        }

        #endregion

        /// <summary>
        /// Every function the picker offers: the built-ins, one per saved profile, and for each AI tool
        /// that is installed and switched on, "open" plus one entry per prompt template if the tool can
        /// run tasks. An AI function that is not available (missing tool, switched off, deleted
        /// template) is still listed, labelled "(not available)", when <paramref name="inUse"/> already
        /// refers to it. Otherwise that button's function box would go blank.
        /// </summary>
        private IEnumerable<WindowsFunction> FunctionChoices(IEnumerable<ActionItem> inUse)
        {
            var templates = CurrentAiTemplates();
            var ai = new List<WindowsFunction>();
            var listed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void Add(WindowsFunction choice)
            {
                if (listed.Add(choice.Id))
                    ai.Add(choice);
            }

            foreach (var tool in AiTools.All.Where(IsAiToolAvailable))
            {
                Add(AiTools.Choice(tool));
                if (tool.SupportsTasks)
                {
                    foreach (var template in templates)
                        Add(AiTools.TaskChoice(tool, template.Id, template.Name, available: true));
                }
            }

            foreach (string id in AiFunctionIdsIn(inUse))
            {
                if (listed.Contains(id) || !AiTools.TryParse(id, out string toolId, out string task)
                    || AiTools.Find(toolId) is not AiTool tool)
                    continue;

                if (AiTools.TryGetTemplateId(task, out string templateId))
                    Add(AiTools.TaskChoice(tool, templateId, AiTaskTemplates.Find(templates, templateId)?.Name, available: false));
                else
                    Add(AiTools.Choice(tool, available: false));
            }

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

        /// <summary>The AI Function ids a set of actions uses, primary or hold, group children included.</summary>
        private static IEnumerable<string> AiFunctionIdsIn(IEnumerable<ActionItem> actions)
        {
            foreach (var action in actions)
            {
                if (action.Kind == ActionKind.Function && AiTools.TryParse(action.Value, out _, out _))
                    yield return action.Value;
                if (action.HoldKind == ActionKind.Function && AiTools.TryParse(action.HoldValue, out _, out _))
                    yield return action.HoldValue;

                foreach (string childId in AiFunctionIdsIn(action.GroupChildren))
                    yield return childId;
            }
        }

        /// <summary>
        /// Save warnings for AI buttons that cannot run: the tool is missing or switched off, or the
        /// template was deleted. Legal, since either can come back, but the button reports an error
        /// until then. Tool checks wait for the first scan, so a save right after opening the window
        /// cannot report every AI button as missing.
        /// </summary>
        private IEnumerable<string> AiToolWarnings()
        {
            var templates = CurrentAiTemplates();
            var warnedTools = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string id in AiFunctionIdsIn(Snapshot()).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!AiTools.TryParse(id, out string toolId, out string task) || AiTools.Find(toolId) is not AiTool tool)
                    continue;

                if (AiTools.TryGetTemplateId(task, out string templateId) && AiTaskTemplates.Find(templates, templateId) == null)
                    yield return $"The prompt template '{templateId}' no longer exists, so its buttons report an error";

                if (_aiDetections.Count == 0 || IsAiToolAvailable(tool) || !warnedTools.Add(tool.Id))
                    continue;

                yield return _aiDisabled.Contains(tool.Id)
                    ? $"{tool.Name} is switched off under AI assistants, so its buttons report an error"
                    : $"{tool.Name} is not installed, so its buttons report an error";
            }
        }
    }
}
