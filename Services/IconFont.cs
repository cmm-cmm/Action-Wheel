using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Action_Wheel.Services
{
    /// <summary>
    /// The font the ring draws its glyph icons from, and what it can draw.
    /// </summary>
    /// <remarks>
    /// CaskaydiaCove NF - the Nerd Fonts build of Microsoft's Cascadia Code - replaced Segoe Fluent
    /// Icons here. It carries about ten thousand icons against Segoe's two thousand, and unlike
    /// Segoe it has the application and language logos a launcher ring actually wants to point at.
    ///
    /// The two fonts overlap in E700-E8EF and disagree about all 887 shared code points: E711 is a
    /// close button in Segoe and the Apple logo here. A glyph code is therefore only meaningful
    /// together with the font it was chosen from, which is why the file is shipped with the app
    /// rather than looked up on the machine - a config written on one computer has to draw the same
    /// picture on the next one. The XAML content and the raw cmap reader use separate entries in
    /// the single-file bundle; the latter is an embedded resource so it never depends on where the
    /// runtime extracts content.
    /// </remarks>
    public static class IconFont
    {
        /// <summary>The name in the font's own name table. Anything else fails to match.</summary>
        public const string FamilyName = "CaskaydiaCove NFP";

        private const string ResourceName = "ActionWheel.IconFont.ttf";

        private static FontFamily? _family;
        private static IReadOnlyList<int>? _codePoints;
        private static HashSet<int>? _drawable;

        /// <summary>The key App.xaml declares the font under.</summary>
        private const string ResourceKey = "IconFontFamily";

        /// <summary>
        /// The family to give a <c>FontIcon</c> built in code, so it matches the one the XAML uses.
        /// </summary>
        /// <remarks>
        /// Taken from the application resources rather than constructed here, because the ms-appx
        /// path and the family name have to agree exactly and two copies of that string would
        /// eventually disagree - silently, since a font that fails to resolve falls back to the
        /// system's rather than raising anything. App.xaml holds the one copy; assigning it from
        /// the App constructor instead was tried and crashed XAML during startup.
        /// </remarks>
        public static FontFamily Family => _family ??=
            Application.Current?.Resources.TryGetValue(ResourceKey, out object? declared) == true
            && declared is FontFamily family
                ? family
                : new FontFamily(FamilyName);

        /// <summary>
        /// Every character the font can draw. Read from the file rather than from the system, so it
        /// is right even though the font is not installed - and so the icons above U+FFFF, two
        /// thirds of what this font has, are included at all.
        /// </summary>
        public static IReadOnlyList<int> CodePoints => _codePoints ??= ReadCodePoints();

        /// <summary>True when the shipped font was found and could be read.</summary>
        public static bool IsAvailable => CodePoints.Count > 0;

        /// <summary>
        /// Whether this font has an icon at <paramref name="codePoint"/>.
        /// </summary>
        /// <remarks>
        /// Version 2 deliberately keeps nothing for the code points of the icon font it used to
        /// draw with. There is no sensible translation between the two - they disagree about 887
        /// values and agree on none of them - so a code this font does not have is shown as nothing
        /// at all. Guessing would put the wrong picture on a button, and the notdef box would look
        /// like a rendering bug rather than a setting to change.
        ///
        /// Answers true when the font could not be read at all, so a machine with a missing font
        /// file loses its icons rather than being told every one of them is wrong.
        /// </remarks>
        public static bool CanDraw(int codePoint)
        {
            if (!IsAvailable)
                return true;

            _drawable ??= new HashSet<int>(CodePoints);
            return _drawable.Contains(codePoint);
        }

        /// <summary>
        /// Whether a stored glyph field names an icon this font can draw, handing back the text to
        /// draw when it does.
        /// </summary>
        public static bool TryResolve(string? glyph, out string text)
        {
            if (ActionValueCodec.TryConvertGlyph(glyph, out text) && CanDraw(char.ConvertToUtf32(text, 0)))
                return true;

            text = string.Empty;
            return false;
        }

        private static IReadOnlyList<int> ReadCodePoints()
        {
            using var stream = typeof(IconFont).Assembly.GetManifestResourceStream(ResourceName);
            if (stream == null)
                return Array.Empty<int>();

            var fromFile = FontGlyphs.Read(stream);
            if (fromFile.Count == 0)
                return Array.Empty<int>();

            // Icon fonts keep their pictures in the private use areas. The rest of this font is
            // Cascadia Code's letters and punctuation, which have no place in an icon picker.
            var icons = new List<int>(fromFile.Count);
            foreach (int codePoint in fromFile)
            {
                if ((codePoint >= 0xE000 && codePoint <= 0xF8FF)
                    || (codePoint >= 0xF0000 && codePoint <= 0xFFFFD)
                    || (codePoint >= 0x100000 && codePoint <= 0x10FFFD))
                {
                    icons.Add(codePoint);
                }
            }

            return icons;
        }
    }
}
