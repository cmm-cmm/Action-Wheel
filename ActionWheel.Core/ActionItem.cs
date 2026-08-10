namespace Action_Wheel.Core
{
    /// <summary>What an <see cref="ActionItem"/> does when its button is clicked.</summary>
    public enum ActionKind
    {
        /// <summary>Do nothing beyond closing the menu.</summary>
        None,
        /// <summary>Send a keyboard shortcut to whatever window is in the foreground.</summary>
        Keys,
        /// <summary>Start a program, open a file/folder, or follow a URI.</summary>
        Launch,
        /// <summary>Run one of Action Wheel's built-in Windows functions.</summary>
        Function,
    }

    /// <summary>One button of the radial menu.</summary>
    public sealed record ActionItem
    {
        public const double DefaultIconScale = 1;
        public const bool DefaultIconTint = true;
        public const bool DefaultShadowEnabled = true;
        public const double DefaultShadowOpacity = 0.45;
        public const double DefaultShadowBlur = 11;
        public const double DefaultShadowOffsetX = 0;
        public const double DefaultShadowOffsetY = 3;
        /// <summary>0 = the center button, 1-8 = the outer buttons clockwise from the top.</summary>
        public int Tag { get; init; }

        /// <summary>Tooltip text.</summary>
        public string Label { get; init; } = string.Empty;

        public ActionKind Kind { get; init; } = ActionKind.None;

        /// <summary>Shortcut ("Ctrl+Shift+S") for Keys, or a path/URI for Launch.</summary>
        public string Value { get; init; } = string.Empty;

        /// <summary>Command-line arguments passed to a Launch target. Ignored for other kinds.</summary>
        public string Arguments { get; init; } = string.Empty;

        /// <summary>
        /// Icon-font code point in hex, e.g. "F0C5". Five digits are allowed: two thirds of the
        /// font's icons live above U+FFFF.
        /// </summary>
        /// <remarks>
        /// The code is only meaningful against the font the app ships. The same value draws
        /// something else entirely in the system icon font, so a config moved between versions that
        /// use different fonts will not survive - which is why the font travels with the app.
        /// </remarks>
        public string Glyph { get; init; } = string.Empty;

        /// <summary>
        /// Path to an image file to draw instead of <see cref="Glyph"/>. Takes precedence when set.
        /// See <see cref="IconFile"/> for the formats that can be drawn.
        /// </summary>
        public string IconPath { get; init; } = string.Empty;
        /// <summary>Display-size multiplier shared by glyph, SVG, PNG and ICO icons.</summary>
        public double IconScale { get; init; } = DefaultIconScale;
        public double IconOffsetX { get; init; }
        public double IconOffsetY { get; init; }

        /// <summary>
        /// Recolours a vector icon to <see cref="Foreground"/> instead of drawing it in the colours
        /// it was authored with.
        /// </summary>
        /// <remarks>
        /// On by default, because the common case is a single-colour pictogram that should match the
        /// rest of the ring. Turn it off for a logo whose colours carry meaning. It has no effect on
        /// a raster icon - a PNG extracted from an application keeps that application's colours.
        /// </remarks>
        public bool IconTint { get; init; } = DefaultIconTint;

        /// <summary>Icon colour as "#RRGGBB"/"#AARRGGBB". Empty means the built-in colour.</summary>
        /// <remarks>Applies to a glyph, and to a vector icon when <see cref="IconTint"/> is on.</remarks>
        public string Foreground { get; init; } = string.Empty;

        /// <summary>Button fill colour as "#RRGGBB"/"#AARRGGBB". Empty means the built-in colour.</summary>
        public string Background { get; init; } = string.Empty;

        /// <summary>Drop-shadow colour. Empty means the built-in black shadow.</summary>
        public string Shadow { get; init; } = string.Empty;
        public bool ShadowEnabled { get; init; } = DefaultShadowEnabled;
        public double ShadowOpacity { get; init; } = DefaultShadowOpacity;
        public double ShadowBlur { get; init; } = DefaultShadowBlur;
        public double ShadowOffsetX { get; init; } = DefaultShadowOffsetX;
        public double ShadowOffsetY { get; init; } = DefaultShadowOffsetY;
    }
}
