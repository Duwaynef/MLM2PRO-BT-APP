using Windows.Web.Http;
using MLM2PRO_BT_APP.util;
using Newtonsoft.Json;

namespace MLM2PRO_BT_APP.connections
{
    public class WebApiClient
    {
        private string BaseUrl;
        private string SecretKey = "Secret";
        private string SecretValue;

        public WebApiClient()
        {
            // Initialize BaseUrl and SecretValue using appSettings
            BaseUrl = SettingsManager.Instance?.Settings?.WebApiSettings?.WebApiURL ?? "";
            SecretValue = SettingsManager.Instance?.Settings?.WebApiSettings?.WebApiSecret ?? "";
        }
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

        public async Task<ApiResponse> SendRequestAsync(int userId)
        {
            Logger.Log("Sending request to Web API...");
            using HttpClient httpClient = new();
            httpClient.DefaultRequestHeaders.Add(SecretKey, SecretValue);
            Logger.Log("UserId: " + userId + ", Secret Key: " + SecretKey + ", Secret Value: " + SecretValue);
            Uri requestUri = new Uri(BaseUrl + userId);
            try
            {
                HttpResponseMessage response = await httpClient.GetAsync(requestUri);
                response.EnsureSuccessStatusCode();
                Logger.Log("Web API request successful.");

                string content = await response.Content.ReadAsStringAsync();

                // Deserialize the JSON response into the ServerResponse object
                ApiResponse? serverResponse = JsonConvert.DeserializeObject<ApiResponse>(content);
                Logger.Log("Server Response: " + serverResponse);
                return serverResponse;
            }
            catch (Exception ex)
            {
                // Log or handle exceptions as needed
                // This is a basic example to return null indicating an error
                // In practice, you might want to return a custom error object or handle differently
                Logger.Log($"Error: {ex.Message}");
                return null;
            }
        }
    }
}
