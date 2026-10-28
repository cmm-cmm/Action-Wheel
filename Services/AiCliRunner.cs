using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Action_Wheel.Services
{
    /// <summary>How a one-shot AI task ended.</summary>
    public sealed record AiCliResult(bool Succeeded, string Answer, string Error);

    /// <summary>
    /// Runs one AI task: starts the tool's CLI hidden, writes the prompt to its stdin, and collects
    /// the answer. See <see cref="AiTools.TaskArguments"/> for the command lines.
    /// </summary>
    /// <remarks>
    /// Each run gets a new empty folder as its working directory, deleted afterwards. Claude Code's
    /// print mode skips the workspace trust prompt, so the folder it runs in must hold nothing of the
    /// user's. The tools still use the user's own sign-in. Action Wheel never sees or stores a key.
    /// </remarks>
    public static class AiCliRunner
    {
        public static readonly TimeSpan Timeout = TimeSpan.FromMinutes(3);

        public static async Task<AiCliResult> RunAsync(AiTool tool, string executable, string prompt, CancellationToken cancel)
        {
            string folder = Path.Combine(Path.GetTempPath(), "ActionWheel-ai-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            string outputFile = Path.Combine(folder, "answer.txt");

            try
            {
                var info = new ProcessStartInfo(executable)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = folder,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                };
                foreach (string argument in AiTools.TaskArguments(tool, outputFile))
                    info.ArgumentList.Add(argument);

                using var process = new Process { StartInfo = info };
                process.Start();

                var output = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
                var errors = process.StandardError.ReadToEndAsync(CancellationToken.None);

                await process.StandardInput.WriteAsync(prompt.AsMemory(), cancel).ConfigureAwait(false);
                process.StandardInput.Close();

                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel);
                timeout.CancelAfter(Timeout);
                try
                {
                    await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    try { process.Kill(entireProcessTree: true); } catch (Exception) { }
                    return new AiCliResult(false, string.Empty, cancel.IsCancellationRequested
                        ? "Cancelled."
                        : $"{tool.Name} did not answer within {Timeout.TotalMinutes:0} minutes.");
                }

                string stdout = (await output.ConfigureAwait(false)).Trim();
                string stderr = (await errors.ConfigureAwait(false)).Trim();

                // Codex writes only its final message to the output file; its stdout can carry
                // progress. Claude Code's text output is the answer itself.
                string answer = File.Exists(outputFile) ? File.ReadAllText(outputFile).Trim() : stdout;

                if (process.ExitCode != 0 || answer.Length == 0)
                {
                    string detail = LastLines(stderr.Length > 0 ? stderr : stdout, 12);
                    return new AiCliResult(false, string.Empty,
                        $"{tool.Name} exited with code {process.ExitCode}." + (detail.Length > 0 ? $"\n\n{detail}" : string.Empty));
                }

                return new AiCliResult(true, answer, string.Empty);
            }
            catch (Exception ex)
            {
                return new AiCliResult(false, string.Empty, $"Could not run {tool.Name}: {ex.Message}");
            }
            finally
            {
                try { Directory.Delete(folder, recursive: true); } catch (Exception) { }
            }
        }

        private static string LastLines(string text, int count)
        {
            var lines = new List<string>(text.Split('\n'));
            return string.Join("\n", lines.GetRange(Math.Max(0, lines.Count - count), Math.Min(count, lines.Count))).Trim();
        }
    }
}
