namespace BelugaFactory.Services.Api.Results;

public class VideoResult
{
    public string id { get; set; }
    public string title { get; set; }
    public string originalLanguage{ get; set; }
    public string? content { get; set; }
    public string originalUrl { get; set; }
    public string userId { get; set; }
    public DateTime created { get; set; }
    public DateTime updated { get; set; }
}