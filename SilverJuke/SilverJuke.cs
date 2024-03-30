using System.ComponentModel;
using System.Composition;
using System.Diagnostics;
using System.Xml.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using SilverAudioPlayer.Shared;
using SilverConfig;

namespace SilverAudioPlayer.Any.SilverJuke;
public class JukeConfig : INotifyPropertyChanged, ICanBeToldThatAPartOfMeIsChanged
{
    [Comment("Should the web interface allow loading URLs")]

    public bool AllowLoading { get; set; } = false;
    public bool ReadOnly { get; set; } = false;
    public bool UseSwagger { get; set; } = false;
    public bool UseSwaggerUI { get; set; } = false;

    void ICanBeToldThatAPartOfMeIsChanged.PropertyChanged(object e, PropertyChangedEventArgs a)
    {
        PropertyChanged?.Invoke(e, a);
    }
    [XmlIgnore] public bool AllowedToRead { get; set; } = true;

    public event PropertyChangedEventHandler? PropertyChanged;
}
[Export(typeof(IMusicStatusInterface))]
public class SilverJuke : IMusicStatusInterface, IAmOnceAgainAskingYouForYourMemory
{

    public ObjectToRemember ConfigObject =
           new(Guid.Parse("e5552a99-dfee-4eaa-b846-5c5a4180ae32"), new JukeConfig());

    public IEnumerable<ObjectToRemember> ObjectsToRememberForMe => [ConfigObject];
    public string Name => "SilverJuke";

    public string Description => "A SilverJuke server. Provides a simple http interface to control the audio player. You can access it yourself at http://localhost:36169";

    public WrappedStream? Icon => new WrappedEmbeddedResourceStream(typeof(SilverJuke).Assembly,
        "SilverAudioPlayer.Any.SilverJuke.juke.svg");


    public Version? Version => typeof(SilverJuke).Assembly.GetName().Version;

    public string Licenses => "SilverJuke is licensed under the GPL 3.0 license.\n";

    public List<Tuple<Uri, URLType>>? Links => [];
    public bool IsStarted => _IsStarted;
    private bool _IsStarted;
    public void Dispose()
    {
        app?.DisposeAsync();
    }
    WebApplication? app;

    IMusicStatusInterfaceListener listener;
    private ILogger _logger;
    JukeConfig jukeConfig;
    public void StartIPC(IMusicStatusInterfaceListener listener)
    {
        jukeConfig = (JukeConfig)ConfigObject.Value;
        _logger = Logger.GetLogger(typeof(SilverJuke));
        _IsStarted = true;
        this.listener = listener;
        var builder = WebApplication.CreateBuilder();
        builder.Host.UseSerilog(_logger);
        builder.WebHost.UseUrls("http://localhost:36169");
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        app = builder.Build();
        if (jukeConfig.UseSwagger)
        {
            app.UseSwagger();
            if (jukeConfig.UseSwaggerUI)
            {
                app.UseSwaggerUI();
                Links.Add(new(new("http://localhost:36169/swagger/index.html"), URLType.Unknown));
            }
        }
        if (!jukeConfig.ReadOnly)
        {
            app.MapPost("/position", listener.SetPosition).WithName("SetPosition");
            app.MapGet("/play", listener.Play).WithName("Play");
            app.MapGet("/pause", listener.Pause).WithName("Pause");
            app.MapGet("/playpause", listener.PlayPause).WithName("PlayPause");
            app.MapGet("/next", listener.Next).WithName("Next");
            app.MapGet("/previous", listener.Previous).WithName("Previous");
            app.MapGet("/stop", listener.Stop).WithName("Stop");
        }
        app.MapGet("/state", listener.GetState).WithName("GetState");
        app.MapGet("/track", listener.GetCurrentTrack).WithName("GetCurrentTrack");
        app.MapGet("/duration", () => listener.GetPosition).WithName("GetDuration");
        app.MapGet("/position", listener.GetPosition).WithName("GetPosition");
        app.MapGet("/lyrics", listener.GetLyrics).WithName("GetLyrics");

        app.MapGet("/albumart", () =>
        {
            Thread.CurrentThread.Priority = ThreadPriority.Lowest;
            var currentTrack = listener.GetCurrentTrack();
            if (currentTrack?.Metadata == null || currentTrack?.Metadata?.Pictures?.Count == 0)
            {
                return Results.NoContent();
            }
            var x = listener.GetBestRepresentation(currentTrack?.Metadata?.Pictures);
            return x == null ? Results.NoContent() : Results.Stream(x.Data.GetStream(), x.Data.MimeType.Common, enableRangeProcessing: true);
        }).WithName("GetBestRepresentation");
        if (jukeConfig.AllowLoading && listener is IPlayStreamProviderListener streamProviderListener)
        {
            app.MapGet("/loadfile", (string file) =>
            {
                streamProviderListener.ProcessFiles(new List<string> { file });
            }).WithName("ProcessFile");
            app.MapGet("/loadfiles", ([FromBody] string[] files) =>
            {
                streamProviderListener.ProcessFiles(files.ToList());
            }).WithName("ProcessFiles");
        }
        Task.Run(() =>
        {
            Thread.CurrentThread.Priority = ThreadPriority.Lowest;
            app.Start();
        });
    }

    public void StopIPC(IMusicStatusInterfaceListener listener)
    {
        _IsStarted = false;
        Task.Run(async () =>
        {
            await app?.StopAsync();
        });
    }
}