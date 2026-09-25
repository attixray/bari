using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using Bari.Core.Model;
using Bari.Core.UI;

namespace Bari.Core.Commands
{
    /// <summary>
    /// Updates bari from the latest GitHub release, through the release's install script
    /// </summary>
    public class SelfUpdateCommand : ICommand
    {
        /// <summary>
        /// The GitHub repository whose releases bari updates from
        /// </summary>
        public const string Repository = "attixray/bari";

        private const string InstallScript = "install-bari.ps1";

        private static readonly HttpClient client = CreateClient();

        private readonly IUserOutput output;

        public SelfUpdateCommand(IUserOutput output)
        {
            this.output = output;
        }

        public string Name { get { return "selfupdate"; } }
        public string Description { get { return "updates bari to its latest release"; } }
        public string Help
        {
            get
            {
                return @"=Self update command=

Checks the latest release of bari on GitHub (" + Repository + @"). If it is newer
than this build, downloads the release's install script and starts it in a new
window. The script waits for this bari to exit, downloads the release, checks it
against its SHA256SUMS and replaces this installation. The previous installation
is kept next to it as <directory>.previous.
Example: `bari selfupdate`

The install script can also be run on its own; see doc/net10-migration.md.
";
            }
        }
        public bool NeedsExplicitTargetGoal { get { return false; } }

        public bool Run(Suite suite, string[] parameters)
        {
            var current = typeof(SelfUpdateCommand).Assembly.GetName().Version;

            string tag, page, scriptUrl;
            try
            {
                using (var release = JsonDocument.Parse(client.GetStringAsync(
                           "https://api.github.com/repos/" + Repository + "/releases/latest").GetAwaiter().GetResult()))
                {
                    var root = release.RootElement;
                    tag = root.GetProperty("tag_name").GetString();
                    page = root.GetProperty("html_url").GetString();
                    scriptUrl = root.GetProperty("assets").EnumerateArray()
                        .Where(asset => asset.GetProperty("name").GetString() == InstallScript)
                        .Select(asset => asset.GetProperty("browser_download_url").GetString())
                        .FirstOrDefault();
                }
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is JsonException || ex is InvalidOperationException ||
                                       ex is System.Threading.Tasks.TaskCanceledException || ex is System.Collections.Generic.KeyNotFoundException)
            {
                output.Error(String.Format("Could not read the latest release of {0}: {1}", Repository, ex.Message));
                return false;
            }

            if (!IsNewer(tag, current))
            {
                output.Message(String.Format("bari {0} is up to date; the latest release is {1}.", current, tag));
                return true;
            }

            if (!OperatingSystem.IsWindows())
            {
                output.Message(String.Format("bari {0} is available: {1}", tag, page));
                return true;
            }

            if (scriptUrl == null)
            {
                output.Warning(String.Format("bari {0} is available, but its release has no {1}. Download it from {2}", tag, InstallScript, page));
                return false;
            }

            var script = Path.Combine(Path.GetTempPath(), "bari-" + tag + "-" + InstallScript);
            try
            {
                File.WriteAllBytes(script, client.GetByteArrayAsync(scriptUrl).GetAwaiter().GetResult());
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is IOException || ex is UnauthorizedAccessException ||
                                       ex is System.Threading.Tasks.TaskCanceledException)
            {
                output.Error(String.Format("Could not download {0}: {1}", scriptUrl, ex.Message));
                return false;
            }

            var installDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var arguments = String.Format("-NoProfile -ExecutionPolicy Bypass -File \"{0}\" -InstallDir \"{1}\" -Version \"{2}\" -Repository {3} -WaitForProcessId {4}",
                script, installDir, tag, Repository, Environment.ProcessId);

            // A window of its own: it outlives this bari, which must exit before its files can be replaced.
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("powershell.exe", arguments) { UseShellExecute = true });

            output.Message(String.Format("Updating bari {0} to {1} in {2}. The installer continues in a new window once this bari has exited.",
                current, tag, installDir));
            return true;
        }

        /// <summary>
        /// Checks whether a release tag is newer than a build. Builds are versioned
        /// &lt;tag&gt;.&lt;commits since the tag&gt;, so only the first three parts are compared.
        /// </summary>
        /// <param name="tag">The release tag, such as <c>1.1.0</c> or <c>v1.1.0</c></param>
        /// <param name="current">The version of the running build</param>
        /// <returns>Returns <c>true</c> if the tag is a version above the build's.</returns>
        public static bool IsNewer(string tag, Version current)
        {
            Version released;
            if (tag == null || !Version.TryParse(tag.TrimStart('v', 'V'), out released))
                return false;

            return Normalize(released) > Normalize(current);
        }

        private static Version Normalize(Version version)
        {
            return new Version(version.Major, version.Minor, Math.Max(version.Build, 0));
        }

        private static HttpClient CreateClient()
        {
            var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("bari-selfupdate");
            httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            return httpClient;
        }
    }
}
