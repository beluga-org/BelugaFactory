namespace BelugaFactory.Services.Api.Requests;

public class TranslationRequest
{
    public string language { get; set; }
    public string videoId { get; set; }
    public string status { get; set; }
    public string? translationUrl { get; set; }
    public string? subtitleUrl { get; set; }
}