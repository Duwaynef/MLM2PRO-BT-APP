using Windows.Web.Http;
using MLM2PRO_BT_APP.util;
using Newtonsoft.Json;
using System.Collections;
using System.Text;

namespace MLM2PRO_BT_APP.connections
{
    public class WebApiClient
    {
        private readonly Encryption _btEncryption = new();
        private readonly ByteConversionUtils _byteConversionUtils = new();
        private readonly string _baseUrl = SettingsManager.Instance?.Settings?.WebApiSettings?.WebApiUrl ?? "";
        private const string SecretKey = "Secret";
        // private readonly string _secretValue = SettingsManager.Instance?.Settings?.WebApiSettings?.WebApiSecret ?? "";
        private readonly string _secretEnc = "19605BE9BD42E0B3AEB20003847376012404EC9D72BB5586391F01BE03F031163242C34CD55C2C3E77D10D9A43A677A6";
        private byte[]? _secretByteArr;
        private string _secretValue;

        public WebApiClient()
        {        
            _secretByteArr = _byteConversionUtils.StringToByteArray(_secretEnc);
            _secretValue = Encoding.UTF8.GetString(_btEncryption?.Decrypt(_secretByteArr));
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
