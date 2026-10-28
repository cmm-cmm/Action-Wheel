using System;
using System.Collections.Generic;

namespace Action_Wheel.Core
{
    /// <summary>How an AI tool is started.</summary>
    public enum AiToolKind
    {
        /// <summary>A command-line tool, started in a terminal.</summary>
        Cli,
        /// <summary>A desktop application or IDE.</summary>
        App,
    }

    /// <summary>
    /// One AI tool Action Wheel knows how to find and start. Pure data: finding it on a machine is
    /// <c>Services/AiToolDetector</c>'s job, because that needs the registry.
    /// </summary>
    /// <param name="Commands">Bare command names, resolved through PATH and App Paths.</param>
    /// <param name="KnownPaths">Install locations, with %VARIABLES% expanded at detection time.</param>
    /// <param name="Protocols">URL schemes whose shell\open\command names the tool's executable.</param>
    /// <param name="OpensFolder">An IDE: the current folder is passed to it as an argument.</param>
    /// <param name="SupportsTasks">
    /// Can run a one-shot task on the selected text with a verified safe command line (see
    /// <see cref="AiTools.TaskArguments"/>). Only tools whose "no tools / read-only" mode was checked
    /// against their own --help qualify.
    /// </param>
    public sealed record AiTool(
        string Id,
        string Name,
        AiToolKind Kind,
        IReadOnlyList<string> Commands,
        IReadOnlyList<string> KnownPaths,
        IReadOnlyList<string> Protocols,
        bool OpensFolder = false,
        bool SupportsTasks = false);

    /// <summary>
    /// The AI tools a ring button can reach, and the Function ids that name them
    /// (<c>action-wheel.ai:&lt;tool&gt;/&lt;task&gt;</c>). See Doc/ai-assistants.md.
    /// </summary>
    /// <remarks>
    /// The valid ids are decided by this static list, never by what is installed. Validation also
    /// gates loading actions.json, so an id that became invalid when a tool was uninstalled would
    /// reject every button. An absent tool is reported when its button is clicked instead.
    /// </remarks>
    public static class AiTools
    {
        public const string Prefix = "action-wheel.ai:";

        /// <summary>Starts the tool: CLIs in a terminal at the current folder, apps as themselves.</summary>
        public const string OpenTask = "open";

        private static readonly string[] None = Array.Empty<string>();

        public static IReadOnlyList<AiTool> All { get; } = new[]
        {
            new AiTool("claude", "Claude Code", AiToolKind.Cli, new[] { "claude" }, None, None, SupportsTasks: true),
            new AiTool("codex", "Codex CLI", AiToolKind.Cli, new[] { "codex" }, None, None, SupportsTasks: true),
            // No tasks: its built-in agents, plan included, start from "permission *: allow", and no
            // read-only mode could be verified.
            new AiTool("opencode", "OpenCode", AiToolKind.Cli, new[] { "opencode" }, None, None),
            new AiTool("gemini", "Gemini CLI", AiToolKind.Cli, new[] { "gemini" }, None, None),
            new AiTool("copilot", "GitHub Copilot CLI", AiToolKind.Cli, new[] { "copilot" }, None, None),
            new AiTool("antigravity", "Antigravity", AiToolKind.App, new[] { "antigravity" },
                new[] { @"%LOCALAPPDATA%\Programs\Antigravity\Antigravity.exe" },
                new[] { "antigravity" }, OpensFolder: true),
            new AiTool("cursor", "Cursor", AiToolKind.App, None,
                new[] { @"%LOCALAPPDATA%\Programs\cursor\Cursor.exe" },
                new[] { "cursor" }, OpensFolder: true),
            new AiTool("windsurf", "Windsurf", AiToolKind.App, None,
                new[] { @"%LOCALAPPDATA%\Programs\Windsurf\Windsurf.exe" },
                new[] { "windsurf" }, OpensFolder: true),
            new AiTool("claude-desktop", "Claude (desktop app)", AiToolKind.App, None,
                new[] { @"%LOCALAPPDATA%\AnthropicClaude\claude.exe" },
                new[] { "claude" }),
            new AiTool("lmstudio", "LM Studio", AiToolKind.App, None,
                new[] { @"%ProgramFiles%\LM Studio\LM Studio.exe", @"%LOCALAPPDATA%\Programs\LM Studio\LM Studio.exe" },
                None),
        };

        public static AiTool? Find(string? id)
        {
            foreach (var tool in All)
            {
                if (string.Equals(tool.Id, id, StringComparison.OrdinalIgnoreCase))
                    return tool;
            }

            return null;
        }

