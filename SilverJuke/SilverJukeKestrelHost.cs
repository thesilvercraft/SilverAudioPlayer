using System.Net;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using SilverAudioPlayer.Shared;

namespace SilverAudioPlayer.Any.SilverJuke;

internal static class SilverJukeKestrelHost
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static WebApplication Start(
        JukeConfig jukeConfig,
        IMusicStatusInterfaceListener listener,
        List<Tuple<Uri, URLType>> links,
        int port = 36169)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton<IHostLifetime, NotAHostLifeTime>();
        builder.Host.UseSerilog();
        builder.WebHost.UseUrls($"http://localhost:{port}");
        builder.Services.AddEndpointsApiExplorer();

        if (jukeConfig.UseSwagger)
        {
            builder.Services.AddSwaggerGen();
        }

        var app = builder.Build();
        links.Add(new(new($"http://localhost:{port}/state"), URLType.Unknown));

        if (jukeConfig.UseSwagger)
        {
            app.UseSwagger();

            if (jukeConfig.UseSwaggerUI)
            {
                app.UseSwaggerUI();
                links.Add(new(new($"http://localhost:{port}/swagger/index.html"), URLType.Unknown));
            }
        }

        IResult GuardReadOnly(Action action)
        {
            if (jukeConfig.ReadOnly) return Results.StatusCode(StatusCodes.Status403Forbidden);
            action();
            return Results.Ok();
        }

        app.MapPost("/position", async (HttpContext ctx, [FromQuery] double? position) =>
        {
            if (jukeConfig.ReadOnly) return Results.StatusCode(StatusCodes.Status403Forbidden);
            double? pos = position;
            if (!pos.HasValue)
            {
                try
                {
                    pos = await ctx.Request.ReadFromJsonAsync<double>();
                }
                catch
                {
                    return Results.BadRequest();
                }
            }

            if (pos.HasValue)
            {
                listener.SetPosition(pos.Value);
                return Results.Ok();
            }

            return Results.BadRequest();
        }).WithName("SetPosition");

        app.MapGet("/play", () => GuardReadOnly(listener.Play)).WithName("Play");
        app.MapGet("/pause", () => GuardReadOnly(listener.Pause)).WithName("Pause");
        app.MapGet("/playpause", () => GuardReadOnly(listener.PlayPause)).WithName("PlayPause");
        app.MapGet("/next", () => GuardReadOnly(listener.Next)).WithName("Next");
        app.MapGet("/previous", () => GuardReadOnly(listener.Previous)).WithName("Previous");
        app.MapGet("/stop", () => GuardReadOnly(listener.Stop)).WithName("Stop");

        app.MapGet("/", () =>
        {
            var asm = typeof(SilverJuke).Assembly;
            var stream = asm.GetManifestResourceStream("SilverAudioPlayer.Any.SilverJuke.SilverJukeWebUi.html");
            return stream == null ? Results.NotFound() : Results.File(stream, "text/html");
        }).WithName("Index");

        if (jukeConfig.UseSlopUI)
        {
            links.Add(new(new($"http://localhost:{port}/"), URLType.Unknown));
            links.Add(new(new($"http://localhost:{port}/inline-remote"), URLType.Unknown));

            app.MapGet("/inline-remote", () =>
            {
                var asm = typeof(SilverJuke).Assembly;
                var stream = asm.GetManifestResourceStream("SilverAudioPlayer.Any.SilverJuke.SilverJukeWebInline.html");
                return stream == null ? Results.NotFound() : Results.File(stream, "text/html");
            }).WithName("InlineRemote");
            app.MapGet("/checkifreadonly", () => jukeConfig.ReadOnly).WithName("CheckIfReadonly");

            app.MapGet("/SilverJukeWebUi.js", () =>
            {
                var asm = typeof(SilverJuke).Assembly;
                var stream = asm.GetManifestResourceStream("SilverAudioPlayer.Any.SilverJuke.SilverJukeWebUi.js");
                return stream == null ? Results.NotFound() : Results.File(stream, "application/javascript");
            }).WithName("IndexJS");

            app.MapGet("/inline-remote-inkscape.svg", () =>
            {
                var asm = typeof(SilverJuke).Assembly;
                var stream = asm.GetManifestResourceStream("SilverAudioPlayer.Any.SilverJuke.inline-remote-inkscape.svg");
                return stream == null ? Results.NotFound() : Results.File(stream, "image/svg+xml");
            }).WithName("InlineRemoteSVG");

            app.MapGet("/DSEG14Classic-Regular.woff2", () =>
            {
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
            if (currentTrack?.Metadata == null || currentTrack.Metadata.Pictures?.Count == 0)
            {
                return Results.NoContent();
            }

            var x = listener.GetBestRepresentation(currentTrack.Metadata.Pictures);
            return x?.Data == null
                ? Results.NoContent()
                : Results.Stream(x.Data.GetStream(), x.Data.MimeType.Common, enableRangeProcessing: true);
        }).WithName("GetBestRepresentation");

        if (jukeConfig.AllowLoading && listener is IPlayStreamProviderListener streamProviderListener)
        {
            app.MapPost("/loadfile", (HttpContext ctx, string file) =>
            {
                if (!jukeConfig.AllowLoading || !SilverJuke.IsLocalOrLan(ctx.Connection.RemoteIpAddress))
                    return Results.StatusCode(403);
                streamProviderListener.ProcessFiles([file]);
                return Results.Ok();
            }).WithName("ProcessFile");

            app.MapPost("/loadfiles", (HttpContext ctx, [FromBody] string[] files) =>
            {
                if (!jukeConfig.AllowLoading || !SilverJuke.IsLocalOrLan(ctx.Connection.RemoteIpAddress))
                    return Results.StatusCode(403);
                streamProviderListener.ProcessFiles(files.ToList());
                return Results.Ok();
            }).WithName("ProcessFiles");
        }

        app.Start();
        return app;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Stop(WebApplication app)
    {
        try
        {
            app.StopAsync().GetAwaiter().GetResult();
        }
        finally
        {
            app.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }
}

public class NotAHostLifeTime : IHostLifetime
{
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task WaitForStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
