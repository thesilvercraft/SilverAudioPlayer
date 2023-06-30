using System.Composition;
using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SilverAudioPlayer.Shared;

namespace SilverAudioPlayer.Any.SilverJuke;

[Export(typeof(IMusicStatusInterface))]
public class SilverJuke : IMusicStatusInterface
{
    public string Name => "SilverJuke";

    public string Description => "SilverJuke";

    public WrappedStream? Icon => null;

    public Version? Version => null;

    public string Licenses => "";

    public List<Tuple<Uri, URLType>>? Links => new();

    public void Dispose()
    {
        app.DisposeAsync();
    }
    WebApplication? app;
    IMusicStatusInterfaceListener listener;
    public void StartIPC(IMusicStatusInterfaceListener listener)
    {
        this.listener = listener;
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://localhost:36169");
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        app = builder.Build();
        if (Debugger.IsAttached)
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }
        app.MapGet("/state", () => listener.GetState()).WithName("GetState");
        app.MapGet("/track", () => listener.GetCurrentTrack()).WithName("GetCurrentTrack");
        app.MapGet("/duration", () => listener.GetPosition).WithName("GetDuration");
        app.MapGet("/position", listener.GetPosition).WithName("GetPosition");
        app.MapPost("/position", listener.SetPosition).WithName("SetPosition");
        app.MapGet("/lyrics", listener.GetLyrics).WithName("GetLyrics");
        app.MapGet("/play", listener.Play).WithName("Play");
        app.MapGet("/pause", listener.Pause).WithName("Pause");
        app.MapGet("/playpause", listener.PlayPause).WithName("PlayPause");
        app.MapGet("/next", listener.Next).WithName("Next");
        app.MapGet("/previous", listener.Previous).WithName("Previous");
        app.MapGet("/stop", listener.Stop).WithName("Stop");
        app.MapGet("/albumart", () =>
        {
            var currentTrack = listener.GetCurrentTrack();
            if ( currentTrack?.Metadata == null || currentTrack?.Metadata?.Pictures?.Count==0)
            {
                return Results.NoContent();
            }
            var x = listener.GetBestRepresentation(currentTrack?.Metadata?.Pictures);
            return x == null ? Results.NoContent() : Results.Stream(x.Data.GetStream(), x.Data.MimeType.Common);
        }).WithName("GetBestRepresentation");
        if(listener is IPlayStreamProviderListener streamProviderListener)
        {
            app.MapGet("/loadfile", (string file) =>
            {
                streamProviderListener.ProcessFiles(new List<string> { file });
            }).WithName("ProcessFile");
            app.MapGet("/loadfiles", ([FromBody] string[] files) =>
            {
                streamProviderListener.ProcessFiles(files.ToList() );
            }).WithName("ProcessFiles");
        }
        Task.Run(() =>
        {
            app.Start();
        });
    }

    public void StopIPC(IMusicStatusInterfaceListener listener)
    {
        Task.Run(async () =>
        {
            await app?.StopAsync();
        });
    }
}