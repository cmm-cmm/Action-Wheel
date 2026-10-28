using System;
using System.Diagnostics;
using System.IO;

namespace Action_Wheel.Services
{
    /// <summary>An AI task button was clicked: run template <paramref name="TemplateId"/> with the tool.</summary>
    /// <param name="TargetWindow">The window that was in front when the ring opened, holding the selection.</param>
    public sealed record AiTaskRequest(string ToolId, string TemplateId, IntPtr TargetWindow);

    /// <summary>
    /// Runs an <c>action-wheel.ai:</c> Function. Phase 1 has one task, <see cref="AiTools.OpenTask"/>:
    /// start the tool where the user is working. See Doc/ai-assistants.md.
    /// </summary>
    /// <remarks>
    /// Nothing is sent to any tool here. A CLI starts interactively in a terminal and receives only
    /// what the user types into it.
    /// </remarks>
    public static class AiLauncher
    {
        /// <summary>Starts the tool. Returns false with a message fit for the user when it cannot.</summary>
        public static bool TryRun(string toolId, string task, IntPtr targetWindow, out string error)
        {
            error = string.Empty;

            var tool = AiTools.Find(toolId);
            if (tool == null || !string.Equals(task, AiTools.OpenTask, StringComparison.OrdinalIgnoreCase))
            {
                error = "this AI action is not supported by this version of Action Wheel.";
                return false;
            }

            if (!AiAssistantSettings.IsEnabled(tool.Id))
            {
                error = $"{tool.Name} is switched off in Settings, under AI assistants.";
                return false;
            }

            // Detected again on every click rather than cached from startup: the tool may have been
            // installed or removed since, and the lookup is only a few file and registry reads.
            var found = AiToolDetector.Detect(tool);
            if (!found.IsInstalled)
            {
                error = found.State switch
                {
                    AiToolState.Stale => $"{tool.Name} is no longer installed ({found.Path} is missing).",
                    AiToolState.LauncherOnly => $"{tool.Name} is not installed. {found.Path} is only a launcher that would install it.",
                    AiToolState.NeedsRepair => $"Windows reports the {tool.Name} package as damaged. Repair or reinstall it from Settings > Apps.",
                    _ => $"{tool.Name} was not found on this computer.",
                };
                return false;
            }

            string? folder = tool.Kind == AiToolKind.Cli || tool.OpensFolder
                ? ExplorerFolder.Of(targetWindow)
                : null;

            try
            {
                if (found.AppUserModelId != null)
                    StartStoreApp(found.AppUserModelId);
                else if (tool.Kind == AiToolKind.Cli)
                    StartInTerminal(found.Path, folder ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
                else
                    StartApp(found.Path, folder);

                return true;
            }
            catch (Exception ex)
            {
                error = $"could not start {tool.Name} ({ex.Message}).";
                return false;
            }
        }

        /// <summary>
        /// A new Windows Terminal tab in <paramref name="folder"/>, or without Windows Terminal a
        /// console window there. An npm shim is a .cmd file, which needs cmd.exe to run.
        /// </summary>
        private static void StartInTerminal(string executable, string folder)
        {
            bool script = executable.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase)
                || executable.EndsWith(".bat", StringComparison.OrdinalIgnoreCase);

            if (CommandResolver.Resolve("wt.exe") is string terminal)
            {
                var info = new ProcessStartInfo(terminal) { UseShellExecute = false };

                // ';' separates Windows Terminal's own subcommands, so one inside a path has to be
                // escaped or the path is cut in two.
                info.ArgumentList.Add("-d");
                info.ArgumentList.Add(EscapeForTerminal(folder));
                if (script)
                {
                    info.ArgumentList.Add("cmd.exe");
                    info.ArgumentList.Add("/k");
                }
                info.ArgumentList.Add(EscapeForTerminal(executable));

                Process.Start(info)?.Dispose();
                return;
            }

            // A console program started through the shell from a windowed app gets a console window
            // of its own.
            var console = script
                ? new ProcessStartInfo("cmd.exe", $"/k {Quote(executable)}")
                : new ProcessStartInfo(executable);
            console.UseShellExecute = true;
            console.WorkingDirectory = folder;
            Process.Start(console)?.Dispose();
        }

        /// <summary>
        /// A Microsoft Store app cannot be started by its file path: its executable sits in the
        /// protected WindowsApps folder and needs its package identity. <c>shell:AppsFolder\&lt;id&gt;</c>
        /// is how the Start menu itself starts it.
        /// </summary>
        private static void StartStoreApp(string appUserModelId) =>
            Process.Start(new ProcessStartInfo("explorer.exe", $@"shell:AppsFolder\{appUserModelId}") { UseShellExecute = true })?.Dispose();

        private static void StartApp(string executable, string? folder)
        {
            var info = new ProcessStartInfo(executable) { UseShellExecute = true };
            if (folder != null)
            {
                info.Arguments = Quote(folder);
                info.WorkingDirectory = folder;
            }

            Process.Start(info)?.Dispose();
        }

        private static string EscapeForTerminal(string value) => value.Replace(";", @"\;");

        /// <summary>
        /// Quotes one command-line argument. A trailing backslash, as in a drive root like "C:\",
        /// is doubled, or it would escape the closing quote.
        /// </summary>
        private static string Quote(string value) =>
            "\"" + (value.EndsWith('\\') ? value + "\\" : value) + "\"";
    }
}
