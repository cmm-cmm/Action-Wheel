using System;
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Xaml;

namespace Action_Wheel.Services
{
    /// <summary>
    /// The application icon, read back out of the running executable's own resources.
    /// </summary>
    /// <remarks>
    /// The icon is embedded by <c>&lt;ApplicationIcon&gt;</c> in the .csproj, and read from
    /// <see cref="Environment.ProcessPath"/> rather than from a .ico file next to the exe: a
    /// single-file publish has no such file, so a path-based lookup would silently fall back to
    /// the generic icon exactly in the shipping configuration.
    ///
    /// The handles are loaded once and never destroyed. Window title bars and the notification area
    /// keep referencing them for as long as they are shown, so destroying them would blank the icon;
    /// two handles for the lifetime of the process is the cheaper trade.
    /// </remarks>
    public static class AppIcon
    {
        private const int IDI_APPLICATION = 32512;

        private static bool _loaded;
        private static IntPtr _large;
        private static IntPtr _small;

        /// <summary>Icon at the system's large size, for window title bars and Alt+Tab.</summary>
        public static IntPtr Large
        {
            get
            {
                EnsureLoaded();
                return _large;
            }
        }

        /// <summary>Icon at the system's small size, for the notification area.</summary>
        public static IntPtr Small
        {
            get
            {
                EnsureLoaded();
                return _small;
            }
        }

        /// <summary>
        /// Puts the app icon on a window's title bar and its taskbar entry. WinUI 3 does not do
        /// this from the exe resource on its own.
        /// </summary>
        public static void ApplyTo(Window window)
        {
            try
            {
                var handle = Large != IntPtr.Zero ? Large : Small;
                if (handle == IntPtr.Zero)
                    return;

                window.AppWindow.SetIcon(Win32Interop.GetIconIdFromIcon(handle));
            }
            catch (Exception)
            {
                // A missing icon is cosmetic - never let it stop a window from opening.
            }
        }

        private static void EnsureLoaded()
        {
            if (_loaded)
                return;

            _loaded = true;

            try
            {
                var exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath))
                    ExtractIconEx(exePath, 0, out _large, out _small, 1);
            }
            catch (Exception)
            {
            }

            // Falling back keeps the tray icon visible: Shell_NotifyIcon with a null handle adds an
            // entry that cannot be clicked, which is worse than a generic icon.
            if (_small == IntPtr.Zero)
                _small = LoadIcon(IntPtr.Zero, new IntPtr(IDI_APPLICATION));

            if (_large == IntPtr.Zero)
                _large = _small;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern uint ExtractIconEx(string lpszFile, int nIconIndex,
            out IntPtr phiconLarge, out IntPtr phiconSmall, uint nIcons);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);
    }
}
