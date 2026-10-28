using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Action_Wheel.Services
{
    /// <summary>
    /// Which AI tools the user has switched off in Settings, in <c>ai-assistants.json</c>.
    /// </summary>
    /// <remarks>
    /// Stored as the switched-off set rather than the switched-on one, so every tool starts enabled
    /// and a tool added to the catalog later is enabled too. Like "Start hidden", it describes this
    /// machine, which tools are installed here, so it is not part of a backup.
    /// </remarks>
    public static class AiAssistantSettings
    {
        private static string FilePath => Path.Combine(AppDataPaths.DirectoryPath, "ai-assistants.json");

        public static HashSet<string> LoadDisabled()
        {
            var disabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (!File.Exists(FilePath))
                    return disabled;

                using var document = JsonDocument.Parse(File.ReadAllText(FilePath));
                if (document.RootElement.TryGetProperty("disabled", out var list) && list.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in list.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.String && item.GetString() is string id && id.Length > 0)
                            disabled.Add(id);
                    }
                }
            }
            catch (Exception)
            {
                // Unreadable means nothing is switched off: the tools still have to be installed
                // to show up, so this cannot surface anything absent.
            }

            return disabled;
        }

        public static bool IsEnabled(string toolId) => !LoadDisabled().Contains(toolId);

        public static bool SetEnabled(string toolId, bool enabled, out string error)
        {
            var disabled = LoadDisabled();
            if (enabled)
                disabled.Remove(toolId);
            else
                disabled.Add(toolId);

            try
            {
                using var stream = new MemoryStream();
                using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
                {
                    writer.WriteStartObject();
                    writer.WriteStartArray("disabled");
                    foreach (string id in disabled)
                        writer.WriteStringValue(id);
                    writer.WriteEndArray();
                    writer.WriteEndObject();
                }

                return AtomicFile.TryWriteText(FilePath, Encoding.UTF8.GetString(stream.ToArray()), out error);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }
}
