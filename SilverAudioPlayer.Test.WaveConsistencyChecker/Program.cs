using Silver.Composition;
using SilverAudioPlayer.NAudio;
using SilverAudioPlayer.Shared;
PlatformLogicHelper.AddManualAssembly(typeof(NaudioWaveStreamWrapperTypeHolder).Assembly);
BootupLogic bl=new();
bl.ConfigureConfigPath();
string inputFile="DDTS.mp3";
using var fileStream= new WrappedFileStream(inputFile);
var wrappersC = new  NaudioWaveStreamWrapperTypes();
var wrappers = wrappersC.GetWrappers();
Console.WriteLine(wrappers.Count);
var wrappersToTest = wrappers.Where(x=>x.SupportedMimeTypes.Contains(fileStream.MimeType)).ToList();
List<List<byte>> Data=new();
Console.WriteLine(wrappersToTest.Count);
foreach(var wrapper in wrappersToTest)
{
    List<byte> data=new();
    using var fs=fileStream.GetStream();
    using var nas=wrapper.GetStream(fs);
    using var fsWRaw= File.OpenWrite($"{inputFile}{wrapper.GetType().Name}.raw{nas.WaveFormat.BitsPerSample}x{nas.WaveFormat.SampleRate}x{nas.WaveFormat.Channels}ch");
    Span<byte> buffer=new byte[nas.BlockAlign];
    while(nas.CanRead)
    {
        var r = nas.Read(buffer);
        data.AddRange(buffer[..r]);
        fsWRaw.Write(buffer[..r]);
        if(r<nas.BlockAlign)
        {
            break;
        }
    }
}
