namespace BelugaFactory.Common;

public class SendTranslationRequest
{
    public string VideoId { get; set; }
    public string OriginLanguage { get; set; }
    public string TargetLanguage { get; set; }
    public bool? HasTranscription { get; set; }
}

