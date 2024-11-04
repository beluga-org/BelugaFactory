using System.Text.RegularExpressions;
using Beluga_Functions.Common;
using BelugaFactory.Common;
using BelugaFactory.Services.Encoding;
using BelugaFactory.Services.Processing;
using BelugaFactory.Services.Storage;
using BelugaFactory.Services.Processing.Requests;

namespace BelugaFactory;
static class Program
{
    private static readonly FfmpegService FfmpegService = new FfmpegService();
    private static readonly AzureStorageService AzureStorageService = new AzureStorageService();
    private static readonly OpenAiService OpenAiService = new OpenAiService();
    private static readonly string TempFolder = Path.Combine(AppContext.BaseDirectory, "Temp");
    
    private static async Task Main(string[] args)
    {
        await AzureStorageService.ProcesQueueMessage("send-translate", ProcessVideo);
    }

    private static async Task ProcessVideo(VideoRequest req)
    {
        InitializeDirectories();
        
        await DownloadToLocal(req);
        
        // CONDITIONAL: if video has transcription jump to translate
        await SpeechToText(req);
        
        await Translate(req);
        
        await TextToSpeech(req);

        Sync(req);
    }

    private static async Task DownloadToLocal(VideoRequest req)
    {
        await AzureStorageService.DownloadBlob("translation", $"{req.From}/{req.BlobName}.mp4", Path.Combine(TempFolder, "original-video", $"{req.BlobName}.mp4"));
        
        FfmpegService.EncodeWithArgs($"-i \"{Path.Combine(TempFolder, "original-video", $"{req.BlobName}.mp4")}\" -an -vcodec copy \"{Path.Combine(TempFolder, "muted-video", $"{req.BlobName}.mp4")}\"");
        
        Console.WriteLine($"DOWNLOAD-LOCAL: {req.BlobName} ---> COMPLETED");
    }
    
    private static async Task SpeechToText(VideoRequest req)
    {
        using (var fileStream = new FileStream(Path.Combine(TempFolder, "original-video", $"{req.BlobName}.mp4"), FileMode.Open))
        {
            TranscriptRequest transcriptionReq = new TranscriptRequest()
            {
                FileStream = fileStream,
                FileName = req.BlobName
            };
            
            var transcriptionStream = await OpenAiService.Transcript(transcriptionReq);
            
            //await AzureStorageService.UploadBlob(transcriptionStream, "application/x-subrip", "transcription", $"{req.From}/{req.BlobName}.srt");
            
            using (var transcriptionFileStream = new FileStream(Path.Combine(TempFolder, "original-transcription", $"{req.BlobName}.srt"), FileMode.Create, FileAccess.Write))
            {
                await transcriptionStream.CopyToAsync(transcriptionFileStream);
            }
        }
        
        Console.WriteLine($"SPEECH-TO-TEXT: {req.BlobName} ---> COMPLETED");
        
    }
    
    private static async Task Translate(VideoRequest req)
    {
        using (var fileStream = new FileStream(Path.Combine(TempFolder, "original-video", $"{req.BlobName}.mp4"), FileMode.Open))
        {
            TranscriptRequest transcriptionReq = new TranscriptRequest()
            {
                FileStream = fileStream,
                FileName = req.BlobName
            };
            
            var transcriptionStream = await OpenAiService.Translate(transcriptionReq);
            
            // await AzureStorageService.UploadBlob(transcriptionStream, "application/x-subrip", "transcription", $"{req.To}/{req.BlobName}.srt");
            
            using (var transcriptionFileStream = new FileStream(Path.Combine(TempFolder, "translated-transcription", $"{req.BlobName}.srt"), FileMode.Create, FileAccess.Write))
            {
                await transcriptionStream.CopyToAsync(transcriptionFileStream);
            }
        }
        
        Console.WriteLine($"TRANSLATE: {req.BlobName} ---> COMPLETED");
        
    }    
    
