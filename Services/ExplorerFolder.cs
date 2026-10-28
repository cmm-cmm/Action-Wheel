using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Action_Wheel.Services
{
    /// <summary>
    /// The folder a File Explorer window is showing: where an AI tool opened from the ring should
    /// start.
    /// </summary>
    /// <remarks>
    /// Read through Shell.Application, the documented way to ask Explorer what its windows show.
    /// It is an STA object and this runs on a thread-pool thread (action dispatch), so the query gets
    /// a short-lived STA thread of its own and a timeout: a busy Explorer must not hold up the
    /// action. The caller falls back to a default folder.
    /// </remarks>
    public static class ExplorerFolder
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

        /// <summary>
        /// The file-system folder <paramref name="window"/> shows, or null. The desktop counts as the
        /// Desktop folder. Virtual locations such as This PC or Home have no path, so they are null.
        /// </summary>
        public static string? Of(IntPtr window)
        {
            if (window == IntPtr.Zero)
                return null;

            string className = ClassNameOf(window);
            if (className is "Progman" or "WorkerW")
                return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

            if (className is not ("CabinetWClass" or "ExploreWClass"))
                return null;

            string title = TitleOf(window);
            string? result = null;
            var thread = new Thread(() => result = Query(window, title)) { IsBackground = true };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            return thread.Join(Timeout) ? result : null;
        }

        /// <remarks>
        /// With tabbed Explorer (Windows 11), every tab of one window reports the same HWND. The
        /// window's title is the active tab's location name, so that picks the tab the user was
        /// looking at. Without a title match, the first tab is the best guess left.
        /// </remarks>
        private static string? Query(IntPtr window, string title)
        {
            object? shell = null;
            try
            {
                var type = Type.GetTypeFromProgID("Shell.Application");
                if (type == null)
                    return null;

                shell = Activator.CreateInstance(type);
                dynamic windows = ((dynamic)shell!).Windows();
                string? firstMatch = null;

                int count = windows.Count;
                for (int i = 0; i < count; i++)
                {
                    try
                    {
                        // ShellWindows.Item returns null for an index that closed in the meantime.
                        object? item = windows.Item(i);
                        if (item is null)
                            continue;

                        dynamic candidate = item;
                        if (Convert.ToInt64(candidate.HWND) != window.ToInt64())
                            continue;

                        string path = candidate.Document.Folder.Self.Path;
                        if (!Directory.Exists(path))
                            continue;

                        if (string.Equals((string)candidate.LocationName, title, StringComparison.OrdinalIgnoreCase))
                            return path;

                        firstMatch ??= path;
                    }
                    catch (Exception)
                    {
                        // An Internet Explorer window or a tab still navigating: not a folder.
                    }
                }

                return firstMatch;
            }
            catch (Exception)
            {
                return null;
            }
            finally
            {
                if (shell != null && Marshal.IsComObject(shell))
                    Marshal.FinalReleaseComObject(shell);
            }
        }

        private static string ClassNameOf(IntPtr window)
        {
            var name = new StringBuilder(64);
            return GetClassName(window, name, name.Capacity) > 0 ? name.ToString() : string.Empty;
        }

        private static string TitleOf(IntPtr window)
        {
            var title = new StringBuilder(512);
            return GetWindowText(window, title, title.Capacity) > 0 ? title.ToString() : string.Empty;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr window, StringBuilder name, int capacity);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr window, StringBuilder text, int capacity);
    }
}
