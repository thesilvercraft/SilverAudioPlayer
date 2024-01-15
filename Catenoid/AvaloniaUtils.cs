using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Serilog;
using SilverAudioPlayer.Shared;

namespace Catenoid;
public static class AvaloniaUtils
{
    public static async Task SetClipboardImage(this Window window, WrappedStream stream, ILogger? logger=null)
    {
        try
        {
            MemoryStream m = new();
            await using var data = stream.GetStream();
            await data.CopyToAsync(m);
            var f = OperatingSystem.IsLinux()
               ? stream.MimeType.Common
               : stream.MimeType.FileExtensions[0][1..].ToUpper();
            var bytes = m.ToArray();
            await Dispatcher.UIThread.InvokeAsync(delegate
            {
                DataObject dataObject = new();
                dataObject.Set(f, bytes);
                window.Clipboard?.SetDataObjectAsync(dataObject);
            });
        }
        catch (Exception ex)
        {
            logger?.Error(ex, "Error while setting clipboard image");
        }
    }
}