    private static async Task TextToSpeech(VideoRequest req)
    {
        using (var fileStream =
               new FileStream(Path.Combine(TempFolder, "translated-transcription", $"{req.BlobName}.srt"),
                   FileMode.Open))
        {
            var subtitles = ParseSrtStream(fileStream);
            string concatAudiosStretchs = ""; 
        
            for (int i = 0; i < subtitles.Count; i++)
            {
                concatAudiosStretchs += "file " + $"{req.BlobName}_{i}.aac" + '\n';
                
                using (var audioFileStream =
                       new FileStream(Path.Combine(TempFolder, "translated-audio", $"{req.BlobName}_{i}.aac"),
                           FileMode.Create, FileAccess.Write))
                {
                    using (var memoryStream = new MemoryStream())
                    {
                        SpeechRequest speechReq = new SpeechRequest()
                        {
                            Input = subtitles[i].Text
                        };
        
                        byte[] buffer = await OpenAiService.Speech(speechReq, "aac");
        
                        await memoryStream.WriteAsync(buffer, 0, buffer.Length);
        
                        memoryStream.Position = 0;
        
                        await memoryStream.CopyToAsync(audioFileStream);
                    }
                }
            }
            
            using (var audioFileStream =
                   new FileStream(Path.Combine(TempFolder, "translated-audio", $"{req.BlobName}.txt"),
                       FileMode.Create, FileAccess.Write))
            {
                using (var writer = new StreamWriter(audioFileStream))
                {
                    writer.Write(concatAudiosStretchs); 
                }
            }
            
        }
        
        FfmpegService.EncodeWithArgs(
            "-f concat " +
            $"-i \"{Path.Combine(TempFolder, "translated-audio", $"{req.BlobName}.txt")}\" " +
            $"-c:a copy \"{Path.Combine(TempFolder, "translated-audio", $"{req.BlobName}.aac")}\"");
        
        // await AzureStorageService.UploadBlob(transcriptionStream, "application/x-subrip", "transcription", $"{req.To}/{req.BlobName}.srt");
        
        Console.WriteLine($"TEXT-TO-SPEECH: {req.BlobName} ---> COMPLETED");
    }

    private static void Sync(VideoRequest req)
    {
        FfmpegService.EncodeWithArgs(
            $"-i \"{Path.Combine(TempFolder, "muted-video", req.BlobName + ".mp4")}\" " + 
            $"-i \"{Path.Combine(TempFolder, "translated-audio", req.BlobName + ".aac")}\" " + 
            $"-c:v copy -c:a aac \"{Path.Combine(TempFolder, "translated-video", req.BlobName + ".mp4")}\"");

        // await AzureStorageService.UploadBlob(transcriptionStream, "application/x-subrip", "transcription", $"{req.To}/{req.BlobName}.srt");
        
        Console.WriteLine($"SYNC: {req.BlobName} ---> COMPLETED");
    }
    
    private static List<SubtitleResult> ParseSrtStream(Stream stream)
    {
        var subtitles = new List<SubtitleResult>();

        using (StreamReader reader = new StreamReader(stream))
        {
            string srt = reader.ReadToEnd();

            var regex = new Regex(@"(?<Index>\d+)\s*?\r?\n(?<StartTime>[\d:,]+)\s-->\s(?<EndTime>[\d:,]+)\s*?\r?\n(?<Text>.*?)(?=\r?\n\d+|\z)", RegexOptions.Singleline);

            foreach (Match match in regex.Matches(srt))
            {
                subtitles.Add(new SubtitleResult
                {
                    Index = int.Parse(match.Groups["Index"].Value),
                    StartTime = match.Groups["StartTime"].Value,
                    EndTime = match.Groups["EndTime"].Value,
                    Text = match.Groups["Text"].Value.Trim()
                });
            }
        }

        return subtitles;
    }

    private static void InitializeDirectories()
    {
        if (Directory.Exists(TempFolder))
            Directory.Delete(TempFolder, true);

        Directory.CreateDirectory(TempFolder);
        Directory.CreateDirectory(Path.Combine(TempFolder, "original-video"));
        Directory.CreateDirectory(Path.Combine(TempFolder, "muted-video"));
        Directory.CreateDirectory(Path.Combine(TempFolder, "original-transcription"));
        Directory.CreateDirectory(Path.Combine(TempFolder, "translated-transcription"));
        Directory.CreateDirectory(Path.Combine(TempFolder, "translated-audio"));
        Directory.CreateDirectory(Path.Combine(TempFolder, "translated-video"));

    }
}