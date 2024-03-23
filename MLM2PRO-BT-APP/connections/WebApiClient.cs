using Windows.Web.Http;
using MLM2PRO_BT_APP.util;
using Newtonsoft.Json;

namespace MLM2PRO_BT_APP.connections
{
    public class WebApiClient
    {
        private readonly string _baseUrl = SettingsManager.Instance?.Settings?.WebApiSettings?.WebApiUrl ?? "";
        private const string SecretKey = "Secret";
        private readonly string _secretValue = SettingsManager.Instance?.Settings?.WebApiSettings?.WebApiSecret ?? "";

        public class User
        {
            public int Id { get; set; }
            public string? Token { get; set; }
            public long ExpireDate { get; set; }
        }

        public class ApiResponse
        {
            public bool Success { get; set; }
            public User? User { get; set; }
        }

        public async Task<ApiResponse?> SendRequestAsync(int userId)
        {
            Logger.Log("Sending request to Web API...");
            using HttpClient httpClient = new();
            httpClient.DefaultRequestHeaders.Add(SecretKey, _secretValue);
            Logger.Log("UserId: " + userId + ", Secret Key: " + SecretKey + ", Secret Value: " + _secretValue);
            Uri requestUri = new Uri(_baseUrl + userId);
            try
            {
                var response = await httpClient.GetAsync(requestUri);
                response.EnsureSuccessStatusCode();
                Logger.Log("Web API request successful.");

                string content = await response.Content.ReadAsStringAsync();
                var serverResponse = JsonConvert.DeserializeObject<ApiResponse>(content);
                Logger.Log("Server Response: " + serverResponse);
                return serverResponse;
            }
            catch (Exception ex)
            {
                Logger.Log($"Error: {ex.Message}");
                return null;
            }
        }
    }
}
