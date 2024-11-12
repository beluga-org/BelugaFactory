namespace BelugaFactory.Services.Api.Results;

public class TranslationResult
{
    public string id { get; set; }
    public string language { get; set; }
    public string videoId { get; set; }
    public string status { get; set; }
    public string? translationUrl { get; set; }
    public string? subtitleUrl { get; set; }
    public DateTime created { get; set; }
    public DateTime updated { get; set; }
    public DateTime? deleted { get; set; }
}