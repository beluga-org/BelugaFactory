using BelugaFactory.Common.WebClient;
using BelugaFactory.Config;
using BelugaFactory.Services.Api.Requests;
using BelugaFactory.Services.Api.Results;
using BelugaFactory.Services.Processing.Requests;

namespace BelugaFactory.Services.Api;

public class BelugaClient
{
    private string ApiEndpoint { get; set; }
    
    public BelugaClient()
    {
        ApiEndpoint = EnvironmentSettings.BelugaAPI + "/api";
    }

    public async Task<TranslationResult> CreateTranslation(TranslationRequest req)
    {
        try
        {
            WebClientOfT<TranslationResult> client = new WebClientOfT<TranslationResult>();
            var result = await client.PostJsonAsync($"{ApiEndpoint}/translation", req);
            return result;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }    
    
    public async Task<TranslationResult> UpdateTranslation(string translationId, TranslationRequest req)
    {
        try
        {
            WebClientOfT<TranslationResult> client = new WebClientOfT<TranslationResult>();
            var result = await client.PutAsJsonAsync($"{ApiEndpoint}/translation/{translationId}", req);
            return result;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }    
    
    public async Task<VideoResult> AddContentToVideo(string videoId, AddContentRequest req)
    {
        try
        {
            WebClientOfT<VideoResult> client = new WebClientOfT<VideoResult>();
            var result = await client.PutAsJsonAsync($"{ApiEndpoint}/video/{videoId}/content", req);
            return result;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
}