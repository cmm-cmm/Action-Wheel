using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Action_Wheel.Services
{
    /// <summary>
    /// Keeps one copy of the app running per user session, and lets a second launch hand its intent
    /// over to the copy that is already running.
    /// </summary>
    /// <remarks>
    /// Two instances would each install a global mouse hook - so a middle click would open two
    /// overlays - each add a tray icon, and each write actions.json from its own settings window,
    /// silently overwriting the other.
    ///
    /// The hand-off goes to the tray icon's message-only window, found by class name. A broadcast
    /// would be simpler but does not work: HWND_MESSAGE windows are excluded from broadcasts, which
    /// is exactly the kind of window used here.
    /// </remarks>
    public static class SingleInstance
    {
        /// <summary>Per-session, per-user. Two users signed in at once each get their own app.</summary>
        private const string MutexName = @"Local\ActionWheel.SingleInstance";

        /// <summary>Class name of the tray icon's message-only window - the hand-off target.</summary>
        public const string MessageWindowClass = "ActionWheelTrayWindow";

        // Held for the lifetime of the process; releasing it would let a second instance start.
        private static Mutex? _mutex;

        /// <summary>
        /// True if this process is the first instance. False means another one is already running.
        /// </summary>
        public static bool TryAcquire()
        {
            try
            {
                _mutex = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);

                if (!createdNew)
                {
                    _mutex.Dispose();
                    _mutex = null;
                }

                return createdNew;
            }
            catch (Exception)
            {
                // If the mutex cannot be created, running is better than refusing to start.
                return true;
            }
        }

        /// <summary>
        /// The window message both instances agree on. Registered by name, so the id is the same in
        /// every process without either having to pick a number. Zero means the registration failed
        /// - callers must treat that as "no message", because zero is WM_NULL, which the tray posts
        /// to its own window after every menu dismissal. Comparing against an unchecked zero here
        /// would turn every closed tray menu into a request to open Settings.
        /// </summary>
        public static uint ShowSettingsMessage { get; } = RegisterWindowMessage("ActionWheel.ShowSettings");

        /// <summary>False when the message could not be registered, so nothing should compare against it.</summary>
        public static bool IsShowSettingsMessageValid => ShowSettingsMessage != 0;

        /// <summary>
        /// Asks the already-running instance to open its settings window.
        /// Returns false if it could not be reached.
        /// </summary>
        public static bool AskRunningInstanceToShowSettings()
        {
            if (!IsShowSettingsMessageValid)
            {
                return false;
            }

            try
            {
                var target = FindWindowEx(HWND_MESSAGE, IntPtr.Zero, MessageWindowClass, null);
                if (target == IntPtr.Zero)
                {
                    return false;
                }

                return PostMessage(target, ShowSettingsMessage, IntPtr.Zero, IntPtr.Zero);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static void Release()
        {
            _mutex?.Dispose();
            _mutex = null;
        }

        private static readonly IntPtr HWND_MESSAGE = new(-3);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr childAfter, string? className, string? windowName);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern uint RegisterWindowMessage(string lpString);
    }
}
