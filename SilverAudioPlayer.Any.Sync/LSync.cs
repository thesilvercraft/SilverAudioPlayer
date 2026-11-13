using System;
using System.Collections.Generic;
using System.Composition;
using SilverAudioPlayer.Shared;
using SilverCraft.Ekorensis;

namespace SilverAudioPlayer.Any.Sync;

[Export(typeof(ISyncPlugin))]
public class LSync : ISyncPlugin, IAmOnceAgainAskingYouForYourMemory, IAmConfigurable
{
    public ObjectToRemember ConfigObject =
        new(Guid.Parse("720fe248-4d44-4427-8d72-578ba565f33c"), new SyncConfig());

    public IEnumerable<ObjectToRemember> ObjectsToRememberForMe => [ConfigObject];

    public IEnumerable<IConfigurableElement> GetElements()
    {
        return
        [
            new SimpleRow(
                new SimpleLabel("Preferred file extension:"),
                new SimpleTextBox
                {
                    GetPlaceholder = () => ".mp3",
                    GetContent = () => ((SyncConfig)ConfigObject.Value).PreferredFileExtension,
                    SetContent = c =>
                    {
                        if (ConfigObject.Value is not SyncConfig x) return;
                        x.PreferredFileExtension = c;
                        ((ICanBeToldThatAPartOfMeIsChanged)x).PropertyChanged(x, new("PreferredFileExtension"));
                    }
                }
            ),
            new SimpleRow(
                new SimpleLabel("FFmpeg options:"),
                new SimpleTextBox
                {
                    GetPlaceholder = () => "-i \"{in}\" -b:a 320k \"{out}\"",
                    GetContent = () => ((SyncConfig)ConfigObject.Value).FFmpegOptions,
                    SetContent = c =>
                    {
                        if (ConfigObject.Value is not SyncConfig x) return;
                        x.FFmpegOptions = c;
                        ((ICanBeToldThatAPartOfMeIsChanged)x).PropertyChanged(x, new("FFmpegOptions"));
                    }
                }
            ),
            new SimpleRow(
                new SimpleLabel("Storage path pattern:"),
                new SimpleTextBox
                {
                    GetPlaceholder = () => "{Album}",
                    GetContent = () => ((SyncConfig)ConfigObject.Value).PathToStore,
                    SetContent = c =>
                    {
                        if (ConfigObject.Value is not SyncConfig x) return;
                        x.PathToStore = c;
                        ((ICanBeToldThatAPartOfMeIsChanged)x).PropertyChanged(x, new("PathToStore"));
                    }
                }
            ),
            new SimpleRow(
                new SimpleLabel("Filename pattern:"),
                new SimpleTextBox
                {
                    GetPlaceholder = () => "{DiscNumber}.{TrackNumber}.{Title}{Extension}",
                    GetContent = () => ((SyncConfig)ConfigObject.Value).NameFormatting,
                    SetContent = c =>
                    {
                        if (ConfigObject.Value is not SyncConfig x) return;
                        x.NameFormatting = c;
                        ((ICanBeToldThatAPartOfMeIsChanged)x).PropertyChanged(x, new("NameFormatting"));
                    }
                }
            )
        ];
    }

    public void Use(ISyncEnvironmentListener env)
    {
        MainWindow mw = new(env)
        {
            config = (SyncConfig)ConfigObject.Value
        };
        mw.Show();
    }

    public string Name => "Sync plugin";

    public string Description =>
        "Move your queue over to a sd card, usb flash drive or store it in ram (/tmp) as mp3s using ffmpeg";

    public WrappedStream? Icon => new WrappedEmbeddedResourceStream(typeof(LSync).Assembly,
        "SilverAudioPlayer.Any.Sync.Sync.svg");

    public Version? Version => typeof(LSync).Assembly.GetName().Version;
    public string Licenses => "SilverAudioPlayer.Linux.Sync\nGPL3";
    public List<Tuple<Uri, URLType>>? Links => null;
}