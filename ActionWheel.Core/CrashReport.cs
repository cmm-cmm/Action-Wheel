using System;
using System.IO;

namespace Action_Wheel.Core
{
    /// <summary>
    /// Keeps the one most recent unhandled exception, so "Copy diagnostics" can say why the app
    /// last closed unexpectedly. Since logging was retired, a crash otherwise left no trace at all.
    /// </summary>
    /// <remarks>
    /// This is not a log. It is written only when the process is already going down, each crash
    /// overwrites the last one, and it holds the exception's own text and nothing about what the
    /// user was doing: no clicks, gestures, actions or profile changes. It stays on this machine
    /// unless someone pastes the diagnostics somewhere.
    /// </remarks>
    public static class CrashReport
    {
        public static string FilePath => Path.Combine(AppDataPaths.DirectoryPath, "last-crash.txt");

        /// <summary>Records <paramref name="exception"/>. Never throws: it runs inside a crash.</summary>
        public static void Write(Exception? exception, string appVersion)
        {
            if (exception == null)
                return;

            try
            {
                // A plain write rather than AtomicFile: the process may be torn down at any moment,
                // and a .bak of the previous crash is not worth the extra steps.
                Directory.CreateDirectory(AppDataPaths.DirectoryPath);
                File.WriteAllText(FilePath,
                    $"Time    : {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}{Environment.NewLine}"
                    + $"Version : {appVersion}{Environment.NewLine}{Environment.NewLine}"
                    + exception);
            }
            catch (Exception)
            {
                // Nothing left to report a failure to.
            }
        }

        /// <summary>The recorded crash, or null if there is none.</summary>
        public static string? TryLoad()
        {
            try
            {
                return File.Exists(FilePath) ? File.ReadAllText(FilePath) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
