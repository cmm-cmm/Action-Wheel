using System.Collections.Generic;
using Microsoft.UI.Input;
using Windows.System;
using Windows.UI.Core;

namespace Action_Wheel.Settings
{
    /// <summary>
    /// Turns a key press into the shortcut text <see cref="Services.ActionDispatcher"/> understands.
    /// </summary>
    /// <remarks>
    /// The modifiers are read from the live keyboard state rather than from the KeyRoutedEventArgs:
    /// a chord arrives as separate KeyDown events, so when the non-modifier key finally lands the
    /// event itself carries no record of Ctrl or Shift still being held.
    ///
    /// The Windows key cannot be captured this way. The shell takes it before any app sees it, so
    /// "Win+..." has to be typed by hand - the settings window says so next to the recorder.
    /// </remarks>
    public static class ShortcutRecorder
    {
        /// <summary>True for keys that are only ever part of a chord, never the whole shortcut.</summary>
        public static bool IsModifier(VirtualKey key) => key switch
        {
            VirtualKey.Control or VirtualKey.LeftControl or VirtualKey.RightControl => true,
            VirtualKey.Menu or VirtualKey.LeftMenu or VirtualKey.RightMenu => true,
            VirtualKey.Shift or VirtualKey.LeftShift or VirtualKey.RightShift => true,
            VirtualKey.LeftWindows or VirtualKey.RightWindows => true,
            _ => false,
        };

        /// <summary>
        /// Builds "Ctrl+Shift+S" from the key plus whatever modifiers are down right now.
        /// Returns null when the key has no name this app can send back.
        /// </summary>
        public static string? Capture(VirtualKey key)
        {
            var name = KeyName(key);
            if (name == null)
                return null;

            var parts = new List<string>(4);

            if (IsDown(VirtualKey.Control)) parts.Add("Ctrl");
            if (IsDown(VirtualKey.Menu)) parts.Add("Alt");
            if (IsDown(VirtualKey.Shift)) parts.Add("Shift");

            parts.Add(name);
            return string.Join("+", parts);
        }

        private static bool IsDown(VirtualKey key) =>
            InputKeyboardSource.GetKeyStateForCurrentThread(key).HasFlag(CoreVirtualKeyStates.Down);

        /// <summary>Name of a key as written in actions.json, or null if it is not supported.</summary>
        private static string? KeyName(VirtualKey key)
        {
            if (key >= VirtualKey.A && key <= VirtualKey.Z)
                return key.ToString();

            if (key >= VirtualKey.Number0 && key <= VirtualKey.Number9)
                return ((char)('0' + (key - VirtualKey.Number0))).ToString();

            if (key >= VirtualKey.NumberPad0 && key <= VirtualKey.NumberPad9)
                return ((char)('0' + (key - VirtualKey.NumberPad0))).ToString();

            if (key >= VirtualKey.F1 && key <= VirtualKey.F24)
                return "F" + (key - VirtualKey.F1 + 1);

            return key switch
            {
                VirtualKey.Tab => "Tab",
                VirtualKey.Enter => "Enter",
                VirtualKey.Space => "Space",
                VirtualKey.Back => "Backspace",
                VirtualKey.Delete => "Delete",
                VirtualKey.Insert => "Insert",
                VirtualKey.Home => "Home",
                VirtualKey.End => "End",
                VirtualKey.PageUp => "PageUp",
                VirtualKey.PageDown => "PageDown",
                VirtualKey.Left => "Left",
                VirtualKey.Up => "Up",
                VirtualKey.Right => "Right",
                VirtualKey.Down => "Down",
                VirtualKey.Snapshot => "PrintScreen",
                _ => null,
            };
        }
    }
}
