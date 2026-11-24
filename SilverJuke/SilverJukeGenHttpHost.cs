using System.Globalization;
using System.Text.Json;
using GenHTTP.Api.Content.IO;
using GenHTTP.Api.Infrastructure;
using GenHTTP.Api.Protocol;
using GenHTTP.Engine.Internal;
using GenHTTP.Modules.IO;
using GenHTTP.Modules.IO.Streaming;
using GenHTTP.Modules.Layouting;
using GenHTTP.Modules.Practices;
using Serilog;
using SilverAudioPlayer.Shared;

namespace SilverAudioPlayer.Any.SilverJuke;

internal static class SilverJukeGenHttpHost
{
    private static readonly JsonSerializerOptions WebJsonOptions = new(JsonSerializerDefaults.Web);

    private static IResponse JsonResponse<T>(IRequest request, T value)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, WebJsonOptions);
        return request.Respond()
            .Status(ResponseStatus.Ok)
            .Content(GenHTTP.Modules.IO.Streaming.ByteArrayContent.Rent(bytes, ContentType.ApplicationJson, null))
            .Build();
    }

    public static IServerHost Start(
        JukeConfig jukeConfig,
        IMusicStatusInterfaceListener listener,
        List<Tuple<Uri, URLType>> links,
        int port = 36169)
    {
        var asm = typeof(SilverJuke).Assembly;

        var layout = Layout.Create();

        layout.Index(Content.From(
            Resource.FromAssembly(asm, "SilverAudioPlayer.Any.SilverJuke.SilverJukeWebUi.html")
                .Type(ContentType.TextHtml)));

        if (jukeConfig.UseSlopUI)
        {
            links.Add(new(new($"http://localhost:{port}/"), URLType.Unknown));
            links.Add(new(new($"http://localhost:{port}/inline-remote"), URLType.Unknown));

            layout.Add("inline-remote", Content.From(
                Resource.FromAssembly(asm, "SilverAudioPlayer.Any.SilverJuke.SilverJukeWebInline.html")
                    .Type(ContentType.TextHtml)));

            layout.Add("SilverJukeWebUi.js", Content.From(
                Resource.FromAssembly(asm, "SilverAudioPlayer.Any.SilverJuke.SilverJukeWebUi.js")
                    .Type(ContentType.TextJavaScript)));

            layout.Add("inline-remote-inkscape.svg", Content.From(
                Resource.FromAssembly(asm, "SilverAudioPlayer.Any.SilverJuke.inline-remote-inkscape.svg")
                    .Type(ContentType.ImageScalableVectorGraphicsXml)));

            layout.Add("DSEG14Classic-Regular.woff2", Content.From(
                Resource.FromAssembly(asm, "SilverAudioPlayer.Any.SilverJuke.DSEG14Classic-Regular.woff2")
                    .Type(ContentType.FontWoff2)));
        }

        IResponse GuardReadOnly(IRequest request, Action action)
        {
            if (jukeConfig.ReadOnly)
            {
                return request.Respond().Status(ResponseStatus.Forbidden).Build();
            }

            action();
            return request.Respond().Status(ResponseStatus.Ok).Build();
        }

        layout.Add(Handler.From(async request =>
        {
            var method = request.Header.Method;
            var path = request.Header.Path.Bytes.Span;

            if (method == RequestMethod.Get)
            {
                if (path.SequenceEqual("/play"u8))
                    return GuardReadOnly(request, listener.Play);
                if (path.SequenceEqual("/pause"u8))
                    return GuardReadOnly(request, listener.Pause);
                if (path.SequenceEqual("/playpause"u8))
                    return GuardReadOnly(request, listener.PlayPause);
                if (path.SequenceEqual("/next"u8))
                    return GuardReadOnly(request, listener.Next);
                if (path.SequenceEqual("/previous"u8))
                    return GuardReadOnly(request, listener.Previous);
                if (path.SequenceEqual("/stop"u8))
                    return GuardReadOnly(request, listener.Stop);
                if (path.SequenceEqual("/state"u8))
                    return JsonResponse(request, listener.GetState());
                if (path.SequenceEqual("/track"u8))
                    return JsonResponse(request, listener.GetCurrentTrack());
                if (path.SequenceEqual("/duration"u8))
                    return JsonResponse(request, listener.GetDuration());
                if (path.SequenceEqual("/position"u8))
                    return JsonResponse(request, listener.GetPosition());
                if (path.SequenceEqual("/lyrics"u8))
                {
                    return request.Respond()
                        .Status(ResponseStatus.Ok)
                        .Content(GenHTTP.Modules.IO.Strings.StringContent.Rent(
                            listener.GetLyrics() ?? string.Empty,
                            ContentType.TextPlain))
                        .Build();
                }

                if (path.SequenceEqual("/albumart"u8))
                {
                    var currentTrack = listener.GetCurrentTrack();
                    if (currentTrack?.Metadata == null || currentTrack.Metadata.Pictures?.Count == 0)
                    {
                        return request.Respond().Status(ResponseStatus.NoContent).Build();
                    }

                    var best = listener.GetBestRepresentation(currentTrack.Metadata.Pictures);
                    if (best?.Data == null)
                    {
                        return request.Respond().Status(ResponseStatus.NoContent).Build();
                    }

                    var resource = new WrappedStreamPictureResource(best.Data);
                    return request.Respond()
                        .Status(ResponseStatus.Ok)
                        .Content(ResourceContent.Rent(resource))
                        .Build();
                }

                if (jukeConfig.UseSlopUI && path.SequenceEqual("/checkifreadonly"u8))
                {
                    return JsonResponse(request, jukeConfig.ReadOnly);
                }

                return request.Respond().Status(ResponseStatus.NotFound).Build();
            }

            if (method == RequestMethod.Post)
            {
                if (path.SequenceEqual("/position"u8))
                {
                    if (jukeConfig.ReadOnly)
                    {
                        return request.Respond().Status(ResponseStatus.Forbidden).Build();
                    }

                    double? pos = null;
                    var queryPos = request.Header.Query.GetEntry("position");
                    if (!string.IsNullOrEmpty(queryPos) &&
                        double.TryParse(queryPos, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedQueryPos))
                    {
                        pos = parsedQueryPos;
                    }

                    if (!pos.HasValue && request.HasBody)
                    {
                        try
                        {
                            await using var bodyStream = request.GetBody()?.AsStream();
                            if (bodyStream is not null)
                            {
                                pos = await JsonSerializer.DeserializeAsync<double>(bodyStream);
                            }
                        }
                        catch
                        {
                            return request.Respond().Status(ResponseStatus.BadRequest).Build();
                        }
                    }

                    if (!pos.HasValue)
                    {
                        return request.Respond().Status(ResponseStatus.BadRequest).Build();
                    }

                    listener.SetPosition(pos.Value);
                    return request.Respond().Status(ResponseStatus.Ok).Build();
                }

                if (jukeConfig.AllowLoading && listener is IPlayStreamProviderListener streamProviderListener)
                {
                    if (path.SequenceEqual("/loadfile"u8))
                    {
                        if (!jukeConfig.AllowLoading || !SilverJuke.IsLocalOrLan(request.Client.Address))
                        {
                            return request.Respond().Status(ResponseStatus.Forbidden).Build();
                        }

                        var file = request.Header.Query.GetEntry("file");
                        if (string.IsNullOrEmpty(file))
                        {
                            return request.Respond().Status(ResponseStatus.BadRequest).Build();
                        }

                        streamProviderListener.ProcessFiles([file]);
                        return request.Respond().Status(ResponseStatus.Ok).Build();
                    }

                    if (path.SequenceEqual("/loadfiles"u8))
                    {
                        if (!jukeConfig.AllowLoading || !SilverJuke.IsLocalOrLan(request.Client.Address))
                        {
                            return request.Respond().Status(ResponseStatus.Forbidden).Build();
                        }

                        string[]? files = null;
                        if (request.HasBody)
                        {
                            try
                            {
                                await using var bodyStream = request.GetBody()?.AsStream();
                                if (bodyStream is not null)
                                {
                                    files = await JsonSerializer.DeserializeAsync<string[]>(bodyStream, WebJsonOptions);
                                }
                            }
                            catch
                            {
                                return request.Respond().Status(ResponseStatus.BadRequest).Build();
                            }
                        }

                        if (files is null)
                        {
                            return request.Respond().Status(ResponseStatus.BadRequest).Build();
                        }

                        streamProviderListener.ProcessFiles(files.ToList());
                        return request.Respond().Status(ResponseStatus.Ok).Build();
                    }
                }

                return request.Respond().Status(ResponseStatus.NotFound).Build();
            }

            return request.Respond().Status(ResponseStatus.NotFound).Build();
        }));

        links.Add(new(new($"http://localhost:{port}/state"), URLType.Unknown));

        if (jukeConfig.UseSwagger)
        {
            Log.Warning("Swagger/OpenAPI in SilverJuke requires UseKestrel=true (skipped in GenHTTP mode to avoid runtime Roslyn compilation)");
        }

        var host = Host.Create()
            .Handler(layout)
            .Defaults()
            .RangeSupport()
            .Port((ushort)port);

        host.StartAsync().GetAwaiter().GetResult();
        return host;
    }

    public static void Stop(IServerHost host)
    {
        host.StopAsync().GetAwaiter().GetResult();
    }

    private sealed class WrappedStreamPictureResource(WrappedStream wrapped) : IResource
    {
        public string? Name => null;
        public DateTime? Modified => null;
        public ContentType? ContentType { get; } = new(wrapped.MimeType.Common);

        public ulong? Length
        {
            get
            {
                using var s = wrapped.GetStream();
                return s.CanSeek ? (ulong)s.Length : null;
            }
        }

        public ValueTask<ulong> CalculateChecksumAsync() => ValueTask.FromResult(0UL);

        public ValueTask<Stream> GetContentAsync() => ValueTask.FromResult(wrapped.GetStream());

        public async ValueTask WriteAsync(IResponseSink sink)
        {
            await using var stream = wrapped.GetStream();
            await stream.CopyToAsync(sink.Stream);
        }
    }
}
