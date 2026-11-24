using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using Serilog;
using SilverAudioPlayer.Shared;

namespace Catenoid;
public static class AvaloniaUtils
{
    public static async Task<bool> SetClipboardImage(this Window window, WrappedStream stream, ILogger? logger=null)
    {
        try
        {
            using MemoryStream m = new();
            await using var data = stream.GetStream();
            await data.CopyToAsync(m);
            var ext = stream.MimeType.FileExtensions is { Length: > 0 } exts && !string.IsNullOrEmpty(exts[0])
                ? exts[0].TrimStart('.').ToUpperInvariant()
                : stream.MimeType.Common;
            var f = OperatingSystem.IsLinux()
               ? stream.MimeType.Common
               : ext;
            var bytes = m.ToArray();
            return await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                if (window.Clipboard == null) return false;
                await window.Clipboard.SetValueAsync(DataFormat.CreateBytesPlatformFormat(f), bytes);
                return true;
            });
        }
        catch (Exception ex)
        {
            logger?.Error(ex, "Error while setting clipboard image");
            return false;
        }
    }
}