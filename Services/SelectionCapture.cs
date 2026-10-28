using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Action_Wheel.Services
{
    /// <summary>
    /// Gets the text the user had selected when the ring opened, for an AI task.
    /// </summary>
    /// <remarks>
    /// No API reads "the selection" out of an arbitrary window, so this does what a person would:
    /// press Ctrl+C in that window and read the clipboard once it changes. If it does not change,
    /// nothing was selected, and the text already on the clipboard is offered instead. The caller
    /// then always asks before sending it, because the user may not remember what is there.
    ///
    /// A terminal is never sent Ctrl+C. There it means "interrupt", and it would stop whatever the
    /// user was running. Terminals copy on selection or with their own shortcut, so the clipboard
    /// is used as is.
    ///
    /// Runs on a worker thread. The clipboard is read through Win32 rather than the WinRT
    /// Clipboard class, which wants the UI thread.
    /// </remarks>
    public static class SelectionCapture
    {
        private static readonly TimeSpan CopyTimeout = TimeSpan.FromMilliseconds(900);
        private static readonly TimeSpan ModifierTimeout = TimeSpan.FromSeconds(1);

        /// <param name="FromSelection">
        /// True when the text came from the selection. False when it is whatever was already on the
        /// clipboard.
        /// </param>
        public sealed record Result(string Text, bool FromSelection);

        public static Result Capture(IntPtr target)
        {
            if (target != IntPtr.Zero && !IsTerminal(target))
            {
                // The emergency trigger is Ctrl+Alt+Space. If those keys are still physically held,
                // a synthetic Ctrl+C would arrive as Ctrl+Alt+C, so wait for them to be released.
                WaitForModifiersReleased();

                uint before = GetClipboardSequenceNumber();
                ActionDispatcher.SendKeys("Ctrl+C", "Copy the selection");

                var deadline = DateTime.UtcNow + CopyTimeout;
                while (DateTime.UtcNow < deadline)
                {
                    if (GetClipboardSequenceNumber() != before)
                    {
                        // Some apps write the clipboard in several steps; give them a moment to finish.
                        Thread.Sleep(30);
                        string copied = ReadClipboardText();
                        if (copied.Length > 0)
                            return new Result(copied, FromSelection: true);
                        break;
                    }

                    Thread.Sleep(20);
                }
            }

            return new Result(ReadClipboardText(), FromSelection: false);
        }

        private static void WaitForModifiersReleased()
        {
            var deadline = DateTime.UtcNow + ModifierTimeout;
            while (DateTime.UtcNow < deadline && (IsDown(0x11) || IsDown(0x12) || IsDown(0x10) || IsDown(0x5B) || IsDown(0x5C)))
                Thread.Sleep(20);
        }

        private static bool IsDown(int virtualKey) => (GetAsyncKeyState(virtualKey) & 0x8000) != 0;

        /// <summary>Console host, Windows Terminal, the pseudo-console, and mintty (Git Bash, Cygwin).</summary>
        private static bool IsTerminal(IntPtr window)
        {
            var name = new StringBuilder(64);
            string className = GetClassName(window, name, name.Capacity) > 0 ? name.ToString() : string.Empty;
            return className is "ConsoleWindowClass" or "CASCADIA_HOSTING_WINDOW_CLASS"
                or "PseudoConsoleWindow" or "mintty" or "VirtualConsoleClass";
        }

        /// <summary>The clipboard's Unicode text, or empty. Retries briefly: another app may hold it open.</summary>
        public static string ReadClipboardText()
        {
            for (int attempt = 0; attempt < 10; attempt++)
            {
                if (!OpenClipboard(IntPtr.Zero))
                {
                    Thread.Sleep(20);
                    continue;
                }

                try
                {
                    IntPtr handle = GetClipboardData(CF_UNICODETEXT);
                    if (handle == IntPtr.Zero)
                        return string.Empty;

                    IntPtr pointer = GlobalLock(handle);
                    if (pointer == IntPtr.Zero)
                        return string.Empty;

                    try
                    {
                        return Marshal.PtrToStringUni(pointer) ?? string.Empty;
                    }
                    finally
                    {
                        GlobalUnlock(handle);
                    }
                }
                finally
                {
                    CloseClipboard();
                }
            }

            return string.Empty;
        }

        private const uint CF_UNICODETEXT = 13;

        [DllImport("user32.dll")]
        private static extern uint GetClipboardSequenceNumber();

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool OpenClipboard(IntPtr owner);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseClipboard();

        [DllImport("user32.dll")]
        private static extern IntPtr GetClipboardData(uint format);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GlobalLock(IntPtr memory);

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalUnlock(IntPtr memory);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int virtualKey);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr window, StringBuilder name, int capacity);
    }
}