        /// <summary>The Function id for <paramref name="task"/> on <paramref name="toolId"/>.</summary>
        public static string FunctionId(string toolId, string task = OpenTask) => $"{Prefix}{toolId}/{task}";

        /// <summary>
        /// Splits an AI Function id into its tool and task. True only for the shape, so an
        /// unknown tool still parses; <see cref="IsKnown"/> is the check that it means something.
        /// </summary>
        public static bool TryParse(string? id, out string toolId, out string task)
        {
            toolId = task = string.Empty;
            if (string.IsNullOrWhiteSpace(id) || !id.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
                return false;

            string rest = id[Prefix.Length..];
            int slash = rest.IndexOf('/');
            if (slash <= 0 || slash == rest.Length - 1)
                return false;

            toolId = rest[..slash].Trim();
            task = rest[(slash + 1)..].Trim();
            return toolId.Length > 0 && task.Length > 0;
        }

        /// <summary>Prefix of a task that runs a prompt template: <c>task:&lt;templateId&gt;</c>.</summary>
        public const string TemplateTaskPrefix = "task:";

        /// <summary>The Function id that runs template <paramref name="templateId"/> with <paramref name="toolId"/>.</summary>
        public static string TaskFunctionId(string toolId, string templateId) =>
            FunctionId(toolId, TemplateTaskPrefix + templateId);

        /// <summary>The template a task names, if it is a template task.</summary>
        public static bool TryGetTemplateId(string task, out string templateId)
        {
            templateId = string.Empty;
            if (!task.StartsWith(TemplateTaskPrefix, StringComparison.OrdinalIgnoreCase))
                return false;

            templateId = task[TemplateTaskPrefix.Length..];
            return AiTaskTemplates.IsValidId(templateId);
        }

        /// <summary>
        /// A tool in the catalog and a task this version can run. Never depends on what is installed,
        /// nor on which templates exist: a deleted template, like an uninstalled tool, is reported
        /// when the button is clicked.
        /// </summary>
        public static bool IsKnown(string? id)
        {
            if (!TryParse(id, out string toolId, out string task) || Find(toolId) is not AiTool tool)
                return false;

            return string.Equals(task, OpenTask, StringComparison.OrdinalIgnoreCase)
                || (tool.SupportsTasks && TryGetTemplateId(task, out _));
        }

        /// <summary>
        /// The arguments for a one-shot task. The prompt always goes on stdin, never on the command line,
        /// where other processes can read it, length limits apply, and quoting can be abused. Fixed, not
        /// configurable, and never with a flag that widens what the tool may do:
        /// <list type="bullet">
        /// <item>Claude Code: print mode with every built-in tool disabled (<c>--tools ""</c>), no MCP
        /// servers from the user's configuration, and no saved session.</item>
        /// <item>Codex: the read-only sandbox, no saved session, and the answer written to
        /// <paramref name="outputFile"/> so progress output never mixes into it.</item>
        /// </list>
        /// </summary>
        public static IReadOnlyList<string> TaskArguments(AiTool tool, string outputFile) => tool.Id switch
        {
            "claude" => new[]
            {
                "-p", "--tools", "", "--strict-mcp-config", "--no-session-persistence", "--output-format", "text",
            },
            "codex" => new[]
            {
                "exec", "-s", "read-only", "--ephemeral", "--skip-git-repo-check", "--color", "never",
                "-o", outputFile, "-",
            },
            _ => throw new ArgumentException($"{tool.Name} cannot run AI tasks.", nameof(tool)),
        };

        /// <summary>
        /// The picker entry for running <paramref name="template"/> with <paramref name="tool"/>; labelled
        /// when the tool or the template is not available, like <see cref="Choice"/>.
        /// </summary>
        public static WindowsFunction TaskChoice(AiTool tool, string templateId, string? templateName, bool available) =>
            new(TaskFunctionId(tool.Id, templateId), available && templateName != null
                ? $"AI · {templateName} with {tool.Name}"
                : $"AI · {templateName ?? templateId} with {tool.Name} (not available)");

        /// <summary>
        /// The function-picker entry for opening <paramref name="tool"/>. <paramref name="available"/>
        /// false still produces an entry, labelled as such, so a button configured for a tool that has
        /// since gone away keeps showing what it is instead of a blank picker.
        /// </summary>
        public static WindowsFunction Choice(AiTool tool, bool available = true) =>
            new(FunctionId(tool.Id), available
                ? $"AI · Open {tool.Name}"
                : $"AI · Open {tool.Name} (not available)");
    }
}
