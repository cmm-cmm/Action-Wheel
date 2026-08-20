using Action_Wheel.Core;

// A manual guardrail against the exact bug class found in this repo while adding IconText:
// a field added to ActionItem but missed in one of the parallel places that has to mirror it by
// hand - ActionConfig's JSON reader/writer, or ProfileXml's XML reader/writer. There is
// deliberately no test project (see CLAUDE.md), so this is the throwaway console tool CLAUDE.md
// already describes for exercising ActionWheel.Core without a desktop session - run it by hand
// after adding a field to ActionItem, with:
//   dotnet run --project Tools\RoundTripCheck\RoundTripCheck.csproj
//
// It builds one ActionItem with every field set to a distinct, non-default value (plus one Group
// child, to exercise the recursive case), round-trips that set through both codecs, and compares
// field-by-field. A plain ActionItem.Equals/SequenceEqual will not do here: GroupChildren is a
// collection, and the compiler-generated record equality compares it by reference, not content -
// two freshly parsed lists are never the same reference even when every child field matches.

int failures = 0;

void Check(string label, bool passed)
{
    Console.WriteLine(passed ? $"  OK    {label}" : $"  FAIL  {label}");
    if (!passed)
        failures++;
}

var probeChild = new ActionItem
{
    Tag = 0,
    Label = "Group child probe",
    Kind = ActionKind.Keys,
    Value = "Ctrl+Shift+G",
    Arguments = "child-args",
    Glyph = "F0C5",
    IconText = "C1",
    IconScale = 1.2,
    IconOffsetX = -3,
    IconOffsetY = 4,
    IconTint = false,
    Foreground = "#FF010203",
    Background = "#FF040506",
    Shadow = "#FF070809",
    ShadowEnabled = false,
    ShadowOpacity = 0.6,
    ShadowBlur = 5,
    ShadowOffsetX = -1,
    ShadowOffsetY = 2,
};

var probe = new ActionItem
{
    Tag = 1,
    Label = "Round-trip probe",
    Kind = ActionKind.Keys,
    Value = "Ctrl+Shift+R",
    Arguments = "probe-args",
    Glyph = "F0C5",
    IconText = "RT",
    IconScale = 1.75,
    IconOffsetX = -12.5,
    IconOffsetY = 8.25,
    IconTint = false,
    Foreground = "#FF112233",
    Background = "#FF445566",
    Shadow = "#80778899",
    ShadowEnabled = false,
    ShadowOpacity = 0.33,
    ShadowBlur = 21,
    ShadowOffsetX = -4,
    ShadowOffsetY = 6,
    HoldKind = ActionKind.Function,
    HoldValue = WindowsFunctions.Lock,
    HoldArguments = "hold-args",
};

var groupProbe = new ActionItem
{
    Tag = 2,
    Label = "Group probe",
    Kind = ActionKind.Group,
    Glyph = "F045",
    GroupChildren = new[] { probeChild },
};

var items = new List<ActionItem> { probe, groupProbe };
foreach (int tag in new[] { 0, 3, 4, 5, 6, 7, 8 })
    items.Add(new ActionItem { Tag = tag, Label = $"Filler {tag}", Glyph = "F00D" });
items = items.OrderBy(item => item.Tag).ToList();

Console.WriteLine("Action Wheel round-trip check");
Console.WriteLine();

if (!ActionConfig.TryValidate(items, out string probeError))
{
    Console.WriteLine($"The probe set itself is invalid - fix the probe, not the codecs: {probeError}");
    return 1;
}

string json = ActionConfig.ToJson(items);
var parsedJson = ActionConfig.Parse(json).OrderBy(item => item.Tag).ToList();
Check("actions.json round-trip (ActionConfig.ToJson / Parse)", DeepEqualAll(items, parsedJson));

string profilePath = Path.Combine(Path.GetTempPath(), $"ActionWheelRoundTrip-{Guid.NewGuid():N}.xml");
try
{
    if (!ProfileXml.Save(profilePath, items, out string saveError))
    {
        Check("profile .xml round-trip (ProfileXml.Save / TryLoad)", false);
        Console.WriteLine($"        ProfileXml.Save failed: {saveError}");
    }
    else if (!ProfileXml.TryLoad(profilePath, out var parsedProfile, out string loadError))
    {
        Check("profile .xml round-trip (ProfileXml.Save / TryLoad)", false);
        Console.WriteLine($"        ProfileXml.TryLoad failed: {loadError}");
    }
    else
    {
        var parsedProfileSorted = parsedProfile.OrderBy(item => item.Tag).ToList();
        Check("profile .xml round-trip (ProfileXml.Save / TryLoad)", DeepEqualAll(items, parsedProfileSorted));
    }
}
finally
{
    TryDelete(profilePath);
    TryDelete(profilePath + ".bak");
}

Console.WriteLine();
Console.WriteLine(failures == 0
    ? "All round-trip checks passed."
    : $"{failures} round-trip check(s) failed - a field is being dropped somewhere in the codecs above.");

return failures == 0 ? 0 : 1;

static void TryDelete(string path)
{
    try { if (File.Exists(path)) File.Delete(path); } catch { }
}

static bool DeepEqualAll(IReadOnlyList<ActionItem> a, IReadOnlyList<ActionItem> b)
{
    if (a.Count != b.Count)
        return false;

    for (int i = 0; i < a.Count; i++)
        if (!DeepEqual(a[i], b[i]))
            return false;

    return true;
}

// Field-by-field rather than a == b: ActionItem is a record, but GroupChildren is a collection,
// and the compiler-generated equality compares a collection member by reference, not content -
// two freshly parsed lists never share a reference even when every child matches exactly.
static bool DeepEqual(ActionItem a, ActionItem b) =>
    a.Tag == b.Tag
    && a.Label == b.Label
    && a.Kind == b.Kind
    && a.Value == b.Value
    && a.Arguments == b.Arguments
    && a.Glyph == b.Glyph
    && a.IconPath == b.IconPath
    && a.IconText == b.IconText
    && a.IconScale == b.IconScale
    && a.IconOffsetX == b.IconOffsetX
    && a.IconOffsetY == b.IconOffsetY
    && a.IconTint == b.IconTint
    && a.Foreground == b.Foreground
    && a.Background == b.Background
    && a.Shadow == b.Shadow
    && a.ShadowEnabled == b.ShadowEnabled
    && a.ShadowOpacity == b.ShadowOpacity
    && a.ShadowBlur == b.ShadowBlur
    && a.ShadowOffsetX == b.ShadowOffsetX
    && a.ShadowOffsetY == b.ShadowOffsetY
    && a.HoldKind == b.HoldKind
    && a.HoldValue == b.HoldValue
    && a.HoldArguments == b.HoldArguments
    && DeepEqualAll(a.GroupChildren, b.GroupChildren);
