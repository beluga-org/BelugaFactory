using System.Diagnostics;

namespace BelugaFactory.Services.Encoding;

public class FfmpegService
{
    private readonly string _ffmpegPath = @"C:\Users\Joao Lucas\Sandbox\BelugaFactory\Ffmpeg\Bin\ffmpeg.exe";

    public void EncodeWithArgs(string args)
    {
        using (Process process = new Process())
        {
            process.StartInfo = new ProcessStartInfo
            {
                FileName = _ffmpegPath,
                Arguments = args,
                RedirectStandardOutput = false, //true
                RedirectStandardError = false, //true
                RedirectStandardInput = false, //true
                UseShellExecute = false,
                CreateNoWindow = false
            };
            
            process.Start();
            process.WaitForExit(); //process.WaitForExitAsync();
            
            if (process.ExitCode != 0)
            {
                throw new Exception($"FFmpeg falhou com o código de saída: {process.ExitCode}");
            }
        }
    }    
    
    public async Task EncodeWithArgsAndInput(string args, Stream inputStream)
    {
        using (Process process = new Process())
        {
            process.StartInfo = new ProcessStartInfo
            {
                FileName = _ffmpegPath,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
          
            using (var binaryWriter = new BinaryWriter(process.StandardInput.BaseStream))
            {
                inputStream.Position = 0; 
                inputStream.CopyTo(binaryWriter.BaseStream);
            }
            
            process.Start();
            
            await process.WaitForExitAsync();
            
            if (process.ExitCode != 0)
            {
                throw new Exception($"FFmpeg falhou com o código de saída: {process.ExitCode}");
            }
        }
    }

    public void testClass()
    {
        Console.WriteLine("AOBAA");
    }
}