using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using Newtonsoft.Json;

namespace MLM2PRO_BT_APP.util
{
    public class GitHubReleaseChecker
    {
        private readonly string _repositoryOwner;
        private readonly string _repositoryName;

        public GitHubReleaseChecker(string repositoryOwner, string repositoryName)
        {
            _repositoryOwner = "DuwayneF";
            _repositoryName = "MLM2PRO-BT-APP";
        }

        public async Task CheckForUpdateAsync(string currentVersion)
        {
            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Add("User-Agent", "request");

                var url = $"https://api.github.com/repos/{_repositoryOwner}/{_repositoryName}/releases/latest";
                var response = await httpClient.GetStringAsync(url);
                var latestRelease = JsonConvert.DeserializeObject<GitHubRelease>(response);

                if (latestRelease != null && IsNewerVersion(currentVersion, latestRelease.TagName))
                {
                    Logger.Log($"Update available: {latestRelease.TagName}");
                    Logger.Log($"Release notes: {latestRelease.Body}");
                }
            }
        }

        private bool IsNewerVersion(string currentVersion, string latestVersionTag)
        {
            return latestVersionTag != currentVersion;
        }

        class GitHubRelease
        {
            [JsonProperty("tag_name")]
            public string TagName { get; set; }

            [JsonProperty("body")]
            public string Body { get; set; }
        }
    }

}
