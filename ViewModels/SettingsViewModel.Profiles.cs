using System;
using System.Linq;
using Action_Wheel.Core;
using Action_Wheel.Services;
using InfoBarSeverity = Action_Wheel.ViewModels.StatusLevel;

namespace Action_Wheel.ViewModels
{
    public sealed partial class SettingsViewModel
    {
        private string _profileName = string.Empty;
        private string? _selectedProfile;
        private string _loadedProfileName = "Main configuration";
        private string _activeProfileName = "Main configuration";

        public string ActiveProfileText => $"Active profile: {_activeProfileName}";
        public string ProfileCountText => $"{Profiles.Count} saved profile(s)";

        public string ProfileName { get => _profileName; set => Set(ref _profileName, value); }
        public string? SelectedProfile
        {
            get => _selectedProfile;
            set
            {
                if (!Set(ref _selectedProfile, value)) return;
                LoadProfileCommand.RaiseCanExecuteChanged();
                DeleteProfileCommand.RaiseCanExecuteChanged();
                RenameProfileCommand.RaiseCanExecuteChanged();
            }
        }

        private void RefreshProfiles()
        {
            Profiles.Clear();
            if (_profileLibrary.TryList(out var names, out string error))
                foreach (string name in names) Profiles.Add(name);
            else
            {
                ShowStatus(InfoBarSeverity.Error, "Profiles unavailable", error);
            }
            Raise(nameof(ProfileCountText));

            var functionChoices = WindowsFunctions.Choices.Concat(Profiles.Select(WindowsFunctions.ProfileChoice)).ToArray();
            foreach (var item in Items)
                item.RefreshFunctionChoices(functionChoices);
        }

        private void SaveNamedProfile()
        {
            string name = ProfileLibrary.SafeName(ProfileName);
            if (string.IsNullOrWhiteSpace(name))
            {
                ShowStatus(InfoBarSeverity.Warning, "Profile name required", "Enter a valid profile name first.");
                return;
            }
            var problems = Validate();
            if (problems.Count > 0)
            {
                ShowStatus(InfoBarSeverity.Error, "Could not save the profile", string.Join("  •  ", problems));
                return;
            }
            if (!_profileLibrary.TrySave(name, Snapshot(), out string error))
            {
                ShowStatus(InfoBarSeverity.Error, "Could not save the profile", error);
                return;
            }
            RefreshProfiles();
            SelectedProfile = name;
        }

        private void LoadNamedProfile()
        {
            if (SelectedProfile == null) return;
            string profileName = SelectedProfile;
            if (!_profileLibrary.TryLoad(profileName, out var actions, out string error))
            {
                ShowStatus(InfoBarSeverity.Error, "Could not load the profile", error);
                return;
            }

            CaptureUndoPoint();
            LoadItems(actions);

            if (!ActionConfig.Save(actions, out string configError))
            {
                ShowStatus(InfoBarSeverity.Error, "Could not activate the profile", configError);
                return;
            }

            if (!ActiveProfileSettings.Save(profileName, out string labelError))
            {
                ShowStatus(InfoBarSeverity.Error, "Profile loaded with an error",
                    $"The buttons were applied, but the active profile name could not be saved: {labelError}");
                ActionsSaved?.Invoke(this, EventArgs.Empty);
                return;
            }

            _profileLibrary.TryTouch(profileName);
            SetActiveProfile(profileName);
            CaptureUndoPoint();
            ActionsSaved?.Invoke(this, EventArgs.Empty);
            ShowStatus(InfoBarSeverity.Success, "Profile activated",
                $"{profileName} is now active. Save will update this profile.");
        }

        private void DeleteNamedProfile()
        {
            if (SelectedProfile == null) return;
            string name = SelectedProfile;
            if (string.Equals(name, _activeProfileName, StringComparison.OrdinalIgnoreCase))
            {
                ShowStatus(InfoBarSeverity.Warning, "Active profile not deleted",
                    "Load another profile before deleting this one.");
                return;
            }
            if (!_profileLibrary.TryDelete(name, out string error))
            {
                ShowStatus(InfoBarSeverity.Error, "Profile not deleted", error);
                return;
            }
            SelectedProfile = null;
            RefreshProfiles();
            ShowStatus(InfoBarSeverity.Informational, "Profile deleted", "The active configuration was not changed.");
        }

        private void RenameNamedProfile()
        {
            if (SelectedProfile == null) return;
            string oldName = SelectedProfile;
            string newName = ProfileLibrary.SafeName(ProfileName);
            if (!_profileLibrary.TryRename(oldName, newName, out string error))
            {
                ShowStatus(InfoBarSeverity.Error, "Profile not renamed", error);
                return;
            }

            string? renameWarning = null;
            if (string.Equals(oldName, _activeProfileName, StringComparison.OrdinalIgnoreCase))
            {
                if (!ActiveProfileSettings.Save(newName, out string labelError))
                {
                    renameWarning = $"The active profile label could not be updated: {labelError}";
                }
                else
                {
                    SetActiveProfile(newName);
                    ActionsSaved?.Invoke(this, EventArgs.Empty);
                }
            }
            RefreshProfiles();
            SelectedProfile = newName;
            if (renameWarning == null)
                ShowStatus(InfoBarSeverity.Success, "Profile renamed", newName);
            else
                ShowStatus(InfoBarSeverity.Warning, "Profile renamed with an error", renameWarning);
        }

        private void SetActiveProfile(string name)
        {
            _activeProfileName = string.IsNullOrWhiteSpace(name) ? "Main configuration" : name;
            _loadedProfileName = _activeProfileName;
            Raise(nameof(ActiveProfileText));
        }
    }
}
