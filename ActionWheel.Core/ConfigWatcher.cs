using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Action_Wheel.Core
{
    /// <summary>
    /// Watches actions.json and raises <see cref="Changed"/> when it is edited outside the app, so
    /// hand-editing the file no longer needs a trip to the tray menu to take effect.
    /// </summary>
    /// <remarks>
    /// Both candidate locations are watched, not just the active one: dropping a portable
    /// actions.json next to the exe changes which file is in effect, and that has to be noticed too.
    ///
    /// Every notification is debounced. A single save in Notepad produces a burst of events - and
    /// several editors write by truncating the file first, so an undebounced reload would run
    /// against a zero-byte file and reject it.
    /// </remarks>
    public sealed class ConfigWatcher : IDisposable
    {
        private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(400);

        private readonly List<FileSystemWatcher> _watchers = new();
        private readonly Timer _timer;
        private readonly object _gate = new();
        private DateTime _suppressedUntil = DateTime.MinValue;
        private bool _disposed;

        /// <summary>Raised once per burst of edits, on a thread-pool thread.</summary>
        public event EventHandler? Changed;

        public ConfigWatcher()
        {
            _timer = new Timer(_ => Fire(), null, Timeout.Infinite, Timeout.Infinite);

            foreach (var directory in CandidateDirectories())
                TryWatch(directory);
        }

        /// <summary>True when at least one directory is actually being watched.</summary>
        private static IEnumerable<string> CandidateDirectories()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var path in new[] { ActionConfig.PortableConfigPath, ActionConfig.UserConfigPath })
            {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && seen.Add(directory))
                    yield return directory;
            }
        }

        private void TryWatch(string directory)
        {
            try
            {
                if (!Directory.Exists(directory))
                    return;

                var watcher = new FileSystemWatcher(directory, ActionConfig.FileName)
                {
                    // Renamed matters as much as Changed: "save" in several editors is a write to a
                    // temp file followed by a rename over the target - including this app's own save.
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                };

                watcher.Changed += OnFileEvent;
                watcher.Created += OnFileEvent;
                watcher.Deleted += OnFileEvent;
                watcher.Renamed += OnFileEvent;
                watcher.Error += (s, e) => Dispose();
                watcher.EnableRaisingEvents = true;

                _watchers.Add(watcher);
            }
            catch (Exception)
            {
                // A watcher is a convenience; the tray menu's Reload still works without it.
            }
        }

        /// <summary>
        /// Ignores changes for a short while. Called around this app's own saves: the settings
        /// window already reloads the launcher when it saves, and a second reload from the watcher
        /// would only produce a spurious "the file changed on disk" notice.
        /// </summary>
        public void SuppressFor(TimeSpan duration)
        {
            lock (_gate)
                _suppressedUntil = DateTime.UtcNow + duration;
        }

        private void OnFileEvent(object sender, FileSystemEventArgs e)
        {
            lock (_gate)
            {
                if (_disposed)
                    return;

                // Restart the countdown rather than adding to it, so a long burst still results in
                // exactly one reload, fired once the writing has actually stopped.
                _timer.Change(Debounce, Timeout.InfiniteTimeSpan);
            }
        }

        private void Fire()
        {
            lock (_gate)
            {
                if (_disposed || DateTime.UtcNow < _suppressedUntil)
                    return;
            }

            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed)
                    return;

                _disposed = true;
            }

            foreach (var watcher in _watchers)
            {
                try
                {
                    watcher.EnableRaisingEvents = false;
                    watcher.Dispose();
                }
                catch
                {
                    // Shutting down; nothing useful to do about a watcher that will not close.
                }
            }

            _watchers.Clear();
            _timer.Dispose();
        }
    }
}
