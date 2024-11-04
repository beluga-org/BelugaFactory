using System.Net.Http.Headers;
using System.Text.Json;
using BelugaFactory.Config;
using BelugaFactory.Services.Processing.Requests;

namespace BelugaFactory.Services.Processing;
public class OpenAiService
{
    private readonly string _openAiApiUrl = "https://api.openai.com";
    private readonly HttpClient _httpClient;

    public OpenAiService()
    {
        string openAiApiKey = EnvironmentSettings.OpenAiApiKey;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            openAiApiKey
        );
    }

    public async Task<MemoryStream> Transcript(TranscriptRequest req)
    {
        try
        {
            using (var formData = new MultipartFormDataContent())
            {
                using (var streamContent = new StreamContent(req.FileStream))
                {
                    streamContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");

                    formData.Add(streamContent, "file", req.FileName);
                    formData.Add(new StringContent("srt"), "response_format");
                    formData.Add(new StringContent("whisper-1"), "model");

                    var response = await _httpClient.PostAsync($"{_openAiApiUrl}/v1/audio/transcriptions", formData);

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new Exception($"Erro ao chamar a API de Transcription: {response.ReasonPhrase}");
                    }

                    var buffer = await response.Content.ReadAsByteArrayAsync();

                    MemoryStream memoryStream = new MemoryStream(buffer);

                    return memoryStream;
                }
            }
        }
        catch (Exception ex)
        {
            throw ex;
        }
    }

    public async Task<MemoryStream> Translate(TranscriptRequest req)
    {
        try
        {
            using (var formData = new MultipartFormDataContent())
            {
                using (var streamContent = new StreamContent(req.FileStream))
                {
                    streamContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");

                    formData.Add(streamContent, "file", req.FileName);
                    formData.Add(new StringContent("srt"), "response_format");
                    formData.Add(new StringContent("whisper-1"), "model");

                    var response = await _httpClient.PostAsync($"{_openAiApiUrl}/v1/audio/translations", formData);

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new Exception($"Erro ao chamar a API de Translation: {response.ReasonPhrase}");
                    }

                    var buffer = await response.Content.ReadAsByteArrayAsync();

                    MemoryStream memoryStream = new MemoryStream(buffer);

                    return memoryStream;
                }
            }
        }
        catch (Exception e)
        {
            throw e;
        }
    }

    public async Task<byte[]> Speech(SpeechRequest req, string responseFormat)
    {
        try
        {
            var jsonObject = new
            {
                input = req.Input,
                voice = "alloy",
                model = "tts-1",
                response_format = responseFormat
            };

            var jsonBody = JsonSerializer.Serialize(jsonObject);

            var content = new StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_openAiApiUrl}/v1/audio/speech", content);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Erro ao chamar a API de Speech: {response.ReasonPhrase}");
            }

            return await response.Content.ReadAsByteArrayAsync();
        }
        catch (Exception e)
        {
            throw e;
        }
    }

    public async Task<string> Chat()
    {
        return "oi";
    }
}    
    
