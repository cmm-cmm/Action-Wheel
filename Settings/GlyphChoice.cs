using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Action_Wheel.Services;

namespace Action_Wheel.Settings
{
    /// <summary>
    /// One entry of the icon picker: an icon-font code point and the character it renders.
    /// </summary>
    public sealed class GlyphChoice
    {
        private GlyphChoice(string code, string preview, string name)
        {
            Code = code;
            Preview = preview;
            Name = name;
        }

        /// <summary>The bundled icon-font code as stored in actions.json, e.g. "F0C5".</summary>
        public string Code { get; }

        /// <summary>The character itself, for display.</summary>
        public string Preview { get; }

        /// <summary>What the icon depicts, where that is known. Empty for the rest of the font.</summary>
        public string Name { get; }

        /// <summary>What the picker shows on hover.</summary>
        public string Tooltip => Name.Length == 0 ? Code : $"{Name}  ·  {Code}";

        /// <summary>
        /// The icons people reach for first, in a sensible reading order rather than by code point.
        /// </summary>
        /// <remarks>
        /// Only glyphs whose meaning is unambiguous are named here. The rest of the font is still
        /// offered through <see cref="All"/> - a name that is merely a good guess would make search
        /// worse than no search, because a wrong hit is harder to recover from than a missing one.
        /// </remarks>
        public static IReadOnlyList<GlyphChoice> Common { get; } = Build(new (string Code, string Name)[]
        {
            // Editing
            ("F0C5", "Copy"), ("F0EA", "Paste"), ("F0C4", "Cut"), ("F040", "Edit"),
            ("F0C7", "Save"), ("F014", "Delete"), ("F055", "Add"), ("F056", "Remove"),
            ("F0E2", "Undo"), ("F01E", "Redo"), ("F00C", "Tick"), ("F00D", "Close"),
            ("F02F", "Print"), ("F046", "Task done"), ("F0C6", "Attach"),

            // Navigation
            ("F0C9", "Menu"), ("F015", "Home"), ("F04F", "Back"),
            ("F107", "Chevron down"), ("F106", "Chevron up"),
            ("F104", "Chevron left"), ("F105", "Chevron right"),
            ("F002", "Search"), ("F00E", "Zoom in"), ("F010", "Zoom out"),
            ("F009", "Grid"), ("F00B", "List"), ("F013", "Settings"),
            ("EAF1", "Filter"), ("EA7C", "More"), ("F047", "Move"),

            // Files
            ("F114", "Folder"), ("F115", "Folder open"), ("F016", "File"),
            ("F0F6", "Document"), ("F02D", "Book"), ("F02E", "Bookmark"),
            ("F019", "Download"), ("F0EE", "Upload"), ("F021", "Refresh"),
            ("F0ED", "Cloud download"), ("F0C2", "Cloud"), ("EAEF", "Archive"),

            // Media
            ("F04B", "Play"), ("F04C", "Pause"), ("F04D", "Stop"),
            ("F048", "Previous"), ("F051", "Next"), ("F04A", "Rewind"), ("F04E", "Fast forward"),
            ("F028", "Volume"), ("F026", "Mute"), ("F025", "Headphones"),
            ("F03D", "Video"), ("F030", "Camera"), ("F03E", "Image"), ("F001", "Music"),

            // System and hardware
            ("F023", "Lock"), ("F011", "Power"), ("F108", "Desktop"), ("F109", "Laptop"),
            ("F10B", "Phone"), ("F11C", "Keyboard"), ("EA85", "Terminal"), ("EACE", "Database"),
            ("F0E7", "Lightning"), ("EAAF", "Bug"), ("EB44", "Rocket"), ("EB53", "Shield"),

            // People, places and messages
            ("F0E0", "Mail"), ("F007", "Person"), ("F0C0", "People"),
            ("F041", "Location"), ("EB01", "Globe"), ("F0C1", "Link"), ("F045", "Share"),
            ("F0F3", "Bell"), ("F017", "Clock"), ("EAB0", "Calendar"), ("F0E5", "Comment"),
            ("F005", "Star"), ("F006", "Star outline"), ("F004", "Heart"), ("F024", "Flag"),
            ("F059", "Help"), ("F05A", "Info"), ("EA6C", "Warning"), ("EA87", "Error"),

            // Logos, which is most of why this font replaced the system's
            ("E70F", "Windows"), ("E711", "Apple"), ("E712", "Linux"), ("E70E", "Android"),
            ("E743", "Chrome"), ("E745", "Firefox"), ("E744", "Edge"), ("E746", "Opera"),
            ("E70C", "Visual Studio Code"), ("E709", "GitHub"), ("E702", "Git"),
            ("E73C", "Python"), ("E738", "Java"), ("E74E", "JavaScript"), ("E739", "Ruby"),
            ("E73D", "PHP"), ("E736", "HTML"), ("E749", "CSS"), ("E73A", "Ubuntu"),
            ("E718", "Node.js"), ("E71E", "npm"), ("E753", "Angular"), ("E755", "Swift"),
            ("E71D", "Django"), ("E70B", "WordPress"), ("E721", "Unity"), ("E722", "Raspberry Pi"),
            ("E76E", "PostgreSQL"), ("E704", "MySQL"), ("E705", "Database"),
            ("E731", "Google Drive"), ("E707", "Dropbox"),
        });

