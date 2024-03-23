using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using Newtonsoft.Json;
using System.Text.RegularExpressions;

namespace MLM2PRO_BT_APP.util
{
    public class GitHubReleaseChecker
    {
        private readonly string _repositoryOwner;
        private readonly string _repositoryName;

        public GitHubReleaseChecker(string repositoryOwner, string repositoryName)
        {
            _repositoryOwner = repositoryOwner;
            _repositoryName = repositoryName;
        }

        public async Task<GitHubRelease?> CheckForUpdateAsync(string currentVersion)
        {
            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Add("User-Agent", "request");

                var url = $"https://api.github.com/repos/{_repositoryOwner}/{_repositoryName}/releases";
                var response = await httpClient.GetStringAsync(url);
                var releases = JsonConvert.DeserializeObject<List<GitHubRelease>>(response);

                if (releases == null || releases.Count == 0) return null;

                foreach (var release in releases)
                {
                    if (release.TagName == "debug") continue;
                    if (release.TagName != null && !IsNewerVersion(currentVersion, release.TagName)) return null;
                    Logger.Log($"Update available: {release.TagName}");
                    Logger.Log($"Release notes: \n{release.Body}");
                    return release;
                }
            }
            return null;
        }

        private bool IsNewerVersion(string currentVersion, string latestVersionTag)
        {
            var regex = new Regex(@"[^\d.]");
            string numericLatestVersionTag = regex.Replace(latestVersionTag, "");
            try
            {
                Version current = new Version(currentVersion);
                Version latest = new Version(numericLatestVersionTag);
                return latest > current;
            }
            catch
            {
                return false;
            }
        }

        public class GitHubRelease
        {
            [JsonProperty("html_url")]
            public string? HtmlUrl { get; set; }

            [JsonProperty("tag_name")]
            public string? TagName { get; set; }

            [JsonProperty("target_commitish")]
            public string? TargetCommitish { get; set; }

            [JsonProperty("name")]
            public string? Name { get; set; }

            [JsonProperty("body")]
            public string? Body { get; set; }

            [JsonProperty("published_at")]
            public string? PublishedAt { get; set; }
        }
    }

}
