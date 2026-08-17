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
                ValidateItem(action, isTopLevel: true, issues);

            return issues;
        }

        /// <summary>
        /// Checks one action's own fields - shared between the 9 top-level actions and, recursively,
        /// each child of a <see cref="ActionKind.Group"/> button, which is why every issue is keyed
        /// by <paramref name="action"/>'s own Tag rather than the top-level button's: a child's Tag
        /// is its position among its siblings (see <see cref="ActionItem.GroupChildren"/>), not a
        /// slot on the main ring, so a reported Tag only means "child N" when it did not come from
        /// the top-level loop - the caller (the settings row for that group) is what has to tell the
        /// two apart, since nothing here has the parent's Tag to prefix it with.
        /// </summary>
        private static void ValidateItem(ActionItem action, bool isTopLevel, List<ActionValidationIssue> issues)
        {
            string prefix = $"Tag {action.Tag}";
            if (isTopLevel && action.Tag is < 0 or > 8)
                issues.Add(new(action.Tag, $"{prefix} is outside the supported range 0-8."));
            if (!System.Enum.IsDefined(action.Kind))
                issues.Add(new(action.Tag, $"{prefix} has an unsupported action type."));
            if (!isTopLevel && action.Kind == ActionKind.Group)
                issues.Add(new(action.Tag, $"{prefix} is a group child and cannot itself be a group."));
            if (isTopLevel && action.Tag == 0 && action.Kind == ActionKind.Group)
                issues.Add(new(action.Tag, "The centre button cannot be a group."));

            bool needsValue = action.Kind != ActionKind.None && action.Kind != ActionKind.Group;
            if (needsValue && string.IsNullOrWhiteSpace(action.Value))
                issues.Add(new(action.Tag, $"{prefix} requires a value."));
            else if (action.Kind == ActionKind.Keys && !ShortcutKeys.IsValid(action.Value))
                issues.Add(new(action.Tag, $"{prefix} contains an invalid shortcut."));
            else if (action.Kind == ActionKind.Function && !WindowsFunctions.IsKnown(action.Value))
                issues.Add(new(action.Tag, $"{prefix} has an unknown Windows function '{action.Value}'."));

            if (action.HoldKind != ActionKind.None)
            {
                if (!System.Enum.IsDefined(action.HoldKind))
                    issues.Add(new(action.Tag, $"{prefix} has an unsupported hold action type."));
                else if (string.IsNullOrWhiteSpace(action.HoldValue))
                    issues.Add(new(action.Tag, $"{prefix} has a hold action but no value."));
                else if (action.HoldKind == ActionKind.Keys && !ShortcutKeys.IsValid(action.HoldValue))
                    issues.Add(new(action.Tag, $"{prefix} contains an invalid hold shortcut."));
                else if (action.HoldKind == ActionKind.Function && !WindowsFunctions.IsKnown(action.HoldValue))
                    issues.Add(new(action.Tag, $"{prefix} has an unknown hold Windows function '{action.HoldValue}'."));
            }

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
            bool text = string.IsNullOrWhiteSpace(action.IconPath) && !string.IsNullOrWhiteSpace(action.IconText);
            if (!string.IsNullOrWhiteSpace(action.IconPath) && !icon)
                issues.Add(new(action.Tag, $"{prefix} refers to an icon file that cannot be read. Supported: {IconFile.SupportedDescription}."));
            else if (!icon && !text && !ActionValueCodec.TryConvertGlyph(action.Glyph, out _))
                issues.Add(new(action.Tag, $"{prefix} contains an invalid glyph."));

            if (isTopLevel && action.Kind == ActionKind.Group)
            {
                if (action.GroupChildren.Count > ActionItem.MaxGroupChildren)
                    issues.Add(new(action.Tag, $"{prefix} has {action.GroupChildren.Count} group children, more than the {ActionItem.MaxGroupChildren} supported."));

                foreach (var child in action.GroupChildren)
                    ValidateItem(child, isTopLevel: false, issues);
            }
        }
    }
}
