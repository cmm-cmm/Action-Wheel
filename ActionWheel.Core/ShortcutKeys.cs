using System;
using System.Collections.Generic;

namespace Action_Wheel.Core
{
    /// <summary>
    /// Turns a shortcut written as text ("Ctrl+Shift+S") into the virtual-key codes to press.
    /// </summary>
    /// <remarks>
    /// Split out from the code that actually presses them: SendInput needs a desktop session and a
    /// foreground window, while deciding <em>which</em> keys a string means is arithmetic on a
    /// lookup table. Keeping the two apart is what lets the settings window validate what the user
    /// typed without synthesising a single keystroke.
    /// </remarks>
    public static class ShortcutKeys
    {
        /// <summary>
        /// The virtual-key codes for <paramref name="shortcut"/>, in press order (modifiers first,
        /// the way the user wrote them). Returns an empty list if <em>any</em> part is unrecognised:
        /// sending half a chord is worse than sending nothing, because "Ctrl+Shift+Q" degrading to
        /// "Ctrl+Shift" is silent while nothing happening is at least visible.
        /// </summary>
        /// <param name="unknownKey">
        /// The first part that could not be mapped, or empty when the parse succeeded. The caller
        /// logs it - "unknown key 'Cmd'" is the one message that makes a dead button explainable.
        /// </param>
        public static IReadOnlyList<ushort> Parse(string shortcut, out string unknownKey)
        {
            unknownKey = string.Empty;

            var result = new List<ushort>();
            if (string.IsNullOrWhiteSpace(shortcut))
                return result;

            // "+" both separates keys and names one, so "Ctrl++" means Ctrl plus the Plus key.
            // Every other empty part ("Ctrl+", "+A", "Ctrl++S") is a mistake, and it is rejected.
            // Empty parts used to be dropped instead, so "Ctrl++" passed validation and then sent
            // Ctrl alone.
            string text = shortcut.Trim();
            if (text == "+")
                text = "Plus";
            else if (text.EndsWith("++", StringComparison.Ordinal))
                text = text[..^1] + "Plus";

            foreach (var raw in text.Split('+'))
            {
                var name = raw.Trim();
                if (name.Length == 0)
                {
                    unknownKey = MissingKey;
                    return new List<ushort>();
                }

                var vk = ToVirtualKey(name);
                if (vk == 0)
                {
                    unknownKey = name;
                    return new List<ushort>();
                }

                result.Add(vk);
            }

            return result;
        }

        /// <summary>What <see cref="Parse(string, out string)"/> reports for a "+" with no key beside it.</summary>
        public const string MissingKey = "(nothing between two +)";

        /// <inheritdoc cref="Parse(string, out string)"/>
        public static IReadOnlyList<ushort> Parse(string shortcut) => Parse(shortcut, out _);

        /// <summary>
        /// True if every part of the shortcut maps to a virtual key, i.e. pressing it would actually
        /// do something. Used by the settings window to flag typos before saving.
        /// </summary>
        public static bool IsValid(string shortcut) => Parse(shortcut).Count > 0;

        /// <summary>
        /// Keys that need KEYEVENTF_EXTENDEDKEY. Without the flag the navigation cluster is
        /// indistinguishable from the numeric keypad, and Windows ignores a Win key sent without it.
        /// </summary>
        public static bool IsExtendedKey(ushort vk) => vk switch
        {
            0x5B or 0x5C => true,                       // Left/Right Windows
            0x21 or 0x22 or 0x23 or 0x24 => true,       // PageUp/PageDown/End/Home
            0x25 or 0x26 or 0x27 or 0x28 => true,       // Arrows
            0x2D or 0x2E => true,                       // Insert/Delete
            >= 0xA6 and <= 0xB7 => true,                // Browser and media keys
            _ => false,
        };

        /// <summary>The virtual-key code for one key name, or 0 when the name is not recognised.</summary>
        public static ushort ToVirtualKey(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return 0;

            name = name.Trim();

            if (name.Length == 1)
            {
                char c = char.ToUpperInvariant(name[0]);
                if (c is >= 'A' and <= 'Z' or >= '0' and <= '9')
                    return c;

                ushort punctuation = PunctuationKey(c);
                if (punctuation != 0)
                    return punctuation;
            }

            if (name.Length is 2 or 3 && (name[0] == 'F' || name[0] == 'f')
                && int.TryParse(name.AsSpan(1), out int fn) && fn is >= 1 and <= 24)
            {
                return (ushort)(0x70 + fn - 1); // VK_F1 = 0x70
            }

            return name.ToLowerInvariant() switch
            {
                "ctrl" or "control" => 0x11,
                "alt" => 0x12,
                "shift" => 0x10,
                "win" or "windows" or "lwin" => 0x5B,
                "esc" or "escape" => 0x1B,
                "tab" => 0x09,
                "enter" or "return" => 0x0D,
                "space" => 0x20,
                "backspace" => 0x08,
                "delete" or "del" => 0x2E,
                "insert" or "ins" => 0x2D,
                "home" => 0x24,
                "end" => 0x23,
                "pageup" or "pgup" => 0x21,
                "pagedown" or "pgdn" => 0x22,
                "left" => 0x25,
                "up" => 0x26,
                "right" => 0x27,
                "down" => 0x28,
                "printscreen" or "prtsc" => 0x2C,
                "volumeup" => 0xAF,
                "volumedown" => 0xAE,
                "volumemute" or "mute" => 0xAD,
                "medianext" => 0xB0,
                "mediaprev" or "mediaprevious" => 0xB1,
                "mediastop" => 0xB2,
                "mediaplaypause" or "playpause" => 0xB3,
                "plus" or "equals" => 0xBB,
                "minus" => 0xBD,
                "comma" => 0xBC,
                "period" => 0xBE,
                "slash" => 0xBF,
                "backtick" or "grave" => 0xC0,
                "semicolon" => 0xBA,
                "quote" => 0xDE,
                "openbracket" => 0xDB,
                "closebracket" => 0xDD,
                "backslash" => 0xDC,
                _ => 0,
            };
        }

        /// <summary>
        /// The punctuation keys, typed as the character on the key itself. Both characters on a
        /// key name the same key, so "Ctrl+=" and "Ctrl+Plus" are one shortcut; Shift is a separate
        /// part of the chord. The codes are the VK_OEM_* positions of a US layout. On other layouts
        /// the same position can carry different characters, which is how Windows itself treats
        /// these keys.
        /// </summary>
        private static ushort PunctuationKey(char c) => c switch
        {
            ';' or ':' => 0xBA,
            '=' => 0xBB,
            ',' or '<' => 0xBC,
            '-' or '_' => 0xBD,
            '.' or '>' => 0xBE,
            '/' or '?' => 0xBF,
            '`' or '~' => 0xC0,
            '[' or '{' => 0xDB,
            '\\' or '|' => 0xDC,
            ']' or '}' => 0xDD,
            '\'' or '"' => 0xDE,
            _ => 0,
        };
    }
}