        /// <summary>
        /// Every icon the installed font can draw, named where <see cref="Common"/> knows the name.
        /// Falls back to the curated list on a machine with no icon font.
        /// </summary>
        public static IReadOnlyList<GlyphChoice> All { get; } = BuildAll();

        /// <summary>
        /// Filters a list by name or by code. A hex query matches as a prefix so typing "E8" walks
        /// into that part of the font, which is how someone with a code from a web reference arrives.
        /// </summary>
        public static IReadOnlyList<GlyphChoice> Search(IReadOnlyList<GlyphChoice> source, string? query)
        {
            var trimmed = (query ?? string.Empty).Trim();
            if (trimmed.Length == 0)
                return source;

            return source.Where(choice =>
                choice.Code.StartsWith(trimmed, StringComparison.OrdinalIgnoreCase)
                || (choice.Name.Length > 0
                    && choice.Name.Contains(trimmed, StringComparison.OrdinalIgnoreCase)))
                .ToArray();
        }

        private static IReadOnlyList<GlyphChoice> BuildAll()
        {
            var codePoints = IconFont.CodePoints;
            if (codePoints.Count == 0)
                return Common;

            var names = Common.ToDictionary(choice => choice.Code, choice => choice.Name,
                StringComparer.OrdinalIgnoreCase);

            var list = new List<GlyphChoice>(codePoints.Count);
            foreach (int codePoint in codePoints)
            {
                string code = codePoint.ToString("X4", CultureInfo.InvariantCulture);
                if (TryRender(codePoint, out string preview))
                    list.Add(new GlyphChoice(code, preview, names.GetValueOrDefault(code, string.Empty)));
            }

            return list;
        }

        private static IReadOnlyList<GlyphChoice> Build((string Code, string Name)[] entries)
        {
            var list = new List<GlyphChoice>(entries.Length);

            foreach (var (code, name) in entries)
            {
                if (int.TryParse(code, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int codePoint)
                    && TryRender(codePoint, out string preview))
                {
                    list.Add(new GlyphChoice(code, preview, name));
                }
            }

            return list;
        }

        private static bool TryRender(int codePoint, out string preview)
        {
            preview = string.Empty;
            if (!System.Text.Rune.IsValid(codePoint))
                return false;

            try
            {
                preview = char.ConvertFromUtf32(codePoint);
                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                // Not a representable code point - just leave it out of the picker.
                return false;
            }
        }
    }
}
