namespace BelugaFactory.Services.Processing.Results;

public class TranscriptResult
{
    public string task { get; set; }
    public string language { get; set; }
    public decimal duration { get; set; }
    public string text { get; set; }
    public List<SegmentResult> segments { get; set; }
}

public class SegmentResult 
{
    public int id { get; set; }
    public int seek { get; set; }
    public decimal start { get; set; }
    public decimal end { get; set; }
    public string text { get; set; }
    public List<int> tokens { get; set; }
    public decimal temperature { get; set; }
    public decimal avg_logprob { get; set; }
    public decimal compression_ratio { get; set; }
    public decimal no_speech_prob { get; set; }
}



