
using System.ComponentModel;
using System.Composition;
using System.Xml.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using SilverAudioPlayer.Shared;
using SilverConfig;

namespace SilverAudioPlayer.Any.SilverJuke;
public class JukeConfig : INotifyPropertyChanged, ICanBeToldThatAPartOfMeIsChanged
{
    [Comment("Should the web interface allow loading URLs")]

    public bool AllowLoading { get; set; } = true;
    public bool ReadOnly { get; set; } = false;
    public bool UseSwagger { get; set; } = false;
    public bool UseSwaggerUI { get; set; } = false;
    [Comment("Should the web interface show a basic html/js interface")]

    public bool UseSlopUI { get; set; } = true;

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

    public string Description => "A SilverJuke server. Provides a simple http interface to control the audio player. Access it at http://localhost:36169";

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
        builder.Services.AddSingleton<IHostLifetime, NotAHostLifeTime>();
        builder.Host.UseSerilog(_logger);
    
        builder.WebHost.UseUrls("http://localhost:36169");
        builder.Services.AddEndpointsApiExplorer();
        if (jukeConfig.UseSwagger)
        {
            builder.Services.AddSwaggerGen();
        }
        app = builder.Build();
        Links.Add(new(new("http://localhost:36169/state"), URLType.Unknown));
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
        app.MapGet("/", () => {
            var asm = typeof(SilverJuke).Assembly;
            var stream = asm.GetManifestResourceStream("SilverAudioPlayer.Any.SilverJuke.SilverJukeWebUi.html");
            return stream == null ? Results.NotFound() : Results.File(stream, "text/html");
        }).WithName("Index");
        if (jukeConfig.UseSlopUI)
        {
            Links.Add(new(new("http://localhost:36169/"), URLType.Unknown));
            Links.Add(new(new("http://localhost:36169/inline-remote"), URLType.Unknown));
            app.MapGet("/inline-remote", () => {
                var asm = typeof(SilverJuke).Assembly;
                var stream = asm.GetManifestResourceStream("SilverAudioPlayer.Any.SilverJuke.SilverJukeWebInline.html");
                return stream == null ? Results.NotFound() : Results.File(stream, "text/html");
            }).WithName("InlineRemote");
            app.MapGet("/checkifreadonly", () => jukeConfig.ReadOnly).WithName("CheckIfReadonly");
            app.MapGet("/SilverJukeWebUi.js", () => {
                var asm = typeof(SilverJuke).Assembly;
                var stream = asm.GetManifestResourceStream("SilverAudioPlayer.Any.SilverJuke.SilverJukeWebUi.js");
                return stream == null ? Results.NotFound() : Results.File(stream, "application/javascript");
            }).WithName("IndexJS");
            app.MapGet("/inline-remote-inkscape.svg", () => {
                var asm = typeof(SilverJuke).Assembly;
                var stream = asm.GetManifestResourceStream("SilverAudioPlayer.Any.SilverJuke.inline-remote-inkscape.svg");
                return stream == null ? Results.NotFound() : Results.File(stream, "image/svg+xml");
            }).WithName("InlineRemoteSVG");
            app.MapGet("/DSEG14Classic-Regular.woff2", () => {
                var asm = typeof(SilverJuke).Assembly;
                var stream = asm.GetManifestResourceStream("SilverAudioPlayer.Any.SilverJuke.DSEG14Classic-Regular.woff2");
                return stream == null ? Results.NotFound() : Results.File(stream, "font/woff2");
            }).WithName("DSEG14FontWoff2");
        }
 

        app.MapGet("/state", listener.GetState).WithName("GetState");
        app.MapGet("/track", listener.GetCurrentTrack).WithName("GetCurrentTrack");
        app.MapGet("/duration", listener.GetDuration).WithName("GetDuration");
        app.MapGet("/position", listener.GetPosition).WithName("GetPosition");
        app.MapGet("/lyrics", listener.GetLyrics).WithName("GetLyrics");

        app.MapGet("/albumart", () =>
        {
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
            bool IsLocalOrLan(HttpContext ctx)
            {
                var ip = ctx.Connection.RemoteIpAddress;
                if (ip == null) return false;
                if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal) return true;
                if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
                // localhost
                if (ip.Equals(System.Net.IPAddress.Loopback) || ip.Equals(System.Net.IPAddress.IPv6Loopback)) return true;
                // 127.0.0.0/8
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && ip.ToString().StartsWith("127.")) return true;
                // LAN: 10.0.0.0/8, 192.168.0.0/16, 172.16.0.0/12
                var s = ip.ToString();
                if (s.StartsWith("10.")) return true;
                if (s.StartsWith("192.168.")) return true;
                if (s.StartsWith("172."))
                {
                    var parts = s.Split('.');
                    if (parts.Length > 1 && int.TryParse(parts[1], out int b) && b >= 16 && b <= 31) return true;
                }
                return false;
            }
            app.MapPost("/loadfile", (HttpContext ctx, string file) =>
            {
                if (!IsLocalOrLan(ctx)) return Results.StatusCode(403);
                streamProviderListener.ProcessFiles(new List<string> { file });
                return Results.Ok();
            }).WithName("ProcessFile");
            app.MapPost("/loadfiles", (HttpContext ctx, [FromBody] string[] files) =>
            {
                if (!IsLocalOrLan(ctx)) return Results.StatusCode(403);
                streamProviderListener.ProcessFiles(files.ToList());
                return Results.Ok();
            }).WithName("ProcessFiles");
        }
        app.Start();
    }

    public void StopIPC(IMusicStatusInterfaceListener listener)
    {
        _IsStarted = false;
        Task.Run(async () =>
        {
            if (app != null)
            {
                await app.StopAsync();
            }
        });
    }
}

public class NotAHostLifeTime : IHostLifetime
{
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task WaitForStartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}