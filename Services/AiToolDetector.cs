using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;

namespace Action_Wheel.Services
{
    /// <summary>What detection concluded about one AI tool.</summary>
    public enum AiToolState
    {
        /// <summary>An executable for it exists right now.</summary>
        Installed,
        /// <summary>Nothing points at it.</summary>
        Missing,
        /// <summary>Something still points at it, typically a protocol handler, but the file is gone.</summary>
        Stale,
        /// <summary>
        /// Only a stand-in launcher was found, one that would install the tool on first run (see
        /// <see cref="AiTool.LauncherMarkers"/>). Not installed: a button must not start an installer.
        /// </summary>
        LauncherOnly,
    }

    /// <param name="Path">The executable that was found, or for <see cref="AiToolState.Stale"/> the one that is gone.</param>
    /// <param name="Source">How it was found, for the settings row: "PATH", "App Paths", a known folder, a protocol.</param>
    public sealed record AiToolDetection(AiTool Tool, AiToolState State, string Path, string Source)
    {
        public bool IsInstalled => State == AiToolState.Installed;
    }

    /// <summary>
    /// Finds the AI tools of <see cref="AiTools"/> on this machine. See Doc/ai-assistants.md.
    /// </summary>
    /// <remarks>
    /// Several sources per tool, because each tool leaves its traces somewhere different: a native
    /// installer on PATH, an npm .cmd shim, a known install folder, a URL protocol handler. Every
    /// source must resolve to a file that exists now. Measured on a real machine, Antigravity's
    /// protocol handler outlived the app: trusting the registry alone would have offered a button
    /// that cannot start anything.
    ///
    /// Only executables and the registry are read, never a tool's configuration folder. Those can
    /// hold credentials, and their presence says nothing about whether the tool is still installed.
    ///
    /// File and registry lookups only, so a full scan is a few milliseconds. Even so, callers keep it
    /// off the UI thread and away from the hook callbacks.
    /// </remarks>
    public static class AiToolDetector
    {
        public static IReadOnlyList<AiToolDetection> DetectAll()
        {
            var results = new List<AiToolDetection>(AiTools.All.Count);
            foreach (var tool in AiTools.All)
                results.Add(Detect(tool));
            return results;
        }

        public static AiToolDetection Detect(AiTool tool)
        {
            string? stale = null;
            string? launcher = null;

            // Every hit, not just the first: a stand-in launcher can sit on PATH ahead of the real
            // tool, and the real one must still win when both are there.
            foreach (string command in tool.Commands)
            {
                foreach (string resolved in CommandResolver.ResolveAll(command))
                {
                    if (tool.IsLauncher(resolved))
                    {
                        launcher ??= resolved;
                        continue;
                    }

                    return new AiToolDetection(tool, AiToolState.Installed, resolved, $"the {command} command");
                }
            }

            foreach (string known in tool.KnownPaths)
            {
                string path = Environment.ExpandEnvironmentVariables(known);
                if (File.Exists(path))
                    return new AiToolDetection(tool, AiToolState.Installed, path, "install folder");
            }

            foreach (string protocol in tool.Protocols)
            {
                if (ProtocolTarget(protocol) is not string target)
                    continue;

                if (File.Exists(target))
                    return new AiToolDetection(tool, AiToolState.Installed, target, $"{protocol}:// handler");

                stale ??= target;
            }

            if (launcher != null)
                return new AiToolDetection(tool, AiToolState.LauncherOnly, launcher, "launcher");

            return stale != null
                ? new AiToolDetection(tool, AiToolState.Stale, stale, "protocol handler")
                : new AiToolDetection(tool, AiToolState.Missing, string.Empty, string.Empty);
        }

        /// <summary>
        /// The executable a URL protocol's open command names, such as
        /// <c>"C:\...\Antigravity.exe" "%1"</c>, or null. Per-user registration wins, as it does
        /// for the shell. The "URL Protocol" value is required: without it the key is a file type,
        /// not something a URL can start.
        /// </summary>
        private static string? ProtocolTarget(string protocol)
        {
            foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
            {
                try
                {
                    using var key = hive.OpenSubKey($@"Software\Classes\{protocol}");
                    if (key?.GetValue("URL Protocol") == null)
                        continue;

                    using var command = key.OpenSubKey(@"shell\open\command");
                    if (command?.GetValue(null) is string line && ExecutableOf(line) is string exe)
                        return exe;
                }
                catch (Exception)
                {
                    // A key locked by policy is "not registered here".
                }
            }

            return null;
        }

        /// <summary>The first token of a command line: quoted, or up to the first space.</summary>
        private static string? ExecutableOf(string commandLine)
        {
            string line = commandLine.Trim();
            if (line.Length == 0)
                return null;

            string exe = line[0] == '"'
                ? line[1..].Split('"')[0]
                : line.Split(' ')[0];

            return exe.Length == 0 ? null : Environment.ExpandEnvironmentVariables(exe);
        }
    }
}
