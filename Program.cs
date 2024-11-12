using System.Text;
using System.Text.RegularExpressions;
using BelugaFactory.Common;
using BelugaFactory.Services.Api;
using BelugaFactory.Services.Api.Requests;
using BelugaFactory.Services.Api.Results;
using BelugaFactory.Services.Encoding;
using BelugaFactory.Services.Processing;
using BelugaFactory.Services.Storage;
using BelugaFactory.Services.Processing.Requests;

//TODO: CRIAR A AS ATUALIZAÇÕES E CHECK-UPS PARA A API
//TODO: SUBIR O CONTENT EM BASE 64 PARA O VIDEO NO SPEECH-TO-TEXT

namespace BelugaFactory;
static class Program
{
    private static readonly FfmpegService FfmpegService = new FfmpegService();
    private static readonly AzureStorageService AzureStorageService = new AzureStorageService();
    private static readonly OpenAiService OpenAiService = new OpenAiService();
    private static readonly BelugaClient BelugaClient = new BelugaClient();
    private static readonly string TempFolder = Path.Combine(AppContext.BaseDirectory, "Temp");
    
    private static async Task Main(string[] args)
    {
        await AzureStorageService.ProcesQueueMessage("send-translate", ProcessVideo);
    }

    private static async Task ProcessVideo(SendTranslationRequest req)
    {
        try
        {
            InitializeDirectories();
        
            var translationProcess = await BelugaClient.CreateTranslation(new TranslationRequest()
            {
                language = req.TargetLanguage,
                status = "STARTED",
                videoId = req.VideoId
            });
        
            await DownloadToLocal(translationProcess);
        
            // CONDITIONAL: if video has transcription jump to translate
            await SpeechToText(translationProcess);
        
            await Translate(translationProcess);
        
            await TextToSpeech(translationProcess);

            await Sync(translationProcess);
            
            await BelugaClient.UpdateTranslation( translationProcess.id, new TranslationRequest() 
                { status = "COMPLETED" }
            );
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    private static async Task DownloadToLocal(TranslationResult req)
    {
        try
        {
            await AzureStorageService.DownloadBlob("originals", $"{req.videoId}", Path.Combine(TempFolder, "original-video", $"{req.videoId}.mp4"));
        
            FfmpegService.EncodeWithArgs($"-i \"{Path.Combine(TempFolder, "original-video", $"{req.videoId}.mp4")}\" -an -vcodec copy \"{Path.Combine(TempFolder, "muted-video", $"{req.videoId}.mp4")}\"");
        
            Console.WriteLine($"DOWNLOAD-LOCAL: {req.videoId} ---> COMPLETED");
        }
        catch (Exception e)
        {
            await BelugaClient.UpdateTranslation( req.id, new TranslationRequest() { status = "FAILED" });
            throw e;
        }
    }
    
    private static async Task SpeechToText(TranslationResult req)
    {
        try
        {
            using (var fileStream = new FileStream(Path.Combine(TempFolder, "original-video", $"{req.videoId}.mp4"), FileMode.Open))
            {
                TranscriptRequest transcriptionReq = new TranscriptRequest()
                {
                    FileStream = fileStream,
                    FileName = req.videoId
                };
            
                var transcriptionStream = await OpenAiService.Transcript(transcriptionReq);
                
                using (var transcriptionFileStream = new FileStream(Path.Combine(TempFolder, "original-transcription", $"{req.videoId}.srt"), FileMode.Create, FileAccess.Write))
                {
                    await transcriptionStream.CopyToAsync(transcriptionFileStream);
                }
                
                string transcriptionText;
                using (var reader = new StreamReader(Path.Combine(TempFolder, "original-transcription", $"{req.videoId}.srt")))
                {
                    transcriptionText = await reader.ReadToEndAsync();
                }
                
                string base64Transcription = Convert.ToBase64String(Encoding.UTF8.GetBytes(transcriptionText));
                
                await BelugaClient.AddContentToVideo(req.videoId, new AddContentRequest { content = base64Transcription });
            }
        
            Console.WriteLine($"SPEECH-TO-TEXT: {req.videoId} ---> COMPLETED");
        }
        catch (Exception ex)
        {
            // Third-part api
            await BelugaClient.UpdateTranslation( req.id, new TranslationRequest() { status = "FAILED" });
            throw ex;
        }
    }
    
    private static async Task Translate(TranslationResult req)
    {
        try
        {
            using (var fileStream = new FileStream(Path.Combine(TempFolder, "original-video", $"{req.videoId}.mp4"), FileMode.Open))
            {
                TranscriptRequest transcriptionReq = new TranscriptRequest()
                {
                    FileStream = fileStream,
                    FileName = req.videoId
                };
            
                var transcriptionStream = await OpenAiService.Translate(transcriptionReq);
            
                using (var transcriptionFileStream = new FileStream(Path.Combine(TempFolder, "translated-transcription", $"{req.videoId}.srt"), FileMode.Create, FileAccess.Write))
                {
                    await transcriptionStream.CopyToAsync(transcriptionFileStream);
                }
            }
        
            Console.WriteLine($"TRANSLATE: {req.videoId} ---> COMPLETED");

        }
        catch (Exception e)
        {
            await BelugaClient.UpdateTranslation( req.id, new TranslationRequest() { status = "FAILED" });
            throw e;
        }
    }    
    
    private static async Task TextToSpeech(TranslationResult req)
    {
        try
        {
            using (var fileStream =
                   new FileStream(Path.Combine(TempFolder, "translated-transcription", $"{req.videoId}.srt"),
                       FileMode.Open))
            {
                var subtitles = ParseSrtStream(fileStream);
                string concatAudiosStretchs = ""; 
            
                for (int i = 0; i < subtitles.Count; i++)
                {
                    concatAudiosStretchs += "file " + $"{req.videoId}_{i}.aac" + '\n';
                    
                    using (var audioFileStream =
                           new FileStream(Path.Combine(TempFolder, "translated-audio", $"{req.videoId}_{i}.aac"),
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
                       new FileStream(Path.Combine(TempFolder, "translated-audio", $"{req.videoId}.txt"),
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
                $"-i \"{Path.Combine(TempFolder, "translated-audio", $"{req.videoId}.txt")}\" " +
                $"-c:a copy \"{Path.Combine(TempFolder, "translated-audio", $"{req.videoId}.aac")}\"");
            
            // await AzureStorageService.UploadBlob(transcriptionStream, "application/x-subrip", "transcription", $"{req.To}/{req.BlobName}.srt");
            
            Console.WriteLine($"TEXT-TO-SPEECH: {req.videoId} ---> COMPLETED");
            
        }
        catch (Exception e)
        {
            await BelugaClient.UpdateTranslation( req.id, new TranslationRequest() { status = "FAILED" });
            throw e;
        }
    }

    private static async Task Sync(TranslationResult req)
    {
        try
        {
            FfmpegService.EncodeWithArgs(
                $"-i \"{Path.Combine(TempFolder, "muted-video", req.videoId + ".mp4")}\" " + 
                $"-i \"{Path.Combine(TempFolder, "translated-audio", req.videoId + ".aac")}\" " + 
                $"-c:v copy -c:a aac \"{Path.Combine(TempFolder, "translated-video", req.videoId + ".mp4")}\"");
        
            using (FileStream 
                   subtitleFileStream = new FileStream(Path.Combine(TempFolder, "translated-transcription", $"{req.videoId}.srt"), FileMode.Open),
                   translationFileStream = new FileStream(Path.Combine(TempFolder, "translated-video", $"{req.videoId}.mp4"), FileMode.Open))
            {
                var subtitleBlob = await AzureStorageService.UploadBlob(subtitleFileStream, "application/x-subrip", "subtitles", $"{req.language}/{req.videoId}");
                var translationBlob = await AzureStorageService.UploadBlob(translationFileStream, "video/mp4", "translations", $"{req.language}/{req.videoId}");

                await BelugaClient.UpdateTranslation(req.id, new TranslationRequest()
                {
                    subtitleUrl = subtitleBlob.Uri.ToString(),
                    translationUrl = translationBlob.Uri.ToString(),
                });
            }
            
            Console.WriteLine($"SYNC: {req.videoId} ---> COMPLETED");
            
        }
        catch (Exception e)
        {
            await BelugaClient.UpdateTranslation( req.id, new TranslationRequest() { status = "FAILED" });
            throw e;
        }
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