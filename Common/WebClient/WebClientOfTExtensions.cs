using System;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Threading.Tasks;

namespace BelugaFactory.Common.WebClient
{
    public static class WebClientOfTExtensions
    {
        private const string contentType = "application/json";

        public static void SetTokenBearer(this HttpRequestHeaders header, string token = null)
        {
            if (!string.IsNullOrEmpty(token) && token.IndexOf(":", System.StringComparison.Ordinal) > 0)
            {
                string[] headerToken = token.Split(':');

                header.Add(headerToken[0], headerToken[1]);
            }
            else if (!string.IsNullOrEmpty(token))
            {
                header.Add("Authorization", "Bearer " + token);
            }
        }

        public static void SetHttpClientHeaders(this HttpClient client, string token = null)
        {
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(contentType));
            client.DefaultRequestHeaders.SetTokenBearer(token);
        }

        public static async Task<HttpResponseMessage> PatchAsync(this HttpClient client, Uri requestUri, HttpContent iContent)
        {
            var method = new HttpMethod("PATCH");
            var request = new HttpRequestMessage(method, requestUri)
            {
                Content = iContent
            };

            HttpResponseMessage response = new HttpResponseMessage();
            try
            {
                response = await client.SendAsync(request);
            }
            catch (TaskCanceledException e)
            {
                Debug.WriteLine("ERROR: " + e.ToString());
            }

            return response;
        }
    }
}
