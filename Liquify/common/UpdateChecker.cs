using Microsoft.Win32;
using System;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace pyrochild.effects.common
{
    internal sealed class AvailableUpdate
    {
        public AvailableUpdate(Version version, string pageUrl, string notesUrl = null)
        {
            Version = version;
            PageUrl = pageUrl;
            NotesUrl = notesUrl;
        }

        // the page that says what is new in it; null when PageUrl is that page
        public string NotesUrl { get; }

        public Version Version { get; }

        // the page to send the user to for it
        public string PageUrl { get; }
    }

    /// <summary>
    /// Finds out whether a plugin has a newer release on GitHub by fetching a small file that
    /// holds its version, the first time it is asked in each run of Paint.NET. A plugin keeps
    /// one of these in a static, so that the answer lasts as long as Paint.NET does.
    /// </summary>
    internal sealed class UpdateChecker
    {
        // what a plugin's own releases call the file
        private const string VersionFile = "version.txt";
        private const string EnabledValue = "CheckForUpdates";
        private const string SnoozedUntilValue = "UpdateSnoozedUntil";
        private const int SnoozeDays = 3;
        private readonly string settingsKey;
        private readonly Version current;
        private readonly string repositoryUrl;
        private readonly string versionFile;
        private readonly string betaRepositoryUrl;
        private readonly string betaNewestReleaseUrl;
        private readonly Func<Version, string> notesUrl;
        private Task<AvailableUpdate> check;

        /// <param name="pluginName">Names the plugin's registry key, where the setting is kept.</param>
        /// <param name="current">The version that is running.</param>
        /// <param name="repository">The GitHub repository, as "owner/name", whose latest release
        /// is what users are sent to: the plugin's own, or that of an installer that carries it.</param>
        /// <param name="versionFile">The file in that release that holds the plugin's version.</param>
        /// <param name="betaRepository">For a beta build, the plugin's own repository: its newest
        /// release, pre-release or not, counts as an update too.</param>
        /// <param name="notesUrl">Gives the page that says what is new in a version, when that
        /// isn't the page users are sent to for it.</param>
        public UpdateChecker(string pluginName, Version current, string repository, string versionFile = VersionFile, string betaRepository = null, Func<Version, string> notesUrl = null)
        {
            this.notesUrl = notesUrl;
            settingsKey = @"Software\pyrochild\" + pluginName;
            this.current = current;
            repositoryUrl = "https://github.com/" + repository + "/";
            this.versionFile = versionFile;

            if (betaRepository != null)
            {
                betaRepositoryUrl = "https://github.com/" + betaRepository + "/";
                betaNewestReleaseUrl = "https://api.github.com/repos/" + betaRepository + "/releases?per_page=1";
            }
        }

        // Kept in the registry: a choice not to check has to outlast Paint.NET.
        public bool Enabled
        {
            get
            {
                try
                {
                    using RegistryKey key = Registry.CurrentUser.OpenSubKey(settingsKey);
                    return key == null || !(key.GetValue(EnabledValue) is int value) || value != 0;
                }
                catch (Exception)
                {
                    return true;
                }
            }
            set
            {
                try
                {
                    using RegistryKey key = Registry.CurrentUser.CreateSubKey(settingsKey);
                    key.SetValue(EnabledValue, value ? 1 : 0, RegistryValueKind.DWord);
                }
                catch (Exception)
                {
                }
            }
        }

        // "Not now": the update isn't offered again for a few days. Kept in the registry, as the
        // time (UTC) from which it is.
        public void Snooze()
        {
            try
            {
                using RegistryKey key = Registry.CurrentUser.CreateSubKey(settingsKey);
                key.SetValue(SnoozedUntilValue, DateTime.UtcNow.AddDays(SnoozeDays).ToString("o"), RegistryValueKind.String);
            }
            catch (Exception)
            {
            }
        }

        public bool Snoozed
        {
            get
            {
                try
                {
                    using RegistryKey key = Registry.CurrentUser.OpenSubKey(settingsKey);
                    DateTime until;
                    return key != null
                        && DateTime.TryParse(key.GetValue(SnoozedUntilValue) as string, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out until)
                        && DateTime.UtcNow < until;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        // The newer release if there is one, otherwise null (also when it couldn't be found out).
        // Asked once; later calls get the same answer. UI thread only.
        public Task<AvailableUpdate> FindNewerAsync()
        {
            if (check == null)
            {
#if RELEASED_BUILD
                check = Task.Run(FetchNewerAsync);
#else
                // only released builds make the request
                check = Task.FromResult<AvailableUpdate>(null);
#endif
            }
            return check;
        }

        private async Task<AvailableUpdate> FetchNewerAsync()
        {
            try
            {
                using HttpClient client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(10);
                client.MaxResponseContentBufferSize = 256 * 1024;

                // the API turns away requests without one
                client.DefaultRequestHeaders.UserAgent.ParseAdd("pdn-plugin-update-check");

                // "latest" is the newest release that isn't a pre-release
                AvailableUpdate update = await FetchAsync(client,
                    repositoryUrl + "releases/latest/download/" + versionFile,
                    repositoryUrl + "releases/latest", notesUrl).ConfigureAwait(false);

                if (betaRepositoryUrl != null)
                {
                    update = Newer(update, await FetchBetaAsync(client).ConfigureAwait(false));
                }
                return update;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // One place failing to answer mustn't hide what the other says, so neither of these throws.
        private async Task<AvailableUpdate> FetchAsync(HttpClient client, string versionUrl, string pageUrl, Func<Version, string> notesUrl)
        {
            try
            {
                string text = await client.GetStringAsync(versionUrl).ConfigureAwait(false);
                Version newer = ParseNewer(text, current);
                return newer == null ? null : new AvailableUpdate(newer, pageUrl, notesUrl?.Invoke(newer));
            }
            catch (Exception)
            {
                return null;
            }
        }

        // Nothing on github.com points at the newest release of either kind, so ask the API where
        // it is.
        private async Task<AvailableUpdate> FetchBetaAsync(HttpClient client)
        {
            try
            {
                string json = await client.GetStringAsync(betaNewestReleaseUrl).ConfigureAwait(false);

                string versionUrl, pageUrl;
                if (!TryFindRelease(json, betaRepositoryUrl, out versionUrl, out pageUrl))
                {
                    return null;
                }
                // a release's own page has its notes
                return await FetchAsync(client, versionUrl, pageUrl, null).ConfigureAwait(false);
            }
            catch (Exception)
            {
                return null;
            }
        }

        // The higher version of the two, either of which can be null. The first wins a tie: it is
        // the finished release.
        internal static AvailableUpdate Newer(AvailableUpdate release, AvailableUpdate beta)
        {
            if (release == null || (beta != null && beta.Version > release.Version))
            {
                return beta;
            }
            return release;
        }

        // Picks the first release in the API's list that has a version file, and gives where that
        // file and the release's page are. Both must be in the repository: the page is opened in
        // the user's browser.
        internal static bool TryFindRelease(string json, string repositoryUrl, out string versionUrl, out string pageUrl)
        {
            versionUrl = null;
            pageUrl = null;

            try
            {
                using JsonDocument document = JsonDocument.Parse(json);
                foreach (JsonElement release in document.RootElement.EnumerateArray())
                {
                    string page = release.GetProperty("html_url").GetString();

                    foreach (JsonElement asset in release.GetProperty("assets").EnumerateArray())
                    {
                        string url = asset.GetProperty("browser_download_url").GetString();

                        if (asset.GetProperty("name").GetString() == VersionFile
                            && IsIn(url, repositoryUrl)
                            && IsIn(page, repositoryUrl))
                        {
                            versionUrl = url;
                            pageUrl = page;
                            return true;
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
            return false;
        }

        private static bool IsIn(string url, string repositoryUrl)
        {
            return url != null && url.StartsWith(repositoryUrl, StringComparison.OrdinalIgnoreCase);
        }

        internal static Version ParseNewer(string text, Version current)
        {
            Version latest;
            if (text != null && Version.TryParse(text.Trim(), out latest) && latest > current)
            {
                return latest;
            }
            return null;
        }
    }
}
