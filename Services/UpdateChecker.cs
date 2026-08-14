using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Action_Wheel.Services
{
    /// <summary>A newer release than the one currently running.</summary>
    public sealed record UpdateInfo(Version Version, string TagName, string ReleaseUrl);

    /// <summary>
    /// Asks GitHub's Releases API for the latest published release and compares its tag against the
    /// version embedded in this exe.
    /// </summary>
    /// <remarks>
    /// Best-effort only, by design: no internet connection, a GitHub outage, a rate-limited
    /// response, a moved repository or a malformed body all look identical from here - "no update
    /// right now", never an error surfaced to someone who did not ask for this check. The
    /// <c>/releases/latest</c> endpoint itself already skips drafts and pre-releases, so nothing
    /// here needs to filter those out again.
    /// </remarks>
    public static class UpdateChecker
    {
        private const string ReleasesApiUrl = "https://api.github.com/repos/cmm-cmm/Action-Wheel/releases/latest";

        // GitHub's unauthenticated rate limit is 60 requests/hour per IP - this is nowhere near
        // that, but there is no reason to ask again every time someone happens to relaunch the app.
        private static readonly TimeSpan MinCheckInterval = TimeSpan.FromHours(20);

        /// <summary>
        /// Returns the newer release if one exists and enough time has passed since the last check,
        /// otherwise null. Never throws.
        /// </summary>
        public static async Task<UpdateInfo?> CheckAsync(Version currentVersion)
        {
            var lastChecked = UpdateCheckSettings.LoadLastCheckedUtc();
            if (lastChecked.HasValue && DateTime.UtcNow - lastChecked.Value < MinCheckInterval)
                return null;

            UpdateInfo? update = null;
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
                // The GitHub REST API rejects requests with no User-Agent header.
                client.DefaultRequestHeaders.UserAgent.ParseAdd("ActionWheel-UpdateChecker");
                client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

                using var response = await client.GetAsync(ReleasesApiUrl).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    using var document = JsonDocument.Parse(json);
                    var root = document.RootElement;

                    string tag = root.TryGetProperty("tag_name", out var tagElement)
                        ? tagElement.GetString() ?? string.Empty : string.Empty;
                    string url = root.TryGetProperty("html_url", out var urlElement)
                        ? urlElement.GetString() ?? string.Empty : string.Empty;

                    // Compared as Major.Minor.Build only: the tag ("v2.5.0") never carries a fourth
                    // segment, while the assembly version always does (Revision defaults to 0), and
                    // Version.CompareTo treats an absent segment as -1 - comparing the raw parses
                    // would read an identical release as "older" than what is already running.
                    if (TryParseReleaseVersion(tag, out var latest) &&
                        latest > Trim(currentVersion) && !string.IsNullOrEmpty(url))
                    {
                        update = new UpdateInfo(latest, tag, url);
                    }
                }
            }
            catch (Exception)
            {
                // Offline, DNS failure, timeout, GitHub outage, unexpected JSON shape - all mean
                // "could not check right now", which is not worth telling anyone about.
            }

            UpdateCheckSettings.SaveLastCheckedUtc(DateTime.UtcNow);
            return update;
        }

        private static Version Trim(Version version) => new(version.Major, version.Minor, version.Build);

        private static bool TryParseReleaseVersion(string tag, out Version version)
        {
            string trimmed = tag.TrimStart('v', 'V');
            return Version.TryParse(trimmed, out version!);
        }
    }
}
