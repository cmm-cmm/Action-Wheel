using System;
using System.Globalization;
using System.IO;

namespace Action_Wheel.Services
{
    /// <summary>
    /// Persists when Action Wheel last asked GitHub for its latest release, so a machine that
    /// restarts often (or crash-loops) does not hammer the API on every launch.
    /// </summary>
    public static class UpdateCheckSettings
    {
        private static string FilePath => Path.Combine(AppDataPaths.DirectoryPath, "update-check.txt");

        public static DateTime? LoadLastCheckedUtc()
        {
            try
            {
                if (!File.Exists(FilePath))
                    return null;

                string text = File.ReadAllText(FilePath).Trim();
                return DateTime.TryParse(text, CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind | DateTimeStyles.AssumeUniversal, out var value)
                    ? value
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static void SaveLastCheckedUtc(DateTime utc) =>
            AtomicFile.TryWriteText(FilePath, utc.ToString("o", CultureInfo.InvariantCulture), out _);
    }
}
