using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Action_Wheel.Core
{
    /// <summary>
    /// A prompt an AI task button sends, with the selected text in place of
    /// <see cref="AiTaskTemplates.Placeholder"/>. See Doc/ai-assistants.md, phase 2.
    /// </summary>
    /// <param name="Id">Stable slug, part of the button's Function id. Renaming the template keeps it.</param>
    /// <param name="Confirm">Ask before the text leaves the machine. On by default.</param>
    public sealed record AiTaskTemplate(string Id, string Name, string Prompt, bool Confirm = true);

    /// <summary>
    /// The prompt templates, in <c>ai-tasks.json</c>. They are the user's own writing, so unlike the
    /// tool switches they travel in a backup.
    /// </summary>
    public static class AiTaskTemplates
    {
        public const string FileName = "ai-tasks.json";
        public const string Placeholder = "{selection}";

        /// <summary>
        /// More text than any selection someone means to send in one go. Past it the prompt is refused
        /// rather than cut: a truncated document gets a confident answer about half of it.
        /// </summary>
        public const int MaxSelectionLength = 100_000;

        private static readonly Regex IdPattern = new("^[a-z0-9][a-z0-9-]{0,47}$", RegexOptions.Compiled);

        public static string FilePath => Path.Combine(AppDataPaths.DirectoryPath, FileName);

        public static IReadOnlyList<AiTaskTemplate> Defaults() => new[]
        {
            new AiTaskTemplate("explain", "Explain",
                "Explain the following text clearly and briefly. Answer in the same language as the text.\n\n" + Placeholder),
            new AiTaskTemplate("fix-grammar", "Fix spelling and grammar",
                "Correct the spelling and grammar of the following text. Keep its meaning, tone and language. "
                + "Reply with the corrected text only, with no comments.\n\n" + Placeholder),
            new AiTaskTemplate("translate-english", "Translate to English",
                "Translate the following text into English. Reply with the translation only.\n\n" + Placeholder),
            new AiTaskTemplate("summarize", "Summarize",
                "Summarize the following text in a few short bullet points, in the same language as the text.\n\n" + Placeholder),
        };

        /// <summary>True for an id a template can have, and so a Function id can carry.</summary>
        public static bool IsValidId(string? id) => id != null && IdPattern.IsMatch(id);

        /// <summary>
        /// A new id from <paramref name="name"/>, unique among <paramref name="existing"/>. Only
        /// a-z, 0-9 and '-', so it reads well inside actions.json and needs no escaping there.
        /// </summary>
        public static string NewId(string name, IEnumerable<string> existing)
        {
            var taken = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);

            var slug = new StringBuilder();
            foreach (char c in (name ?? string.Empty).ToLowerInvariant())
            {
                if (c is >= 'a' and <= 'z' or >= '0' and <= '9')
                    slug.Append(c);
                else if (slug.Length > 0 && slug[^1] != '-')
                    slug.Append('-');
            }

            string stem = slug.ToString().Trim('-');
            if (stem.Length == 0)
                stem = "task";
            if (stem.Length > 40)
                stem = stem[..40].TrimEnd('-');

            string id = stem;
            for (int n = 2; taken.Contains(id); n++)
                id = $"{stem}-{n}";
            return id;
        }

        /// <summary>
        /// The prompt for <paramref name="selection"/>: every placeholder replaced, or the text appended
        /// after a blank line when the template has none, so a template can never send nothing.
        /// </summary>
        public static string BuildPrompt(AiTaskTemplate template, string selection)
        {
            string prompt = template.Prompt ?? string.Empty;
            return prompt.Contains(Placeholder, StringComparison.Ordinal)
                ? prompt.Replace(Placeholder, selection, StringComparison.Ordinal)
                : $"{prompt.TrimEnd()}\n\n{selection}";
        }

        /// <summary>The saved templates, or the defaults when none were saved or the file cannot be read.</summary>
        public static IReadOnlyList<AiTaskTemplate> Load()
        {
            try
            {
                return File.Exists(FilePath) ? Parse(File.ReadAllText(FilePath)) : Defaults();
            }
            catch (Exception)
            {
                return Defaults();
            }
        }

        public static bool Save(IEnumerable<AiTaskTemplate> templates, out string error) =>
            AtomicFile.TryWriteText(FilePath, ToJson(templates), out error);

        /// <summary>Parses the file. Throws <see cref="FormatException"/> on a malformed one.</summary>
        public static IReadOnlyList<AiTaskTemplate> Parse(string json)
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("templates", out var list) || list.ValueKind != JsonValueKind.Array)
                throw new FormatException("ai-tasks.json must have a \"templates\" array.");

            var templates = new List<AiTaskTemplate>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in list.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                    throw new FormatException("Every template must be an object.");

                string id = ReadString(item, "id");
                if (!IsValidId(id))
                    throw new FormatException($"'{id}' is not a valid template id (a-z, 0-9 and '-' only).");
                if (!seen.Add(id))
                    throw new FormatException($"The template id '{id}' appears more than once.");

                templates.Add(new AiTaskTemplate(
                    id,
                    ReadString(item, "name") is { Length: > 0 } name ? name : id,
                    ReadString(item, "prompt"),
                    !item.TryGetProperty("confirm", out var confirm) || confirm.ValueKind != JsonValueKind.False));
            }

            return templates;
        }

        public static string ToJson(IEnumerable<AiTaskTemplate> templates)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
            {
                Indented = true,
                // Same reason as actions.json: prompts in other languages stay readable in the file.
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            }))
            {
                writer.WriteStartObject();
                writer.WriteStartArray("templates");
                foreach (var template in templates)
                {
                    writer.WriteStartObject();
                    writer.WriteString("id", template.Id);
                    writer.WriteString("name", template.Name);
                    writer.WriteString("prompt", template.Prompt);
                    writer.WriteBoolean("confirm", template.Confirm);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }

        public static AiTaskTemplate? Find(IEnumerable<AiTaskTemplate> templates, string id) =>
            templates.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase));

        private static string ReadString(JsonElement element, string name) =>
            element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? string.Empty
                : string.Empty;
    }
}
