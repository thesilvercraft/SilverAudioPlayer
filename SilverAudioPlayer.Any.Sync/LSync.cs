using System;
using System.Collections.Generic;
using System.Composition;
using SilverAudioPlayer.Shared;

namespace SilverAudioPlayer.Any.Sync;

[Export(typeof(ISyncPlugin))]

public class LSync : ISyncPlugin, IAmOnceAgainAskingYouForYourMemory
{
    public ObjectToRemember ConfigObject =
        new(Guid.Parse("720fe248-4d44-4427-8d72-578ba565f33c"), new SyncConfig());

    public IEnumerable<ObjectToRemember> ObjectsToRememberForMe => [ConfigObject]; public void Use(ISyncEnvironmentListener env)
    {
        MainWindow mw = new(env)
        {
            config = (SyncConfig)ConfigObject.Value
        };
        mw.Show();
    }

    public string Name => "Sync plugin";
    public string Description => "Move your queue over to a sd card, usb flash drive or store it in ram (/tmp) as mp3s using ffmpeg";
    public WrappedStream? Icon => new WrappedEmbeddedResourceStream(typeof(LSync).Assembly,
        "SilverAudioPlayer.Any.Sync.Sync.svg");

    public Version? Version => typeof(LSync).Assembly.GetName().Version;
    public string Licenses => "SilverAudioPlayer.Linux.Sync\nGPL3";
    public List<Tuple<Uri, URLType>>? Links => null;
}