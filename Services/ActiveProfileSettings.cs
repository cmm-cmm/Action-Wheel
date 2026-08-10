using System;
using System.IO;

namespace Action_Wheel.Services
{
    /// <summary>Persists the name of the profile most recently applied to actions.json.</summary>
    public static class ActiveProfileSettings
    {
        private const string DefaultName = "Main configuration";
        private static string FilePath => Path.Combine(AppDataPaths.DirectoryPath, "active-profile.txt");

        public static string Load()
        {
            try
            {
                if (!File.Exists(FilePath))
                    return DefaultName;

                string name = File.ReadAllText(FilePath).Trim();
                return string.IsNullOrWhiteSpace(name) ? DefaultName : name;
            }
            catch (Exception)
            {
                return DefaultName;
            }
        }

        public static bool Save(string? name, out string error)
        {
            string content = string.IsNullOrWhiteSpace(name) ? DefaultName : name.Trim();
            if (!AtomicFile.TryWriteText(FilePath, content, out error))
            {
                return false;
            }
            return true;
        }
    }
}
