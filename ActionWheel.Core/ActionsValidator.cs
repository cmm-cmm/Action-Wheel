using System.Collections.Generic;
using System.Linq;

namespace Action_Wheel.Core
{
    public sealed record ActionValidationIssue(int? Tag, string Message);

    public static class ActionsValidator
    {
        public static IReadOnlyList<ActionValidationIssue> Validate(IReadOnlyList<ActionItem> actions)
        {
            var issues = new List<ActionValidationIssue>();
            if (actions.Count != 9)
                issues.Add(new(null, $"Expected exactly 9 actions, but found {actions.Count}."));

            foreach (var duplicate in actions.GroupBy(a => a.Tag).Where(g => g.Count() > 1))
                issues.Add(new(duplicate.Key, $"Tag {duplicate.Key} appears more than once."));

            foreach (var action in actions)
            {
                string prefix = $"Tag {action.Tag}";
                if (action.Tag is < 0 or > 8) issues.Add(new(action.Tag, $"{prefix} is outside the supported range 0-8."));
                if (!System.Enum.IsDefined(action.Kind)) issues.Add(new(action.Tag, $"{prefix} has an unsupported action type."));
                if (action.Kind != ActionKind.None && string.IsNullOrWhiteSpace(action.Value))
                    issues.Add(new(action.Tag, $"{prefix} requires a value."));
                else if (action.Kind == ActionKind.Keys && !ShortcutKeys.IsValid(action.Value))
                    issues.Add(new(action.Tag, $"{prefix} contains an invalid shortcut."));
                else if (action.Kind == ActionKind.Function && !WindowsFunctions.IsKnown(action.Value))
                    issues.Add(new(action.Tag, $"{prefix} has an unknown Windows function '{action.Value}'."));

                if (!ColorValue.IsValidOrEmpty(action.Foreground) || !ColorValue.IsValidOrEmpty(action.Background)
                    || !ColorValue.IsValidOrEmpty(action.Shadow))
                    issues.Add(new(action.Tag, $"{prefix} contains an invalid colour."));
                if (action.ShadowOpacity is < 0 or > 1 || action.ShadowBlur is < 0 or > 64
                    || action.ShadowOffsetX is < -50 or > 50 || action.ShadowOffsetY is < -50 or > 50)
                    issues.Add(new(action.Tag, $"{prefix} contains invalid shadow settings."));
                if (!double.IsFinite(action.IconScale) || action.IconScale is < 0.25 or > 3
                    || !double.IsFinite(action.IconOffsetX) || !double.IsFinite(action.IconOffsetY)
                    || action.IconOffsetX is < -50 or > 50
                    || action.IconOffsetY is < -50 or > 50)
                    issues.Add(new(action.Tag, $"{prefix} contains invalid icon layout settings."));
                bool icon = !string.IsNullOrWhiteSpace(action.IconPath) && IconFile.IsUsable(action.IconPath);
                if (!string.IsNullOrWhiteSpace(action.IconPath) && !icon)
                    issues.Add(new(action.Tag, $"{prefix} refers to an icon file that cannot be read. Supported: {IconFile.SupportedDescription}."));
                else if (!icon && !ActionValueCodec.TryConvertGlyph(action.Glyph, out _))
                    issues.Add(new(action.Tag, $"{prefix} contains an invalid glyph."));
            }
            return issues;
        }
    }
}
