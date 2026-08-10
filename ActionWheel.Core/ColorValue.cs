using System;
using System.Globalization;

namespace Action_Wheel.Core
{
    /// <summary>
    /// A colour as it appears in actions.json, independent of any UI framework's Color type.
    /// </summary>
    public readonly record struct ColorValue(byte A, byte R, byte G, byte B)
    {
        /// <summary>Parses "#RRGGBB" or "#AARRGGBB". Returns false for empty or malformed input.</summary>
        public static bool TryParse(string? value, out ColorValue color)
        {
            color = default;

            if (string.IsNullOrWhiteSpace(value))
                return false;

            var text = value.Trim().TrimStart('#');

            // Six digits mean the user wrote an opaque colour; the file format is ARGB throughout,
            // so fill the alpha in rather than treating the shorter form as a different notation.
            if (text.Length == 6)
                text = "FF" + text;

            if (text.Length != 8
                || !uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint argb))
            {
                return false;
            }

            color = new ColorValue(
                (byte)((argb >> 24) & 0xFF),
                (byte)((argb >> 16) & 0xFF),
                (byte)((argb >> 8) & 0xFF),
                (byte)(argb & 0xFF));

            return true;
        }

        /// <summary>True when the field is either left blank or holds a colour this app understands.</summary>
        public static bool IsValidOrEmpty(string? value) =>
            string.IsNullOrWhiteSpace(value) || TryParse(value, out _);

        public string ToHex() => $"#{A:X2}{R:X2}{G:X2}{B:X2}";

        /// <summary>Moves a colour towards white. Used for the hover state of a filled button.</summary>
        public ColorValue Lighten(double amount) =>
            new(A, Mix(R, 255, amount), Mix(G, 255, amount), Mix(B, 255, amount));

        /// <summary>Moves a colour towards black. Used for the pressed state.</summary>
        public ColorValue Darken(double amount) =>
            new(A, Mix(R, 0, amount), Mix(G, 0, amount), Mix(B, 0, amount));

        private static byte Mix(byte from, int to, double amount) =>
            (byte)Math.Clamp(from + (to - from) * amount, 0, 255);
    }
}
