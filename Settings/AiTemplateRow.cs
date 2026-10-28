using System;
using Action_Wheel.ViewModels;

namespace Action_Wheel.Settings
{
    /// <summary>One editable prompt template in the AI assistants section.</summary>
    /// <remarks>
    /// The id never changes once made: it is what a button's Function id refers to, so renaming a
    /// template keeps every button that uses it working.
    /// </remarks>
    public sealed class AiTemplateRow : ObservableObject
    {
        private readonly Action _changed;
        private string _name;
        private string _prompt;
        private bool _confirm;

        public AiTemplateRow(AiTaskTemplate template, Action changed, Action<AiTemplateRow> delete)
        {
            Id = template.Id;
            _name = template.Name;
            _prompt = template.Prompt;
            _confirm = template.Confirm;
            _changed = changed;
            DeleteCommand = new RelayCommand(() => delete(this));
        }

        public string Id { get; }

        public RelayCommand DeleteCommand { get; }

        public string Name
        {
            get => _name;
            set
            {
                if (Set(ref _name, value))
                    _changed();
            }
        }

        public string Prompt
        {
            get => _prompt;
            set
            {
                if (Set(ref _prompt, value))
                {
                    Raise(nameof(PlaceholderHint));
                    _changed();
                }
            }
        }

        public bool Confirm
        {
            get => _confirm;
            set
            {
                if (Set(ref _confirm, value))
                    _changed();
            }
        }

        /// <summary>Says where the selected text goes, so a template without the placeholder is not a surprise.</summary>
        public string PlaceholderHint => (_prompt ?? string.Empty).Contains(AiTaskTemplates.Placeholder, StringComparison.Ordinal)
            ? $"{AiTaskTemplates.Placeholder} is replaced by the selected text."
            : $"No {AiTaskTemplates.Placeholder} in the prompt, so the selected text is added after it.";

        public AiTaskTemplate ToTemplate() =>
            new(Id, string.IsNullOrWhiteSpace(_name) ? Id : _name.Trim(), _prompt ?? string.Empty, _confirm);
    }
}
